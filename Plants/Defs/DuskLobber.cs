using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Projectile;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Content.Projectiles;
using PvZReCoreLib.Util;
using UnityEngine;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class DuskLobberDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public DuskLobberDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Dusk Lobber", false);
        SetDescription("Dusk Lobber hurls glowing thorn bulbs over the lane. Each one bursts on impact, hurting the zombie it lands on and the ones around it.", false);
        SetTooltip("Dusk Lobber lobs thorn bulbs that splash the zombies around the target.", false);

        // PvZ2 DuskLobberDefault: Cost 150, PacketCooldown 10s, shot every 2.85-3s
        // (the same 300 launch rate Bloomerang uses for 2.85-3s).
        m_seedCost = 150;
        m_versusCost = 150;

        m_refreshTime = 1000;
        m_versusBaseRefreshTime = 1000;
        m_versusSuddenDeathRefreshTime = 250;

        m_launchRate = 300;

        SetSeedPacketImage(CompleteCollectionMod.DuskLobberBundleId, "assets/plantrip/dusklobber/spr_seedpacket_dusklobber.png");
        SetPreviewImage(CompleteCollectionMod.DuskLobberBundleId, "assets/plantrip/dusklobber/spr_seedpacket_dusklobber.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "DuskLobber_Default",
            AssetBundleId = CompleteCollectionMod.DuskLobberBundleId,
            SkinPrefabId = "assets/plantrip/dusklobber/models/default/plant/dusklobber.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<DuskLobberBehaviorController>();
    }
}

// Two animation states: the Animator's default clip (idle) and "attack". The attack clip carries
// the PlaySound events (baked from the PAM) and an ExecuteAttack event on its launch frame, so
// both the sound and the throw follow the game's animation speed - no timers in code.
[RegisterTypeInIl2Cpp]
public class DuskLobberBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // The native lob (Plant.Fire for Cabbage-pult & co.) always flies for 120 ticks.
    private const float FlightTicks = 120f;

    #endregion

    #region Constructors

    public DuskLobberBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    // The launch counter only says "time to shoot": start the attack clip if there is something
    // to hit. The projectile itself leaves on the clip's ExecuteAttack event.
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

        var projectile = SpawnProjectile(DuskLobberProjectileDefinition.DuskLobberProjectileType);
        projectile.mTargetZombieID = target.mRelatedZombieID;
        // Native CheckForCollision ignores every zombie unless this is set (see FirePeashooter).
        projectile.mDamageRangeFlags = DamageRangeFlags.Ground;

        // Same lob setup as the native Plant.Fire (read from the decompile, same maths as PvZ1):
        // X covers the distance to where the zombie will be, Z is the vertical throw that lands
        // on the zombie's rect after FlightTicks, mAccZ is gravity.
        projectile.mMotionType = ProjectileMotion.Lobbed;
        projectile.mVelX = (target.ZombieTargetLeadX(50f) - projectile.mPosX - 30f) / FlightTicks;
        projectile.mVelY = 0f;
        projectile.mVelZ = (target.GetZombieRect().y - projectile.mPosY) / FlightTicks - 7f;
        projectile.mAccZ = 0.115f;
    }

    // Nearest living zombie ahead of the plant in its lane.
    private Zombie FindTarget()
    {
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

[RegisterTypeInIl2Cpp]
public class DuskLobberProjectileDefinition : CustomProjectileDefinition
{
    #region Variables

    public static ProjectileType DuskLobberProjectileType;

    // PvZ2 DuskLobberDefault projectile: BaseDamage 30, SplashDamage 25.
    public const int SplashDamage = 25;

    // Instantiated per impact by DuskLobberProjectileBehaviorController.
    public GameObject HitEffectPrefab;

    #endregion

    #region Constructors

    public DuskLobberProjectileDefinition(IntPtr pointer) : base(pointer)
    {
        DuskLobberProjectileType = m_projectileType;

        m_damage = 30;
        m_motionType = ProjectileMotion.Lobbed;
        SetDamageRangeFlags(DamageRangeFlags.Ground);
        // Impact sounds are code-driven (not in the PAM): Play_Plant_DuskLobber_Impact, first variant.
        SetHitSfx(CompleteCollectionMod.DuskLobberBundleId, "assets/plantrip/dusklobber/sounds/326960603.ogg");

        RegistryBridge.LoadAssetFromAssetBundle<GameObject>(
            CompleteCollectionMod.DuskLobberBundleId,
            "assets/plantrip/dusklobber/models/default/projectile_hit/projectile_hit.prefab",
            prefab => HitEffectPrefab = prefab);

        RegisterSkin(new SpriteRendererProjectileSkin()
        {
            skinId = "DuskLobberProjectile_Default",
            AssetBundleId = CompleteCollectionMod.DuskLobberBundleId,
            SkinPrefabId = "assets/plantrip/dusklobber/models/default/projectile/projectile.prefab",
        });
    }

    #endregion

    #region Methods

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<DuskLobberProjectileBehaviorController>();
    }

    #endregion
}

[RegisterTypeInIl2Cpp]
public class DuskLobberProjectileBehaviorController : CustomProjectileBehaviorController
{
    #region Constructors

    public DuskLobberProjectileBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    // The engine only gives splash to the melon/fireball types, so a custom type does it here:
    // the zombie the lob lands on takes the full damage (and plays the hit sound), every other
    // zombie the native IsZombieHitBySplash accepts (adjacent rows, overlapping the blast) takes
    // the splash damage.
    public override bool PreDoImpact(Zombie theZombie)
    {
        var customDef = ProjectileDefinition.TryCast<DuskLobberProjectileDefinition>();

        if (theZombie != null)
        {
            DamageZombie(theZombie);
            SpawnHitEffect(customDef, theZombie);
        }

        foreach (var entry in Board.m_zombies.m_list.ToList())
        {
            var zombie = entry.mItem;
            if (zombie == null || zombie == theZombie || zombie.IsDeadOrDying()
                || !zombie.EffectedByDamage(DamageRangeFlags.Ground)
                || !Projectile.IsZombieHitBySplash(zombie))
            {
                continue;
            }

            zombie.TakeDamage(DuskLobberProjectileDefinition.SplashDamage, customDef.m_damageFlags);
        }

        // Returning false skips the native DoImpact, which is what normally ends the projectile.
        Projectile.Die();
        return false;
    }

    private static void SpawnHitEffect(DuskLobberProjectileDefinition customDef, Zombie theZombie)
    {
        if (customDef?.HitEffectPrefab == null || theZombie.mController == null)
        {
            return;
        }

        var instance = UnityEngine.Object.Instantiate(customDef.HitEffectPrefab, theZombie.mController.gameObject.transform.position, Quaternion.identity);
        UnityEngine.Object.Destroy(instance, 1f);
    }

    #endregion
}
