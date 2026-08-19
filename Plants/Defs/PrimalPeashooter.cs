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

namespace PvZCompleteCollection2.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class PrimalPeashooterDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public PrimalPeashooterDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        // Shoots or rolls straight along the lane, which the roof's slope stops: not for a roof level's conveyor belt (see m_notOnRoof).
        m_notOnRoof = true;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Primal Peashooter", false);
        SetDescription("Primal Peashooter spits heavy rock peas. They hit for two and a half times a pea's damage, and every other one sends the zombie staggering backwards.", false);
        SetTooltip("Shoots slow, hard-hitting rock peas that can knock zombies back.", false);

        // PvZ2 PrimalPeashooterDefault: Cost 175, Hitpoints 300, PacketCooldown 5, shot every 2.85-3 s
        // (PrimalPeashooterPea: BaseDamage 50, StunDuration 0.5, KnockbackChance 0.5). PacketCooldown 5 ->
        // m_refreshTime 500 (~100 ticks/sec, same mapping as every other port here); the shot rate is
        // the same 300 ticks Dusk Lobber uses for 2.85-3 s.
        m_seedCost = 175;
        m_versusCost = 175;
        m_health = 300;

        m_refreshTime = 500;
        m_versusBaseRefreshTime = 500;
        m_versusSuddenDeathRefreshTime = 125;

        m_launchRate = 300;

        // The other pea shooters (Peapod, Mega Gatling) are in the Appease-mint family.
        m_mintFamily = MintFamily.AppeaseMint;

        SetSeedPacketImage(CompleteCollectionMod.PrimalPeashooterBundleId, "assets/plantrip/primal_peashooter/spr_seedpacket_primal_peashooter.png");
        SetPreviewImage(CompleteCollectionMod.PrimalPeashooterBundleId, "assets/plantrip/primal_peashooter/spr_seedpacket_primal_peashooter.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "PrimalPeashooter_Default",
            AssetBundleId = CompleteCollectionMod.PrimalPeashooterBundleId,
            SkinPrefabId = "assets/plantrip/primal_peashooter/models/default/plant/primal_peashooter.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the shot sound is a PlaySound Animation Event baked into
        // the "attack" clip from the PAM; the rock pea's hit sound is code-driven (see the projectile definition).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<PrimalPeashooterBehaviorController>();
    }
}

// Two animation states: the Animator default (idle) and "attack" (31 frames at 30 fps). The PAM marks the
// launch frame (use_action on +16); the bundle build bakes an ExecuteAttack event there, so the pea leaves on
// the clip's own keyframe and follows the game speed, no timers in code.
[RegisterTypeInIl2Cpp]
public class PrimalPeashooterBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // Same nudge FirePeashooter / Peapod / Mega Gatling use (identical PvZ2 spawn offset, 35/-32). Tune by eye in game.
    private const float MouthHeightOffset = 23f;

    // The rock pea art is anchored at its fixed lowest point, 68.5 art pixels below the ball's centre (from the
    // exported manifest: ball 988..1081, anchor 1103, centre 1034.5), and one board pixel is 2.82 world units, so
    // to put the ball's centre where a native pea's is, the anchor goes 68.5 / 2.82 = 24 board pixels lower (Y grows
    // downward in this engine). Tune by eye in game.
    private const float RockPeaAnchorDrop = 24f;

    #endregion

    #region Constructors

    public PrimalPeashooterBehaviorController(IntPtr pointer) : base(pointer)
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

        PlayAnimation("attack");
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
        var projectile = SpawnProjectile(PrimalPeaProjectileDefinition.PrimalPeaProjectileType);
        projectile.mPosY += MouthHeightOffset + RockPeaAnchorDrop;
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

// PvZ2 PrimalPeashooterPea: BaseDamage 50, shooter damage flags, speed 333 (the pea's), StunDuration 0.5,
// KnockbackChance 0.5 / KnockbackDistance 64, art T_PEA_ROCK_PROJECTILE, splat T_SPLAT_PEA_ROCK, impact sound
// Play_Plant_Peapod_Impact_Nitro. Replanted has no such pea, so this is a custom projectile that flies like a
// pea (Straight motion, same speed as a native pea).
[RegisterTypeInIl2Cpp]
public class PrimalPeaProjectileDefinition : CustomProjectileDefinition
{
    #region Variables

    public static ProjectileType PrimalPeaProjectileType;

    // Instantiated per impact by PrimalPeaBehaviorController.
    public GameObject HitEffectPrefab;

    #endregion

    #region Constructors

    public PrimalPeaProjectileDefinition(IntPtr pointer) : base(pointer)
    {
        PrimalPeaProjectileType = m_projectileType;

        // PvZ2: pea 20, rock pea 50.
        m_damage = 50;
        m_motionType = ProjectileMotion.Straight;
        SetDamageRangeFlags(DamageRangeFlags.Ground);
        // Impact sounds are code-driven (not in the PAM): Play_Plant_Peapod_Impact_Nitro, first variant.
        SetHitSfx(CompleteCollectionMod.PrimalPeashooterBundleId, "assets/plantrip/primal_peashooter/sounds/184881107.ogg");

        RegistryBridge.LoadAssetFromAssetBundle<GameObject>(
            CompleteCollectionMod.PrimalPeashooterBundleId,
            "assets/plantrip/primal_peashooter/models/default/projectile_hit/projectile_hit.prefab",
            prefab => HitEffectPrefab = prefab);

        RegisterSkin(new SpriteRendererProjectileSkin()
        {
            skinId = "PrimalPea_Default",
            AssetBundleId = CompleteCollectionMod.PrimalPeashooterBundleId,
            SkinPrefabId = "assets/plantrip/primal_peashooter/models/default/projectile/projectile.prefab",
        });
    }

    #endregion

    #region Methods

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<PrimalPeaBehaviorController>();
    }

    #endregion
}

[RegisterTypeInIl2Cpp]
public class PrimalPeaBehaviorController : CustomProjectileBehaviorController
{
    #region Variables

    // PvZ2 KnockbackChance 0.5, KnockbackDistance 64 (board units). The PvZ2 hop height of 250 and 0.1 s
    // flight time do not translate to this engine's ZombiePushBack (a slide with a parabolic hop): a short
    // quick shove is used instead. StunDuration 0.5 is left out (a half-second freeze of the animation).
    private const float KnockbackChance = 0.5f;
    private const float KnockbackDistance = 64f;
    private const int KnockbackTicks = 15;
    private const float KnockbackHop = 25f;

    #endregion

    #region Constructors

    public PrimalPeaBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    // The engine only knows its own projectile types, so the hit is done here (same as Mega Gatling's giant
    // pea): the zombie takes the full damage (and the hit sound plays), maybe gets shoved back, then the pea is gone.
    public override bool PreDoImpact(Zombie theZombie)
    {
        var customDef = ProjectileDefinition.TryCast<PrimalPeaProjectileDefinition>();

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

    // Heavy zombies and the like do not move for a pea.
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

    private static void SpawnHitEffect(PrimalPeaProjectileDefinition customDef, Zombie theZombie)
    {
        if (customDef?.HitEffectPrefab == null || theZombie.mController == null)
        {
            return;
        }

        var instance = UnityEngine.Object.Instantiate(customDef.HitEffectPrefab, theZombie.mController.gameObject.transform.position, Quaternion.identity);
        // The splat is 20 frames = 0.67 s at 30 fps.
        UnityEngine.Object.Destroy(instance, 1f);
    }

    #endregion
}
