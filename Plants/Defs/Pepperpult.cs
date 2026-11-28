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
public class PepperpultDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public PepperpultDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Pepperpult", false);
        SetDescription("Pepperpult lobs fiery peppers over the lane. Each one bursts on impact, hurting the zombie it lands on and a little the ones around it.", false);
        SetTooltip("Lobs peppers that splash the zombies around the target.", false);

        // PvZ2 PepperpultDefault: Cost 200, Hitpoints 300, PacketCooldown 20 (m_refreshTime 2000: ~100 ticks/sec,
        // the same mapping as every other port here), a throw every 3 s (launch rate 300, as Dusk Lobber's).
        m_seedCost = 200;
        m_versusCost = 200;
        m_health = 300;

        m_refreshTime = 2000;
        m_versusBaseRefreshTime = 2000;
        m_versusSuddenDeathRefreshTime = 500;

        m_launchRate = 300;

        SetSeedPacketImage(CompleteCollectionMod.PepperpultBundleId, "assets/plantrip/pepperpult/spr_seedpacket_pepperpult.png");
        SetPreviewImage(CompleteCollectionMod.PepperpultBundleId, "assets/plantrip/pepperpult/spr_seedpacket_pepperpult.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Pepperpult_Default",
            AssetBundleId = CompleteCollectionMod.PepperpultBundleId,
            SkinPrefabId = "assets/plantrip/pepperpult/models/default/plant/pepperpult.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<PepperpultBehaviorController>();
    }
}

// Two animation states: the Animator's default clip (idle) and "attack". The attack clip carries the PlaySound
// event (baked from the PAM) and an ExecuteAttack event on its launch frame (use_action on +13), so both the
// sound and the throw follow the game's animation speed - no timers in code.
[RegisterTypeInIl2Cpp]
public class PepperpultBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // The native lob (Plant.Fire for Cabbage-pult & co.) always flies for 120 ticks.
    private const float FlightTicks = 120f;

    // The pepper art is anchored at its fixed lowest point, 162 art pixels below the pepper's centre (from the
    // exported manifest: centre 989.7, anchor 1152), and one board pixel is 2.82 world units, so the anchor goes
    // 162 / 2.82 = 57 board pixels lower to put the pepper's centre where a native lob's is. Tune by eye in game.
    private const float PepperAnchorDrop = 57f;

    #endregion

    #region Constructors

    public PepperpultBehaviorController(IntPtr pointer) : base(pointer)
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

        var projectile = SpawnProjectile(PepperpultProjectileDefinition.PepperpultProjectileType);
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

        // Lower the art's anchor AFTER the throw is worked out, so the arc still ends where it would, and the
        // pepper (which sits above its anchor) ends on the zombie instead of high above it.
        projectile.mPosY += PepperAnchorDrop;
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

// PvZ2 PepperpultProjectile: BaseDamage 50, SplashDamage 10 (the PvZ2 pepper also sets the zombie on fire and
// chars it to ash; Replanted has no fire damage type, so this port only has the direct hit and the splash). The
// PvZ2 data has no impact sound for it, so the hit is silent.
[RegisterTypeInIl2Cpp]
public class PepperpultProjectileDefinition : CustomProjectileDefinition
{
    #region Variables

    public static ProjectileType PepperpultProjectileType;

    public const int SplashDamage = 10;

    // Instantiated per impact by PepperpultProjectileBehaviorController.
    public GameObject HitEffectPrefab;

    #endregion

    #region Constructors

    public PepperpultProjectileDefinition(IntPtr pointer) : base(pointer)
    {
        PepperpultProjectileType = m_projectileType;

        m_damage = 50;
        m_motionType = ProjectileMotion.Lobbed;
        SetDamageRangeFlags(DamageRangeFlags.Ground);

        RegistryBridge.LoadAssetFromAssetBundle<GameObject>(
            CompleteCollectionMod.PepperpultBundleId,
            "assets/plantrip/pepperpult/models/default/projectile_hit/projectile_hit.prefab",
            prefab => HitEffectPrefab = prefab);

        RegisterSkin(new SpriteRendererProjectileSkin()
        {
            skinId = "PepperpultProjectile_Default",
            AssetBundleId = CompleteCollectionMod.PepperpultBundleId,
            SkinPrefabId = "assets/plantrip/pepperpult/models/default/projectile/projectile.prefab",
        });
    }

    #endregion

    #region Methods

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<PepperpultProjectileBehaviorController>();
    }

    #endregion
}

[RegisterTypeInIl2Cpp]
public class PepperpultProjectileBehaviorController : CustomProjectileBehaviorController
{
    #region Constructors

    public PepperpultProjectileBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    // The engine only gives splash to the melon/fireball types, so a custom type does it here (same as Dusk
    // Lobber's bulb): the zombie the lob lands on takes the full damage, every other zombie the native
    // IsZombieHitBySplash accepts (adjacent rows, overlapping the blast) takes the splash damage.
    public override bool PreDoImpact(Zombie theZombie)
    {
        var customDef = ProjectileDefinition.TryCast<PepperpultProjectileDefinition>();

        if (theZombie != null)
        {
            DamageZombie(theZombie);
            SpawnHitEffect(customDef, theZombie);
        }

        foreach (var entry in ZombieSnapshot.Items(Board))
        {
            var zombie = entry.mItem;
            if (zombie == null || zombie == theZombie || zombie.IsDeadOrDying()
                || !zombie.EffectedByDamage(DamageRangeFlags.Ground)
                || !Projectile.IsZombieHitBySplash(zombie))
            {
                continue;
            }

            zombie.TakeDamage(PepperpultProjectileDefinition.SplashDamage, customDef.m_damageFlags);
        }

        // Returning false skips the native DoImpact, which is what normally ends the projectile.
        Projectile.Die();
        return false;
    }

    private static void SpawnHitEffect(PepperpultProjectileDefinition customDef, Zombie theZombie)
    {
        if (customDef?.HitEffectPrefab == null || theZombie.mController == null)
        {
            return;
        }

                // The splat clip is 38 frames = 1.27 s at 30 fps; keep it alive long enough to play out.
        ZombieClipEffect.SpawnHit(theZombie, customDef.HitEffectPrefab, 1.5f);
    }

    #endregion
}
