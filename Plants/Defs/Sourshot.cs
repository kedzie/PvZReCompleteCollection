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
public class SourshotDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public SourshotDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        // Shoots along the lane, which the roof's slope stops: not for a roof level's conveyor belt (see m_notOnRoof).
        m_notOnRoof = true;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Sourshot", false);
        SetDescription("Sourshot spits sour lemons two at a time. Each one hits hard and sometimes sends the zombie staggering backwards.", false);
        SetTooltip("Spits two lemons per volley that can knock zombies back.", false);

        // PvZ2 SourshotDefault: Cost 150, Hitpoints 700, PacketCooldown 5 (m_refreshTime 500: ~100 ticks/sec, the
        // same mapping as every other port here). The attack is one 60-frame sequence (wind-up, a loop with two
        // shots, wind-down): a volley every 200 ticks plays it exactly once, i.e. one lemon per second.
        m_seedCost = 150;
        m_versusCost = 150;
        m_health = 700;

        m_refreshTime = 500;
        m_versusBaseRefreshTime = 500;
        m_versusSuddenDeathRefreshTime = 125;

        m_launchRate = 200;

        // The other pea shooters (Peapod, Mega Gatling, Primal Peashooter) are in the Appease-mint family.
        m_mintFamily = MintFamily.AppeaseMint;

        SetSeedPacketImage(CompleteCollectionMod.SourshotBundleId, "assets/plantrip/sourshot/spr_seedpacket_sourshot.png");
        SetPreviewImage(CompleteCollectionMod.SourshotBundleId, "assets/plantrip/sourshot/spr_seedpacket_sourshot.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Sourshot_Default",
            AssetBundleId = CompleteCollectionMod.SourshotBundleId,
            SkinPrefabId = "assets/plantrip/sourshot/models/default/plant/sourshot.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the spit sound is a PlaySound Animation Event baked into the
        // "attack_loop" clip from the PAM; the lemon's hit sound is code-driven (see the projectile definition).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<SourshotBehaviorController>();
    }
}

// Animation states: the Animator default (idle) and the attack chain attack_start -> attack_loop -> attack_end,
// which the Animator walks by itself (idle again at the end). The PAM marks the two shots in attack_loop
// (use_action on +3 and +11); the bundle build bakes an ExecuteAttack event on each, so each lemon leaves on the
// clip's own keyframe and follows the game speed, no timers in code.
[RegisterTypeInIl2Cpp]
public class SourshotBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // Same nudge Primal Peashooter / Peapod / Mega Gatling use (identical PvZ2 spawn offset). Tune by eye in game.
    private const float MouthHeightOffset = 23f;

    // The lemon art is anchored at its fixed lowest point, 24.5 art pixels below the lemon's centre (from the
    // exported manifest: centre 979.5, anchor 1004), and one board pixel is 2.82 world units, so the anchor goes
    // 24.5 / 2.82 = 8.7 board pixels lower to put the lemon's centre where a native pea's is. Tune by eye in game.
    private const float LemonAnchorDrop = 8.7f;

    #endregion

    #region Constructors

    public SourshotBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override void OnLaunchCounterTriggered()
    {
        base.OnLaunchCounterTriggered();

        if (FindTarget() == null)
        {
            return;
        }

        PlayAnimation("attack_start");
    }

    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        var target = FindTarget();
        if (target == null)
        {
            return;
        }

        // A custom projectile that flies like a pea (see FirePeashooter for why the mouth offset and
        // mDamageRangeFlags are needed with SpawnProjectile).
        var projectile = SpawnProjectile(SourshotProjectileDefinition.SourshotProjectileType);
        projectile.mPosY += MouthHeightOffset + LemonAnchorDrop;
        projectile.mTargetZombieID = target.mRelatedZombieID;
        projectile.mDamageRangeFlags = DamageRangeFlags.Ground;
    }

    // Nearest living zombie ahead of the plant in its lane.
    private Zombie FindTarget()
    {
        if (Board?.m_zombies?.m_list == null)
        {
            return null;
        }

        return Board.m_zombies.m_list.ToList()
            .Where(z => z.mItem != null
                        && !z.mItem.IsDeadOrDying()
                        && (z.mItem.mRow == Plant.mRow || z.mItem.mZombieType == ZombieType.Boss)
                        && z.mItem.EffectedByDamage(DamageRangeFlags.Ground)
                        && z.mItem.mPosX > Plant.mX)
            .OrderBy(z => z.mItem.mPosX)
            .Select(z => z.mItem)
            .FirstOrDefault();
    }

    #endregion
}

// PvZ2 SourshotProjectile: BaseDamage 20, shooter damage flags, a pea's speed, KnockbackChance 0.5 /
// KnockbackDistance 128, art SOURSHOT_PROJECTILE, splat SOURSHOT_PROJECTILE_IMPACT, impact sound
// Play_Plant_Peapod_Impact_Nitro. Replanted has no such pea, so this is a custom projectile that flies like a
// pea (Straight motion, same speed as a native pea).
[RegisterTypeInIl2Cpp]
public class SourshotProjectileDefinition : CustomProjectileDefinition
{
    #region Variables

    public static ProjectileType SourshotProjectileType;

    // Instantiated per impact by SourshotProjectileBehaviorController.
    public GameObject HitEffectPrefab;

    #endregion

    #region Constructors

    public SourshotProjectileDefinition(IntPtr pointer) : base(pointer)
    {
        SourshotProjectileType = m_projectileType;

        m_damage = 20;
        m_motionType = ProjectileMotion.Straight;
        SetDamageRangeFlags(DamageRangeFlags.Ground);
        // Impact sounds are code-driven (not in the PAM): Play_Plant_Peapod_Impact_Nitro, first variant.
        SetHitSfx(CompleteCollectionMod.SourshotBundleId, "assets/plantrip/sourshot/sounds/184881107.ogg");

        RegistryBridge.LoadAssetFromAssetBundle<GameObject>(
            CompleteCollectionMod.SourshotBundleId,
            "assets/plantrip/sourshot/models/default/projectile_hit/projectile_hit.prefab",
            prefab => HitEffectPrefab = prefab);

        RegisterSkin(new SpriteRendererProjectileSkin()
        {
            skinId = "SourshotProjectile_Default",
            AssetBundleId = CompleteCollectionMod.SourshotBundleId,
            SkinPrefabId = "assets/plantrip/sourshot/models/default/projectile/projectile.prefab",
        });
    }

    #endregion

    #region Methods

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<SourshotProjectileBehaviorController>();
    }

    #endregion
}

[RegisterTypeInIl2Cpp]
public class SourshotProjectileBehaviorController : CustomProjectileBehaviorController
{
    #region Variables

    // PvZ2 KnockbackChance 0.5, KnockbackDistance 128 (board units). PvZ2's hop height and flight time do not
    // translate to this engine's ZombiePushBack (a slide with a parabolic hop): a quick shove is used instead,
    // twice as far as Primal Peashooter's (64) like the data says.
    private const float KnockbackChance = 0.5f;
    private const float KnockbackDistance = 128f;
    private const int KnockbackTicks = 25;
    private const float KnockbackHop = 30f;

    #endregion

    #region Constructors

    public SourshotProjectileBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    // The engine only knows its own projectile types, so the hit is done here (same as Primal Peashooter's rock
    // pea): the zombie takes the full damage (and the hit sound plays), maybe gets shoved back, then the lemon is gone.
    public override bool PreDoImpact(Zombie theZombie)
    {
        var customDef = ProjectileDefinition.TryCast<SourshotProjectileDefinition>();

        if (theZombie != null)
        {
            DamageZombie(theZombie);
            SpawnHitEffect(customDef, theZombie);

            if (UnityEngine.Random.value < KnockbackChance && CanBeShoved(theZombie))
            {
                ZombiePushBack.Push(theZombie, Board, KnockbackDistance, KnockbackTicks, KnockbackHop);
            }
        }

        // Returning false skips the native DoImpact, which is what normally ends the projectile.
        Projectile.Die();
        return false;
    }

    // Heavy zombies and the like do not move for a lemon.
    private static bool CanBeShoved(Zombie zombie)
    {
        if (zombie.IsDeadOrDying())
        {
            return false;
        }

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

    private static void SpawnHitEffect(SourshotProjectileDefinition customDef, Zombie theZombie)
    {
        if (customDef?.HitEffectPrefab == null || theZombie.mController == null)
        {
            return;
        }

        var instance = UnityEngine.Object.Instantiate(customDef.HitEffectPrefab, theZombie.mController.gameObject.transform.position, Quaternion.identity);
        // The splat is 10 frames = 0.33 s at 30 fps.
        UnityEngine.Object.Destroy(instance, 1f);
    }

    #endregion
}
