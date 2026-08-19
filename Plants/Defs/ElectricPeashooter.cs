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
public class ElectricPeashooterDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public ElectricPeashooterDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        // Flies straight along the lane, which the roof's slope stops: not for a roof level's conveyor belt (see m_notOnRoof).
        m_notOnRoof = true;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Electric Peashooter", false);
        SetDescription("Electric Peashooter spits a crackling electro ball that passes through every zombie in the lane and keeps zapping the zombies around it.", false);
        SetTooltip("Shoots a piercing electro ball that arcs lightning to nearby zombies.", false);

        // PvZ2 ElectricPeashooterDefault: Cost 200, Hitpoints 300, PacketCooldown 5, a shot every 2.25-2.4 s.
        // PacketCooldown 5 -> m_refreshTime 500 (~100 ticks/sec, same mapping as every other port here); 2.25-2.4 s -> 230.
        m_seedCost = 200;
        m_versusCost = 200;
        m_health = 300;

        m_refreshTime = 500;
        m_versusBaseRefreshTime = 500;
        m_versusSuddenDeathRefreshTime = 125;

        m_launchRate = 230;

        m_mintFamily = MintFamily.FilaMint;

        SetSeedPacketImage(CompleteCollectionMod.ElectricPeashooterBundleId, "assets/plantrip/electric_peashooter/spr_seedpacket_electric_peashooter.png");
        SetPreviewImage(CompleteCollectionMod.ElectricPeashooterBundleId, "assets/plantrip/electric_peashooter/spr_seedpacket_electric_peashooter.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "ElectricPeashooter_Default",
            AssetBundleId = CompleteCollectionMod.ElectricPeashooterBundleId,
            SkinPrefabId = "assets/plantrip/electric_peashooter/models/default/plant/electric_peashooter.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the charge-up and shot sounds are PlaySound Animation Events
        // baked into the "attack" clip from the PAM.

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<ElectricPeashooterBehaviorController>();
    }
}

// Two animation states: the Animator default (idle) and "attack" (71 frames at 30 fps). The PAM marks the launch
// frame (use_action on +38); the bundle build bakes an ExecuteAttack event there, so the ball leaves on the clip's
// own keyframe and follows the game speed, no timers in code.
[RegisterTypeInIl2Cpp]
public class ElectricPeashooterBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // Same nudge FirePeashooter / Peapod / Primal Peashooter use (identical PvZ2 spawn offset, 35/-32). Tune by eye in game.
    private const float MouthHeightOffset = 23f;

    // The electro ball art is anchored at its fixed lowest point (the ball's tail hangs below it), about 150 art pixels
    // below the ball's centre (from the exported manifest: ball top 974, ball ~88 px across so centre ~1018, anchor 1168),
    // and one board pixel is 2.82 world units, so the anchor goes 150 / 2.82 = 53 board pixels lower to put the ball's
    // centre where a native pea's is. Tune by eye in game.
    public const float BallAnchorDrop = 53f;

    #endregion

    #region Constructors

    public ElectricPeashooterBehaviorController(IntPtr pointer) : base(pointer)
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

        var projectile = SpawnProjectile(ElectricPeaProjectileDefinition.ElectricPeaProjectileType);
        projectile.mPosY += MouthHeightOffset + BallAnchorDrop;
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

// PvZ2 ElectricPeaDefault: BaseDamage 5, lightning + shooter damage flags, speed 333 (a pea's), DiesOnImpact false
// (it pierces), art ELECTRIC_PEASHOOTER_ELECTROBALL. The plant's BoltDamage 20 / BoltRange 2 / BoltFrequency 0.5 s /
// BoltCount 2 are done by the ball itself: while it flies, every half second it zaps the two nearest zombies
// within two tiles of it, in any lane.
[RegisterTypeInIl2Cpp]
public class ElectricPeaProjectileDefinition : CustomProjectileDefinition
{
    #region Variables

    public static ProjectileType ElectricPeaProjectileType;

    // Instantiated per zap by ElectricPeaBehaviorController.
    public GameObject HitEffectPrefab;

    #endregion

    #region Constructors

    public ElectricPeaProjectileDefinition(IntPtr pointer) : base(pointer)
    {
        ElectricPeaProjectileType = m_projectileType;

        // PvZ2: the ball itself only does 5; the bolts (20 each) are where its damage is.
        m_damage = 5;
        m_motionType = ProjectileMotion.Straight;
        SetDamageRangeFlags(DamageRangeFlags.Ground);

        RegistryBridge.LoadAssetFromAssetBundle<GameObject>(
            CompleteCollectionMod.ElectricPeashooterBundleId,
            "assets/plantrip/electric_peashooter/models/default/projectile_hit/projectile_hit.prefab",
            prefab => HitEffectPrefab = prefab);

        RegisterSkin(new SpriteRendererProjectileSkin()
        {
            skinId = "ElectricPea_Default",
            AssetBundleId = CompleteCollectionMod.ElectricPeashooterBundleId,
            SkinPrefabId = "assets/plantrip/electric_peashooter/models/default/projectile/projectile.prefab",
        });
    }

    #endregion

    #region Methods

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<ElectricPeaBehaviorController>();
    }

    #endregion
}

[RegisterTypeInIl2Cpp]
public class ElectricPeaBehaviorController : CustomProjectileBehaviorController
{
    #region Variables

    // PvZ2 BoltDamage 20, BoltRange 2 (grid squares), BoltFrequency 0.5 s (~50 ticks at ~100 ticks/sec), BoltCount 2.
    private const int BoltDamage = 20;
    private const float BoltRangeTiles = 2f;
    private const int BoltIntervalTicks = 50;
    private const int BoltCount = 2;

    // Size of the contact burst relative to the clip's native size. Tune by eye in game.
    private const float ContactEffectScale = 0.6f;

    // Replanted's grid is 80 board pixels wide.
    private const float TileWidth = 80f;

    // The ball goes through everything: each zombie is hit by it once.
    private readonly List<Zombie> hitZombies = new List<Zombie>();
    private int ticksSinceBolt;

    #endregion

    #region Constructors

    public ElectricPeaBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    // Let the native motion run, and count ticks for the bolts.
    public override bool PreUpdateNormalMotion()
    {
        base.PreUpdateNormalMotion();

        ticksSinceBolt++;
        if (ticksSinceBolt >= BoltIntervalTicks)
        {
            ticksSinceBolt = 0;
            ZapNearbyZombies();
        }

        return true;
    }

    // The ball keeps flying: no Die(), and a zombie it already hit is ignored while it overlaps it.
    public override bool PreDoImpact(Zombie theZombie)
    {
        base.PreDoImpact(theZombie);

        if (theZombie == null || hitZombies.Contains(theZombie))
        {
            return false;
        }

        hitZombies.Add(theZombie);
        DamageZombie(theZombie);
        SpawnContactEffect(ProjectileDefinition.TryCast<ElectricPeaProjectileDefinition>());

        return false;
    }

    private void ZapNearbyZombies()
    {
        if (Board?.m_zombies?.m_list == null)
        {
            return;
        }

        var customDef = ProjectileDefinition.TryCast<ElectricPeaProjectileDefinition>();

        var targets = ZombieSnapshot.Items(Board)
            .Select(z => z.mItem)
            .Where(z => z != null
                        && !z.IsDeadOrDying()
                        && z.EffectedByDamage(DamageRangeFlags.Ground))
            .Select(z => new
            {
                Zombie = z,
                Distance = Mathf.Sqrt(Mathf.Pow((z.mPosX - Projectile.mPosX) / TileWidth, 2f)
                                      + Mathf.Pow(z.mRow - Projectile.mRow, 2f))
            })
            .Where(e => e.Distance <= BoltRangeTiles)
            .OrderBy(e => e.Distance)
            .Take(BoltCount)
            .ToList();

        // The bolt is drawn from the ball to the zombie (ZombieClipEffect): the ball's centre is where its art is
        // actually drawn.
        Vector3 ballCenter = Vector3.zero;
        bool haveBall = Projectile?.mController != null
                        && ZombieClipEffect.TryGetDrawnCenter(Projectile.mController.gameObject, out ballCenter);

        foreach (var entry in targets)
        {
            entry.Zombie.TakeDamage(BoltDamage, customDef.m_damageFlags);
            if (customDef?.HitEffectPrefab != null)
            {
                ZombieClipEffect.Spawn(entry.Zombie, customDef.HitEffectPrefab, "animation", 1f, haveBall ? ballCenter : null);
            }
        }
    }

    // The ball's own 5-damage contact gets a small burst at the ball (the hit prefab's "animation3" clip, scaled
    // down), deliberately not the zigzag bolt the 20-damage zaps use, so the two can be told apart at a glance.
    private void SpawnContactEffect(ElectricPeaProjectileDefinition customDef)
    {
        if (customDef?.HitEffectPrefab == null || Projectile?.mController == null)
        {
            return;
        }

        // The clip is 36 frames = 1.2 s at 30 fps.
        ZombieClipEffect.SpawnAtWorld(customDef.HitEffectPrefab, Projectile.mController.gameObject.transform.position, 1.3f, "animation3", ContactEffectScale);
    }

    public override void Reset()
    {
        base.Reset();

        hitZombies.Clear();
        ticksSinceBolt = 0;
    }

    #endregion
}
