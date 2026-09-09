using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Projectile;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Content.Projectiles;
using UnityEngine;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection2.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class TumbleweedDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public TumbleweedDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Tumbleweed", false);
        SetDescription("A one-way ticket to Bounce Town. Tumbleweed doesn't wait around - the moment he's planted, he's already rolling.", false);
        SetTooltip("Tumbleweed instantly launches himself down the lane, bouncing diagonally between zombies he hits.", false);

        // Estimated - couldn't find a PlantProperties entry for Tumbleweed in
        // the ripped RESOURCES.json (unlike FirePeashooter's), so this is a
        // guess at a fitting cheap/fast one-shot tier, not a verified real value.
        m_seedCost = 25;
        m_versusCost = 25;

        m_refreshTime = 500;
        m_versusBaseRefreshTime = 500;
        m_versusSuddenDeathRefreshTime = 125;

        SetSeedPacketImage(CompleteCollectionMod.TumbleweedBundleId, "assets/plantrip/tumbleweed/spr_seedpacket_tumbleweed.png");
        SetPreviewImage(CompleteCollectionMod.TumbleweedBundleId, "assets/plantrip/tumbleweed/spr_seedpacket_tumbleweed.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Tumbleweed_Default",
            AssetBundleId = CompleteCollectionMod.TumbleweedBundleId,
            SkinPrefabId = "assets/plantrip/tumbleweed/models/default/plant/tumbleweed.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<TumbleweedBehaviorController>();
    }
}

[RegisterTypeInIl2Cpp]
public class TumbleweedBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    private bool hasFired;

    #endregion

    #region Constructors

    public TumbleweedBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    // Tumbleweed doesn't wait for a launch cycle like a normal shooter - it
    // fires itself down the lane and dies on its very first Update tick after
    // being placed. Firing from PrePlantUpdate (not PostInitialize) so the
    // plant has already been fully registered onto the board for at least one
    // frame before Die() runs - safer than tearing it down mid-initialization.
    // Returning false on the firing frame skips native Plant.Update() for this
    // now-dying plant entirely rather than letting it run against an object
    // we just told to die.
    public override bool PrePlantUpdate()
    {
        var shouldContinue = base.PrePlantUpdate();

        if (!hasFired)
        {
            hasFired = true;

            PlayAnimation("idle2_1");
            SpawnProjectile(TumbleweedProjectileDefinition.TumbleweedProjectileType);
            Plant.Die();

            return false;
        }

        return shouldContinue;
    }

    public override void Reset()
    {
        base.Reset();

        hasFired = false;
    }

    #endregion
}

[RegisterTypeInIl2Cpp]
public class TumbleweedProjectileDefinition : CustomProjectileDefinition
{
    #region Variables

    public static ProjectileType TumbleweedProjectileType;

    #endregion

    #region Constructors

    public TumbleweedProjectileDefinition(IntPtr pointer) : base(pointer)
    {
        TumbleweedProjectileType = m_projectileType;

        m_damage = 20;
        m_motionType = ProjectileMotion.Straight;
        SetDamageRangeFlags(DamageRangeFlags.Ground);

        RegisterSkin(new SpriteRendererProjectileSkin()
        {
            skinId = "TumbleweedProjectile_Default",
            AssetBundleId = CompleteCollectionMod.TumbleweedBundleId,
            SkinPrefabId = "assets/plantrip/tumbleweed/models/default/projectile/projectile.prefab",
        });
    }

    #endregion

    #region Methods

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<TumbleweedProjectileBehaviorController>();
    }

    #endregion
}

[RegisterTypeInIl2Cpp]
public class TumbleweedProjectileBehaviorController : CustomProjectileBehaviorController
{
    #region Variables

    // How many world-Y units separate two adjacent rows. There's no exposed
    // row-to-Y lookup we found, so this is an eyeballed guess to tune in-game -
    // too small and the diagonal bounce looks flat, too big and it overshoots
    // past the next row before the damage-dealing part of the hop is done.
    private const float RowHeight = 100f;

    // How many ticks the diagonal hop takes to cross one row. Keeps the X
    // travel (handled entirely by native Straight motion, untouched here)
    // and Y travel moving over the same window so it reads as one 45-degree
    // hop rather than a snap.
    private const int BounceTicks = 20;

    private bool isBouncing;
    private int bounceDirection;
    private int bounceTicksRemaining;
    private int bounceTargetRow;

    // Suppresses re-triggering PreDoImpact every tick the projectile is still
    // overlapping the zombie it just hit - a straight native pea never needs
    // this since it dies on its first hit, but ours deliberately doesn't.
    private Zombie lastHitZombie;
    private int hitCooldownTicks;

    #endregion

    #region Constructors

    public TumbleweedProjectileBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override void Reset()
    {
        base.Reset();

        isBouncing = false;
        bounceDirection = 0;
        bounceTicksRemaining = 0;
        bounceTargetRow = -1;
        lastHitZombie = null;
        hitCooldownTicks = 0;
    }

    public override bool PreDoImpact(Zombie theZombie)
    {
        if (hitCooldownTicks > 0 && theZombie == lastHitZombie)
        {
            // Still overlapping the zombie we just bounced off of - let it
            // pass through untouched instead of re-triggering another bounce.
            return false;
        }

        DamageZombie(theZombie);

        lastHitZombie = theZombie;
        hitCooldownTicks = BounceTicks;

        int rowCount = Board.mPlantRow.Length;
        int currentRow = Projectile.mRow;

        // Random up/down, clamped at the top/bottom rows so it can't bounce
        // off the edge of the lawn - matches the classic bowling-ball rule of
        // just reflecting back the only direction still available.
        int direction = UnityEngine.Random.Range(0, 2) == 0 ? -1 : 1;
        if (currentRow + direction < 0) direction = 1;
        if (currentRow + direction >= rowCount) direction = -1;

        isBouncing = true;
        bounceDirection = direction;
        bounceTicksRemaining = BounceTicks;
        bounceTargetRow = currentRow + direction;

        return false;
    }

    public override void PostUpdateNormalMotion()
    {
        base.PostUpdateNormalMotion();

        if (hitCooldownTicks > 0)
        {
            hitCooldownTicks--;
        }

        if (!isBouncing)
        {
            return;
        }

        Projectile.mPosY += bounceDirection * (RowHeight / BounceTicks);
        bounceTicksRemaining--;

        if (bounceTicksRemaining <= 0)
        {
            Projectile.mRow = bounceTargetRow;
            isBouncing = false;
        }
    }

    #endregion
}
