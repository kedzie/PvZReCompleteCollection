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
public class PVineDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public PVineDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        // Shoots straight along the lane, which the roof's slope stops: not for a roof level's conveyor belt (see m_notOnRoof).
        m_notOnRoof = true;

        // PvZ2 MultiPlantLayer "armor": Pea Vine goes on top of another plant (any plant, one Pea Vine per tile) and
        // stays a plant of its own there (see CustomPlantDefinition.m_plantsOnTopOfPlant).
        m_plantsOnTopOfPlant = true;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Pea Vine", false);
        SetDescription("Pea Vine wraps around another plant, planted right on top of it, and lashes out two vine-wrapped peas at a time down the lane.", false);
        SetTooltip("Plant it on top of another plant. Shoots two vine peas per volley.", false);

        // PvZ2 PvineDefault: Cost 150, Hitpoints 300, PacketCooldown 5, two identical projectile actions every
        // 1.35-1.5 s (PVineDefault: BaseDamage 20). PacketCooldown 5 -> m_refreshTime 500 (~100 ticks/sec, same
        // mapping as every other port here); 1.35-1.5 s -> 140 like Peapod / Mega Gatling.
        // Not ported: the Appease-mint boost it gives the plant it sits on.
        m_seedCost = 150;
        m_versusCost = 150;
        m_health = 300;

        m_refreshTime = 500;
        m_versusBaseRefreshTime = 500;
        m_versusSuddenDeathRefreshTime = 125;

        m_launchRate = 140;

        m_mintFamily = MintFamily.AppeaseMint;

        SetSeedPacketImage(CompleteCollectionMod.PVineBundleId, "assets/plantrip/pvine/spr_seedpacket_pvine.png");
        SetPreviewImage(CompleteCollectionMod.PVineBundleId, "assets/plantrip/pvine/spr_seedpacket_pvine.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "PVine_Default",
            AssetBundleId = CompleteCollectionMod.PVineBundleId,
            SkinPrefabId = "assets/plantrip/pvine/models/default/plant/pvine.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the spit sound is a PlaySound Animation Event baked into
        // the "attack" clip from the PAM; the pea's hit sound is code-driven (see the projectile definition).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<PVineBehaviorController>();
    }
}

// Two animation states: the Animator default (idle) and "attack" (37 frames at 30 fps). The PAM marks one launch
// frame (use_action on +14); the bundle build bakes an ExecuteAttack event there and a second one 6 frames later
// (PvZBundleBuilder.ExtraLaunchOffsets "pvine"), so each pea leaves on a clip keyframe and follows the game speed,
// no timers in code.
[RegisterTypeInIl2Cpp]
public class PVineBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // The vine's heads sit lower than a Peashooter's mouth (23 put the peas too high). 35 is where Peanut's lower
    // pea leaves (its 19 + SecondPeaDrop 16), which is the height the user asked for. Tune by eye in game.
    private const float MouthHeightOffset = 35f;

    // The pea art is anchored at its fixed lowest point, 39 art pixels below the pea's centre (from the exported
    // manifest: centre 1028, anchor 1067), and one board pixel is 2.82 world units, so the anchor goes
    // 39 / 2.82 = 13.8 board pixels lower to put the pea's centre where a native pea's is. Tune by eye in game.
    private const float PeaAnchorDrop = 13.8f;

    #endregion

    #region Constructors

    public PVineBehaviorController(IntPtr pointer) : base(pointer)
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
        var projectile = SpawnProjectile(PVineProjectileDefinition.PVineProjectileType);
        projectile.mPosY += MouthHeightOffset + PeaAnchorDrop;
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

    #endregion
}

// PvZ2 PVineDefault: BaseDamage 20, shooter damage flags, speed 333 (a pea's), art POPANIM_EFFECTS_PVINE_PROJECTILE,
// splat PVINE_PROJECTILE_HIT, impact sound Play_Splat. Replanted has no such pea, so this is a custom projectile
// that flies like a pea (Straight motion).
[RegisterTypeInIl2Cpp]
public class PVineProjectileDefinition : CustomProjectileDefinition
{
    #region Variables

    public static ProjectileType PVineProjectileType;

    // Instantiated per impact by PVineProjectileBehaviorController.
    public GameObject HitEffectPrefab;

    #endregion

    #region Constructors

    public PVineProjectileDefinition(IntPtr pointer) : base(pointer)
    {
        PVineProjectileType = m_projectileType;

        // PvZ2: pea 20, Pea Vine's pea 20 (the vine's value is in having two).
        m_damage = 20;
        m_motionType = ProjectileMotion.Straight;
        SetDamageRangeFlags(DamageRangeFlags.Ground);
        // Impact sounds are code-driven (not in the PAM): Play_Splat, first variant.
        SetHitSfx(CompleteCollectionMod.PVineBundleId, "assets/plantrip/pvine/sounds/475957.ogg");

        RegistryBridge.LoadAssetFromAssetBundle<GameObject>(
            CompleteCollectionMod.PVineBundleId,
            "assets/plantrip/pvine/models/default/projectile_hit/projectile_hit.prefab",
            prefab => HitEffectPrefab = prefab);

        RegisterSkin(new SpriteRendererProjectileSkin()
        {
            skinId = "PVinePea_Default",
            AssetBundleId = CompleteCollectionMod.PVineBundleId,
            SkinPrefabId = "assets/plantrip/pvine/models/default/projectile/projectile.prefab",
        });
    }

    #endregion

    #region Methods

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<PVineProjectileBehaviorController>();
    }

    #endregion
}

[RegisterTypeInIl2Cpp]
public class PVineProjectileBehaviorController : CustomProjectileBehaviorController
{
    #region Constructors

    public PVineProjectileBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    // The engine only knows its own projectile types, so the hit is done here: the zombie takes the damage (and
    // the hit sound plays), the splat is shown, then the pea is gone.
    public override bool PreDoImpact(Zombie theZombie)
    {
        var customDef = ProjectileDefinition.TryCast<PVineProjectileDefinition>();

        if (theZombie != null)
        {
            DamageZombie(theZombie);
            SpawnHitEffect(customDef, theZombie);
        }

        // Returning false skips the native DoImpact, which is what normally ends the projectile.
        Projectile.Die();
        return false;
    }

    private static void SpawnHitEffect(PVineProjectileDefinition customDef, Zombie theZombie)
    {
        if (customDef?.HitEffectPrefab == null || theZombie.mController == null)
        {
            return;
        }

        ZombieClipEffect.SpawnHit(theZombie, customDef.HitEffectPrefab, 1f);
    }

    #endregion
}
