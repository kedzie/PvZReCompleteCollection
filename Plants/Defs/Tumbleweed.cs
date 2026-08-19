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
    private bool hasDied;
    private float elapsedSeconds;

    // Same class of bug as Celery Stalker's idle_down->idle fix: calling
    // PlayAnimation this early in a plant's life (right at spawn) doesn't
    // reliably take effect, and none of the completion-detection APIs we
    // tried could be trusted either -
    // AnimationController.IsAnimationPlaying reports "finished" almost
    // immediately regardless of real clip length, raw-Animator
    // IsCustomAnimationActive never became true for any instance, and
    // IsCustomAnimationFinished (normalizedTime) kept reporting "not
    // finished" for 3-4 seconds after visual playback had already ended.
    //
    // So attack2 (the vanish-into-light transition, launch sound baked onto
    // it by hand in the Editor) is now the plant Animator's own default
    // state instead - it's just showing from the instant the object exists,
    // no PlayAnimation call needed at all. All that's left here is a flat
    // real-time delay before firing, tuned purely by ear/eye in-game.
    private const float LaunchDelay = 0.28f;

    // Decoupled from LaunchDelay - the plant's actually invisible inside
    // attack2's bright light well before it fires, so this just lets that
    // light FX finish playing out before the plant object itself (and its
    // lawn tile) actually frees up, rather than yanking it away the instant
    // the projectile spawns. Kept short since a long-occupied tile blocks
    // replanting there.
    private const float DespawnDelay = 0.5f;

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
    // shows attack2 (its "vanishing into light, becoming the projectile"
    // transition, launch sound baked onto it in the Editor) as its Animator
    // default state from the moment it's planted. Firing and dying are on
    // their own independent fixed delays rather than happening together -
    // the projectile launches first (LaunchDelay), then the plant object
    // itself lingers a bit longer (DespawnDelay) so attack2's light FX gets
    // to finish before the tile frees up. Returning false the whole time
    // skips native Plant.Update() for this plant, which never needs to do
    // anything else in its short life anyway.
    public override bool PrePlantUpdate()
    {
        var shouldContinue = base.PrePlantUpdate();

        if (hasDied)
        {
            return shouldContinue;
        }

        elapsedSeconds += Time.deltaTime;

        if (!hasFired && elapsedSeconds >= LaunchDelay)
        {
            hasFired = true;

            var projectile = SpawnProjectile(TumbleweedProjectileDefinition.TumbleweedProjectileType);
            projectile.mPosY += LaunchYOffset;
            projectile.mVelX = ProjectileSpeed;
        }

        if (elapsedSeconds >= DespawnDelay)
        {
            hasDied = true;
            Plant.Die();
        }

        return false;
    }

    public override void Reset()
    {
        base.Reset();

        hasFired = false;
        hasDied = false;
        elapsedSeconds = 0f;
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
            ScaleOverride = new Vector3(1.3f, 1.3f, 1f),
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

    // Replaces the earlier per-hit TossChance roll (30%/15%/5%/0% chance to
    // bounce after the 1st/2nd/3rd/4th+ hit) - that let a packed row of 4+
    // zombies get fully mowed down with zero chance of ever bouncing away,
    // which felt far too strong. Now it ALWAYS bounces after resolving a
    // row. How many zombies that row-visit hits is its own layered roll -
    // the one that triggered the impact is always hit (100%), then 30%
    // chance of a 2nd, and if that lands, 15% chance of a 3rd - and any
    // extras beyond the first are picked from a generous forward sweep:
    // only zombies further along in the direction of travel (higher
    // mPosX) than the one that triggered the impact, closest first, within
    // SweepRange, never zombies already passed behind it.
    private const float SweepRange = 120f;

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
            // Already swept and resolved this row's hits (or still
            // mid-transition into the next row, mRow hasn't actually changed
            // yet) - without this, the projectile could keep colliding with
            // and hitting OTHER zombies still in the OLD row during that
            // window, on top of whatever the sweep already caught.
            return false;
        }

        // theZombie already passed the native engine's own targeting checks
        // to trigger this call at all, so it's hit unconditionally here
        // rather than being re-validated against FindZombiesToHit's own
        // filters (IsDeadOrDying/EffectedByDamage) - re-checking it caused a
        // real bug where a roll of 1+ could still resolve to 0 actual hits
        // if this exact zombie briefly failed one of those checks despite
        // the engine already having called PreDoImpact for it.
        DamageZombie(theZombie);
        ZombiePushBack.Push(theZombie, Board, TileWidth * 2f, ZombiePushTicks);
        SpawnHitEffect(theZombie);

        int extraHits = RollHitCount() - 1;
        if (extraHits > 0)
        {
            var extraZombies = FindZombiesToHit(theZombie.mRow, theZombie.mPosX, theZombie, extraHits);
            foreach (var zombie in extraZombies)
            {
                DamageZombie(zombie);
                ZombiePushBack.Push(zombie, Board, TileWidth * 2f, ZombiePushTicks);
                SpawnHitEffect(zombie);
            }
        }

        // Board.mPlantRow.Length isn't actually the row count (it didn't
        // clamp correctly in-game) - Board.GetNumRows() is the real,
        // authoritative source (also handles 5 vs 6-row stages).
        int rowCount = Board.GetNumRows();
        int currentRow = Projectile.mRow;

        // Fully random up/down every time, no bias away from the row it just
        // came from - real bowling doesn't seem to avoid re-visiting a lane,
        // so this doesn't either. Only clamped at the top/bottom rows so it
        // can't bounce off the edge of the lawn.
        int direction = UnityEngine.Random.Range(0, 2) == 0 ? -1 : 1;
        if (currentRow + direction < 0) direction = 1;
        if (currentRow + direction >= rowCount) direction = -1;

        isBouncing = true;
        bounceDirection = direction;
        bounceTicksRemaining = BounceTicks;
        bounceTargetRow = currentRow + direction;

        return false;
    }

    // Layered roll: the triggering zombie is always hit (100%), then 30%
    // chance of a 2nd, and only if that lands, 15% chance of a 3rd.
    private static int RollHitCount()
    {
        int count = 1;
        if (UnityEngine.Random.value < 0.30f)
        {
            count = 2;
            if (UnityEngine.Random.value < 0.15f)
            {
                count = 3;
            }
        }

        return count;
    }

    // Generous forward hitbox sweep, not a tight per-zombie collision check -
    // grabs whatever's clustered ahead of the zombie that triggered this
    // impact (closest first, same direction the projectile is traveling) so
    // a single row-visit can hit multiple zombies at once instead of
    // needing repeated native collisions to do it. Zombies already passed
    // (behind, lower mPosX) are excluded - the tumbleweed doesn't hit
    // backward. triggerZombie itself is excluded since PreDoImpact already
    // hits it directly - this only looks for the extras beyond it.
    private List<Zombie> FindZombiesToHit(int row, float centerX, Zombie triggerZombie, int maxHits)
    {
        return Board.m_zombies.m_list.ToList()
            .Where(z => z.mItem != null
                        && z.mItem != triggerZombie
                        && z.mItem.mRow == row
                        && !z.mItem.IsDeadOrDying()
                        && z.mItem.EffectedByDamage(DamageRangeFlags.Ground)
                        && z.mItem.mPosX >= centerX
                        && z.mItem.mPosX - centerX <= SweepRange)
            .OrderBy(z => z.mItem.mPosX)
            .Select(z => z.mItem)
            .Take(maxHits)
            .ToList();
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
