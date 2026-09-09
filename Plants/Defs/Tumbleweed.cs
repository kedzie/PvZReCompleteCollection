using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Behavior;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Projectile;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Content.Projectiles;
using PvZReCoreLib.Util;
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

    // Same story as FirePeashooter's MouthHeightOffset - SpawnProjectile()
    // spawns at Plant.mY with no vertical correction, and this engine's Y
    // increases downward on screen, so a positive offset here is what
    // actually moves the tumbleweed down toward ground level. First-pass
    // guess, tune by eye in-game.
    private const float LaunchYOffset = 60f;

    // Native Straight motion's default speed reads like it was tuned for a
    // small pea, not a rolling tumbleweed - overriding mVelX once at spawn
    // to something slower. First-pass guess.
    private const float ProjectileSpeed = 150f;

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
            var projectile = SpawnProjectile(TumbleweedProjectileDefinition.TumbleweedProjectileType);
            projectile.mPosY += LaunchYOffset;
            projectile.mVelX = ProjectileSpeed;
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

    // Loaded once here and instantiated fresh per hit by
    // TumbleweedProjectileBehaviorController.PreDoImpact - cheaper than
    // re-loading from the asset bundle on every impact.
    public GameObject HitEffectPrefab;

    #endregion

    #region Constructors

    public TumbleweedProjectileDefinition(IntPtr pointer) : base(pointer)
    {
        TumbleweedProjectileType = m_projectileType;

        m_damage = 20;
        m_motionType = ProjectileMotion.Straight;
        SetDamageRangeFlags(DamageRangeFlags.Ground);
        SetHitSfx(CompleteCollectionMod.TumbleweedBundleId, "assets/plantrip/tumbleweed/sounds/168329485.ogg");

        RegistryBridge.LoadAssetFromAssetBundle<GameObject>(
            CompleteCollectionMod.TumbleweedBundleId,
            "assets/plantrip/tumbleweed/models/default/projectile_hit/projectile_hit.prefab",
            prefab => HitEffectPrefab = prefab);

        RegisterSkin(new SpriteRendererProjectileSkin()
        {
            skinId = "TumbleweedProjectile_Default",
            AssetBundleId = CompleteCollectionMod.TumbleweedBundleId,
            SkinPrefabId = "assets/plantrip/tumbleweed/models/default/projectile/projectile.prefab",
            // Raw sprite pixel dimensions for the projectile's frames (~150px)
            // are already comparable to the plant's own (~124px) at 1x each,
            // so no real scale mismatch to compensate for - 1x looked a
            // touch small in-game, 1.5x is the current best guess.
            ScaleOverride = new Vector3(1.5f, 1.5f, 1f),
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

    // Approximate world-unit width of one lawn column - no clean tile-width
    // constant was found to derive this from (Board.mWidth exists but needs
    // verifying against column count for this board type first), so this is
    // a first-pass guess to tune in-game. Real PvZ2 Tumbleweed pushes the
    // zombie back 2 squares on every hit.
    private const float TileWidth = 80f;

    // Separate from BounceTicks (the projectile's own hop timing) - the
    // zombie's slide-back was riding BounceTicks before and came out too
    // fast, so it gets its own, longer duration to tune independently.
    private const int ZombiePushTicks = 40;

    private bool isBouncing;
    private int bounceDirection;
    private int bounceTicksRemaining;
    private int bounceTargetRow;

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
    }

    public override bool PreDoImpact(Zombie theZombie)
    {
        if (isBouncing)
        {
            // Still mid-transition into the next row (mRow hasn't actually
            // changed yet) - without this, the projectile could keep
            // colliding with and hitting OTHER zombies still in the OLD row
            // during that window, which is exactly how it was taking out an
            // entire row instead of one zombie before bouncing on.
            return false;
        }

        DamageZombie(theZombie);
        ZombiePushBack.Push(theZombie, TileWidth * 2f, ZombiePushTicks);
        SpawnHitEffect(theZombie);

        // Board.mPlantRow.Length isn't actually the row count (it didn't
        // clamp correctly in-game) - Board.GetNumRows() is the real,
        // authoritative source (also handles 5 vs 6-row stages).
        int rowCount = Board.GetNumRows();
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

    private void SpawnHitEffect(Zombie theZombie)
    {
        var customDef = ProjectileDefinition.TryCast<TumbleweedProjectileDefinition>();
        if (customDef?.HitEffectPrefab == null || theZombie.mController == null)
        {
            return;
        }

        var instance = UnityEngine.Object.Instantiate(customDef.HitEffectPrefab, theZombie.mController.gameObject.transform.position, Quaternion.identity);
        UnityEngine.Object.Destroy(instance, 1f);
    }

    #endregion
}
