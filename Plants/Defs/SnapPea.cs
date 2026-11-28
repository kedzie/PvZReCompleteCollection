using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Behavior;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Projectile;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Content.Plants.Mint;
using PvZReCoreLib.Content.Projectiles;
using PvZReCoreLib.Util;
using UnityEngine;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class SnapPeaDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    // A second copy of the plant's own skin, used to draw the bite where the zombie is (see PlantClipEffect).
    public static GameObject BitePrefab;

    public SnapPeaDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Snap Pea", false);
        SetDescription("Snap Pea burrows under the nearest zombie and swallows it whole. While it chews, it keeps the zombie's head, and when it is done it spits the head back at the next zombie in the lane.", false);
        SetTooltip("Eats one zombie, then spits its head at another.", false);

        // PvZ2 SnapPeaDefault: Cost 200, Hitpoints 300, PacketCooldown 10 (StartingCooldown 5).
        // PacketCooldown 10 -> m_refreshTime 1000 (~100 ticks/sec, same mapping as every other port here).
        m_seedCost = 200;
        m_versusCost = 200;
        m_health = 300;

        m_refreshTime = 1000;
        m_versusBaseRefreshTime = 1000;
        m_versusSuddenDeathRefreshTime = 250;

        // Polling rate only: the bite / chew / spit cycle runs on its own state machine (see the controller).
        m_launchRate = 150;

        m_mintFamily = MintFamily.EnforceMint;

        SetSeedPacketImage(CompleteCollectionMod.SnapPeaBundleId, "assets/plantrip/snappea/spr_seedpacket_snappea.png");
        SetPreviewImage(CompleteCollectionMod.SnapPeaBundleId, "assets/plantrip/snappea/spr_seedpacket_snappea.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "SnapPea_Default",
            AssetBundleId = CompleteCollectionMod.SnapPeaBundleId,
            SkinPrefabId = "assets/plantrip/snappea/models/default/plant/snappea.prefab",
        });

        RegistryBridge.LoadAssetFromAssetBundle<GameObject>(
            CompleteCollectionMod.SnapPeaBundleId,
            "assets/plantrip/snappea/models/default/plant/snappea.prefab",
            prefab => BitePrefab = prefab);

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the burrow, bite, chew and spit sounds are PlaySound Animation
        // Events baked into the clips from the PAM; the head's hit sound is code-driven (see the projectile definition).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<SnapPeaBehaviorController>();
    }
}

// PvZ2's Snap Pea burrows to the nearest zombie in its lane. Its PAM clips, chained by the Animator:
//   idle -> burrow_start -> burrow_loop (holds) -> burrow_return_chewing -> chewing_loop (holds) -> head_fire -> idle
// and, drawn where the ZOMBIE is (the plant stays sunk at home; the clip is a free-standing copy of its skin played
// under the zombie by CoreLib's PlantClipEffect, whose baked ExecuteAttack events drive this code in clip order):
//   burrow_attack01: pops up high, 3 peas angled down, then swallows the zombie   (use_action x3, eat_action)
//   burrow_attack02: 3 peas only, for a zombie it cannot eat (Gargantuar...)      (use_action x3), 4 s cooldown
//   burrow_attack03: swallows the zombie from below, no peas                      (eat_action)
// After a swallow the plant grows back at home chewing for 20 s, then spits the head (head_fire, fire_action).
// Only the two holds (burrow_loop, chewing_loop) and the safety timeouts are measured in ticks here.
[RegisterTypeInIl2Cpp]
public class SnapPeaBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    private enum State
    {
        Idle,
        Burrowing,
        Biting,
        Chewing,
        Spitting,
    }

    // Board ticks (~100 per second).
    private const int BurrowTicks = 150;       // burrow_start (32 frames = ~107 ticks) + a short burrow_loop
    private const int BiteTimeoutTicks = 600;  // burrow_attack01 is 128 frames = ~427 ticks; only for a lost effect
    private const int ChewTicks = 2000;        // PvZ2 ChewTimeSeconds 20
    private const int NoChewTicks = 150;       // nothing was swallowed: just let the return animation finish
    private const int SpitRetryTicks = 150;    // PvZ2 FiredZombieHeadAttackCooldownSeconds 1.5
    private const int SpitTimeoutTicks = 200;  // head_fire is 51 frames = ~170 ticks
    private const int NoEatCooldownTicks = 400; // PvZ2 CouldNotEatAttackCooldownSeconds 4: after a peas-only attack
    private const int PollTicks = 10;
    private const int HoldGraceTicks = 60;     // let the Animator chain run before checking a hold

    // The 3 peas of burrow_attack01/02 (PvZ2 SnapPeaDefault: 30 damage, spawned high above the zombie, flying down at
    // it). Board pixels: they start this far in front of the zombie's front edge and above its head, and drop at
    // this speed per tick while flying at a pea's speed. Tune by eye in game.
    private const int BitePeaCount = 3;
    private const float BitePeaBehind = 30f;
    private const float BitePeaAbove = 35f;
    private const float BitePeaFallSpeed = 3f;

    // How far ahead of the plant (board pixels, 80 per tile) a zombie must be to be bitten: the whole lane, as in
    // PvZ2 (its Snap Pea burrows to the zombie and pops up under it; see ShiftToTarget). Lower it to shorten the reach.
    private const float BiteRange = 720f;

    // Half a tile: the plant's pixel position is the tile's left edge, the sprite is centred on the tile.
    private const float HalfTile = 40f;

    // Same nudge FirePeashooter / Peapod / Primal Peashooter use (identical PvZ2 spawn offset, 35/-32). Tune by eye in game.
    private const float MouthHeightOffset = 23f;

    private State state = State.Idle;
    private int stateTicks;
    private int chewTicks;
    private bool hasZombie;

    // The bite is drawn where the zombie is (see PlantClipEffect); the plant stays in burrow_loop meanwhile.
    private GameObject biteEffect;
    private Zombie biteTarget;
    private string biteClip;
    private int biteEvents;
    private bool swallowed;
    private int idleWaitTicks;

    #endregion

    #region Constructors

    public SnapPeaBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override void PostPlantUpdate()
    {
        base.PostPlantUpdate();

        if (Plant == null || Board == null)
        {
            return;
        }

        stateTicks++;
        KeepHold();

        switch (state)
        {
            case State.Idle:
                if (stateTicks % PollTicks == 0 && stateTicks >= idleWaitTicks && FindLaneZombie() != null)
                {
                    idleWaitTicks = 0;
                    SetState(State.Burrowing);
                    PlayAnimation("burrow_start");
                }
                break;

            case State.Burrowing:
                if (stateTicks >= BurrowTicks)
                {
                    var target = FindLaneZombie();
                    if (target == null)
                    {
                        // The zombie died or left while the plant was burrowing: just go home.
                        EndCycle(true);
                        break;
                    }

                    StartBite(target);
                }
                break;

            case State.Biting:
                // The swallow arrives as an event of the bite effect (OnAnimationAttackEvent); the effect ends itself
                // when its clip has played. Then the plant grows back at home, chewing, or goes home empty-handed.
                if (biteEffect == null || stateTicks >= BiteTimeoutTicks)
                {
                    if (hasZombie)
                    {
                        chewTicks = ChewTicks;
                        SetState(State.Chewing);
                        PlayAnimation("burrow_return_chewing");
                    }
                    else
                    {
                        // Peas only (or nothing to swallow): rest a while before the next burrow.
                        idleWaitTicks = biteClip == "burrow_attack02" ? NoEatCooldownTicks : 0;
                        EndCycle(true);
                    }
                }
                break;

            case State.Chewing:
                if (stateTicks >= chewTicks)
                {
                    if (!hasZombie)
                    {
                        EndCycle(true);
                    }
                    else if (FindSpitTarget() != null)
                    {
                        SetState(State.Spitting);
                        PlayAnimation("head_fire");
                    }
                    else
                    {
                        // Nothing to spit at yet: keep chewing and look again in 1.5 s.
                        chewTicks = stateTicks + SpitRetryTicks;
                    }
                }
                break;

            case State.Spitting:
                if (stateTicks >= SpitTimeoutTicks)
                {
                    // head_fire has played out and the Animator is back in idle by itself.
                    EndCycle(false);
                }
                break;
        }
    }

    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        if (state == State.Biting)
        {
            // Events of the bite effect, in clip order: first the peas (use_action x3 in 01 and 02), then the swallow
            // (eat_action in 01 and 03).
            int peas = biteClip == "burrow_attack03" ? 0 : BitePeaCount;
            if (biteEvents < peas)
            {
                biteEvents++;
                FireBitePea();
            }
            else if (biteClip != "burrow_attack02")
            {
                Swallow();
            }
        }
        else if (state == State.Spitting)
        {
            // fire_action: the zombie's head leaves the mouth.
            var target = FindSpitTarget();
            if (target != null)
            {
                var projectile = SpawnProjectile(SnapPeaHeadProjectileDefinition.SnapPeaHeadProjectileType);
                projectile.mPosY += MouthHeightOffset + SnapPeaHeadProjectileDefinition.PeaAnchorDrop;
                projectile.mTargetZombieID = target.mRelatedZombieID;
                projectile.mDamageRangeFlags = DamageRangeFlags.Ground;
            }

            hasZombie = false;
        }
    }

    // PvZ2's burrow attack is drawn where the ZOMBIE is: burrow_start sinks the plant into the ground at home
    // (burrow_loop is just the leaves), the stem then rises out of a hole under the zombie (burrow_attack01/03),
    // swallows it and retracts, and burrow_return_chewing grows the plant back at home. The plant stays sunk; the
    // bite is a free-standing copy of its skin played under the zombie.
    private void StartBite(Zombie target)
    {
        SetState(State.Biting);
        biteTarget = target;
        swallowed = false;
        hasZombie = false;

        biteEvents = 0;
        biteClip = !CanBeEaten(target) ? "burrow_attack02"
            : UnityEngine.Random.value < 0.5f ? "burrow_attack01" : "burrow_attack03";

        var rect = target.GetZombieRect();
        float zombieCentreX = rect.x + rect.width * 0.5f;
        float plantCentreX = Board.GridToPixelX(Plant.mPlantCol, Plant.mRow) + HalfTile;

        biteEffect = PlantClipEffect.Spawn(this, SnapPeaDefinition.BitePrefab, biteClip, zombieCentreX - plantCentreX, 4.5f);
        if (biteEffect == null)
        {
            // No effect could be made (the bundle did not load): swallow at once so the plant still works.
            MelonLogger.Warning("[SnapPea] bite effect could not be spawned, swallowing without it");
            if (biteClip != "burrow_attack02")
            {
                Swallow();
            }
        }
    }

    // One of the 3 peas: spawned above and in front of the zombie and flown down into it.
    private void FireBitePea()
    {
        var victim = biteTarget != null && !biteTarget.IsDeadOrDying() ? biteTarget : FindLaneZombie();
        if (victim == null)
        {
            return;
        }

        var rect = victim.GetZombieRect();
        var projectile = SpawnProjectile(SnapPeaPeaProjectileDefinition.SnapPeaPeaProjectileType);
        projectile.mPosX = rect.x - BitePeaBehind;
        projectile.mPosY = rect.y - BitePeaAbove + SnapPeaHeadProjectileDefinition.PeaAnchorDrop;
        projectile.mVelY = BitePeaFallSpeed;
        projectile.mTargetZombieID = victim.mRelatedZombieID;
        projectile.mDamageRangeFlags = DamageRangeFlags.Ground;
    }

    private void Swallow()
    {
        if (swallowed)
        {
            return;
        }

        swallowed = true;

        var victim = biteTarget != null && !biteTarget.IsDeadOrDying() ? biteTarget : FindEdibleZombie(BiteRange + 80f);
        hasZombie = victim != null;
        if (victim != null)
        {
            victim.DieNoLoot();
        }
    }

    // Resilience to the game's own idle calls (see CLAUDE.md, section 5): the two holds have no exit of their own, so
    // if the sprite was knocked back to another clip, the held clip is played again. A short grace period after
    // each state change lets the Animator chain (burrow_start -> burrow_loop, burrow_return_chewing -> chewing_loop)
    // run its course first.
    private void KeepHold()
    {
        if (stateTicks < HoldGraceTicks || stateTicks % PollTicks != 0)
        {
            return;
        }

        if ((state == State.Burrowing || state == State.Biting) && !IsCustomAnimationActive("burrow_start") && !IsCustomAnimationActive("burrow_loop"))
        {
            PlayAnimation("burrow_loop");
        }
        else if (state == State.Chewing && !IsCustomAnimationActive("burrow_return_chewing") && !IsCustomAnimationActive("chewing_loop"))
        {
            PlayAnimation("chewing_loop");
        }
    }

    private void SetState(State next)
    {
        state = next;
        stateTicks = 0;
        MelonLogger.Msg($"[SnapPea] row {Plant?.mRow} col {Plant?.mPlantCol}: {next}");
    }

    // "idle" cannot be requested through PlayAnimation (the engine throws on it, once per cycle), so the plant only
    // ever gets back to idle through the Animator's own exits. head_fire ends in idle; the two holds (burrow_loop /
    // chewing_loop) have no exit, so burrow_return_idle walks the plant home when a cycle ends while it is in one.
    private void EndCycle(bool inHold)
    {
        hasZombie = false;
        SetState(State.Idle);
        if (inHold)
        {
            PlayAnimation("burrow_return_idle");
        }
    }

    // Nearest living zombie ahead of the plant in its lane that it can swallow.
    private Zombie FindEdibleZombie(float range = BiteRange)
    {
        if (Board?.m_zombies?.m_list == null)
        {
            return null;
        }

        return ZombieSnapshot.Items(Board)
            .Where(z => z.mItem != null
                        && !z.mItem.IsDeadOrDying()
                        && z.mItem.mRow == Plant.mRow
                        && z.mItem.EffectedByDamage(DamageRangeFlags.Ground)
                        && z.mItem.mPosX > Plant.mX
                        && z.mItem.mPosX - Plant.mX <= range
                        && CanBeEaten(z.mItem))
            .OrderBy(z => z.mItem.mPosX)
            .Select(z => z.mItem)
            .FirstOrDefault();
    }

    // Nearest zombie ahead of the plant in its lane, eatable or not (the ones it cannot eat get the peas-only attack).
    private Zombie FindLaneZombie()
    {
        if (Board?.m_zombies?.m_list == null)
        {
            return null;
        }

        return ZombieSnapshot.Items(Board)
            .Where(z => z.mItem != null
                        && !z.mItem.IsDeadOrDying()
                        && z.mItem.mRow == Plant.mRow
                        && z.mItem.mZombieType != ZombieType.Boss
                        && z.mItem.mZombieType != ZombieType.Gravestone
                        && z.mItem.EffectedByDamage(DamageRangeFlags.Ground)
                        && z.mItem.mPosX > Plant.mX
                        && z.mItem.mPosX - Plant.mX <= BiteRange)
            .OrderBy(z => z.mItem.mPosX)
            .Select(z => z.mItem)
            .FirstOrDefault();
    }

    // Anyone in the lane ahead of the plant (the head flies like a pea).
    private Zombie FindSpitTarget()
    {
        if (Board?.m_zombies?.m_list == null)
        {
            return null;
        }

        return ZombieSnapshot.Items(Board)
            .Where(z => z.mItem != null
                        && !z.mItem.IsDeadOrDying()
                        && (z.mItem.mRow == Plant.mRow || z.mItem.mZombieType == ZombieType.Boss)
                        && z.mItem.EffectedByDamage(DamageRangeFlags.Ground)
                        && z.mItem.mPosX > Plant.mX)
            .OrderBy(z => z.mItem.mPosX)
            .Select(z => z.mItem)
            .FirstOrDefault();
    }

    // PvZ2 ZombieEatExcludelist: the big and the mechanical ones are too much for it.
    private static bool CanBeEaten(Zombie zombie)
    {
        switch (zombie.mZombieType)
        {
            case ZombieType.Gargantuar:
            case ZombieType.RedeyeGargantuar:
            case ZombieType.Zamboni:
            case ZombieType.Bobsled:
            case ZombieType.Boss:
            case ZombieType.Gravestone:
            case ZombieType.Catapult:
                return false;
            default:
                return true;
        }
    }

    #endregion
}

// PvZ2 ThrownZombieParticle: BaseDamage 400, speed 650. The zombie's own head is not available as art, so the
// head flies as a pea (Straight motion, a pea's speed). Same custom-projectile route as the other pea-likes.
[RegisterTypeInIl2Cpp]
public class SnapPeaHeadProjectileDefinition : CustomProjectileDefinition
{
    #region Variables

    public static ProjectileType SnapPeaHeadProjectileType;

    // The pea art anchor drop, in board pixels (39 art px anchor-to-centre / 2.82, see Pea Vine). Tune by eye in game.
    public const float PeaAnchorDrop = 13.8f;

    // Instantiated per impact by SnapPeaHeadBehaviorController.
    public GameObject HitEffectPrefab;

    #endregion

    #region Constructors

    public SnapPeaHeadProjectileDefinition(IntPtr pointer) : base(pointer)
    {
        SnapPeaHeadProjectileType = m_projectileType;

        m_damage = 400;
        m_motionType = ProjectileMotion.Straight;
        SetDamageRangeFlags(DamageRangeFlags.Ground);
        // Impact sounds are code-driven (not in the PAM): Play_Splat, first variant.
        SetHitSfx(CompleteCollectionMod.SnapPeaBundleId, "assets/plantrip/snappea/sounds/475957.ogg");

        RegistryBridge.LoadAssetFromAssetBundle<GameObject>(
            CompleteCollectionMod.SnapPeaBundleId,
            "assets/plantrip/snappea/models/default/projectile_hit/projectile_hit.prefab",
            prefab => HitEffectPrefab = prefab);

        RegisterSkin(new SpriteRendererProjectileSkin()
        {
            skinId = "SnapPeaHead_Default",
            AssetBundleId = CompleteCollectionMod.SnapPeaBundleId,
            SkinPrefabId = "assets/plantrip/snappea/models/default/projectile/projectile.prefab",
        });
    }

    #endregion

    #region Methods

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<SnapPeaHeadBehaviorController>();
    }

    #endregion
}

[RegisterTypeInIl2Cpp]
public class SnapPeaHeadBehaviorController : CustomProjectileBehaviorController
{
    #region Constructors

    public SnapPeaHeadBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override bool PreDoImpact(Zombie theZombie)
    {
        var customDef = ProjectileDefinition.TryCast<SnapPeaHeadProjectileDefinition>();

        if (theZombie != null)
        {
            DamageZombie(theZombie);
            SpawnHitEffect(customDef, theZombie);
        }

        // Returning false skips the native DoImpact, which is what normally ends the projectile.
        Projectile.Die();
        return false;
    }

    private static void SpawnHitEffect(SnapPeaHeadProjectileDefinition customDef, Zombie theZombie)
    {
        if (customDef?.HitEffectPrefab == null || theZombie.mController == null)
        {
            return;
        }

        ZombieClipEffect.SpawnHit(theZombie, customDef.HitEffectPrefab, 1f);
    }

    #endregion
}

// PvZ2 SnapPeaDefault: BaseDamage 30, a pea's speed and art. The three peas of the burrow attack: same custom-projectile
// route as the head (a native pea would do 20), with the same pea skin and splat.
[RegisterTypeInIl2Cpp]
public class SnapPeaPeaProjectileDefinition : CustomProjectileDefinition
{
    #region Variables

    public static ProjectileType SnapPeaPeaProjectileType;

    // Instantiated per impact by SnapPeaPeaBehaviorController.
    public GameObject HitEffectPrefab;

    #endregion

    #region Constructors

    public SnapPeaPeaProjectileDefinition(IntPtr pointer) : base(pointer)
    {
        SnapPeaPeaProjectileType = m_projectileType;

        m_damage = 30;
        m_motionType = ProjectileMotion.Straight;
        SetDamageRangeFlags(DamageRangeFlags.Ground);
        SetHitSfx(CompleteCollectionMod.SnapPeaBundleId, "assets/plantrip/snappea/sounds/475957.ogg");

        RegistryBridge.LoadAssetFromAssetBundle<GameObject>(
            CompleteCollectionMod.SnapPeaBundleId,
            "assets/plantrip/snappea/models/default/projectile_hit/projectile_hit.prefab",
            prefab => HitEffectPrefab = prefab);

        RegisterSkin(new SpriteRendererProjectileSkin()
        {
            skinId = "SnapPeaPea_Default",
            AssetBundleId = CompleteCollectionMod.SnapPeaBundleId,
            SkinPrefabId = "assets/plantrip/snappea/models/default/projectile/projectile.prefab",
        });
    }

    #endregion

    #region Methods

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<SnapPeaPeaBehaviorController>();
    }

    #endregion
}

[RegisterTypeInIl2Cpp]
public class SnapPeaPeaBehaviorController : CustomProjectileBehaviorController
{
    #region Constructors

    public SnapPeaPeaBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override bool PreDoImpact(Zombie theZombie)
    {
        var customDef = ProjectileDefinition.TryCast<SnapPeaPeaProjectileDefinition>();

        if (theZombie != null)
        {
            DamageZombie(theZombie);

            if (customDef?.HitEffectPrefab != null && theZombie.mController != null)
            {
                ZombieClipEffect.SpawnHit(theZombie, customDef.HitEffectPrefab, 1f);
            }
        }

        // Returning false skips the native DoImpact, which is what normally ends the projectile.
        Projectile.Die();
        return false;
    }

    #endregion
}
