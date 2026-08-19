using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Content.Plants.Mint;
using PvZReCoreLib.Util;
using UnityEngine;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection2.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class SnapdragonDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    // PvZ2's SNAPDRAGON_FIRE (the breath), ripped into the plant's own bundle as an animated prefab (PvZRipTools
    // plants.json "effects"). Stays null on an older bundle (no flames drawn, the damage still happens).
    public static GameObject FirePrefab;
    private const string FirePrefabPath = "assets/plantrip/snapdragon/models/default/effects/fire/fire.prefab";

    public SnapdragonDefinition(IntPtr pointer) : base(pointer)
    {
        // Not a Shooter: the game's own shooter update only runs a launch counter (it never looks at zombies at all),
        // and the plant fires no projectile; the controller keeps its own breath timer (see the controller).
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Snapdragon", false);
        SetDescription("Snapdragon breathes a short burst of fire over the two tiles in front of him, across his own lane and the ones next to it. Everything that stands in it burns.", false);
        SetTooltip("Breathes fire over the 2 tiles in front of him, in 3 lanes.", false);

        // PvZ2 SnapdragonDefault: Cost 150, Hitpoints 300, PacketCooldown 5, a breath every 1.85-2 s.
        // PacketCooldown 5 -> m_refreshTime 500 (~100 ticks/sec, same mapping as every other port here).
        m_seedCost = 150;
        m_versusCost = 150;
        m_health = 300;

        m_refreshTime = 500;
        m_versusBaseRefreshTime = 500;
        m_versusSuddenDeathRefreshTime = 125;

        // PvZ2 family: Pepper-mint (the fire plants).
        m_mintFamily = MintFamily.PepperMint;

        SetSeedPacketImage(CompleteCollectionMod.SnapdragonBundleId, "assets/plantrip/snapdragon/spr_seedpacket_snapdragon.png");
        SetPreviewImage(CompleteCollectionMod.SnapdragonBundleId, "assets/plantrip/snapdragon/spr_seedpacket_snapdragon.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Snapdragon_Default",
            AssetBundleId = CompleteCollectionMod.SnapdragonBundleId,
            SkinPrefabId = "assets/plantrip/snapdragon/models/default/plant/snapdragon.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the breath sound is a PlaySound Animation Event baked into the
        // "attack" clip (PvZBundleBuilder.HandBakedSounds - the PAM has no sound command for it).

        LoadFirePrefab();

        PlantSeedType = m_seedType;
    }

    private static void LoadFirePrefab()
    {
        try
        {
            RegistryBridge.LoadAssetFromAssetBundle<GameObject>(
                CompleteCollectionMod.SnapdragonBundleId, FirePrefabPath, prefab => FirePrefab = prefab);
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"[Snapdragon] fire prefab not loaded, the breath is invisible: {e.Message}");
        }
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<SnapdragonBehaviorController>();
    }
}

// (Targeting: the game's native Shooter update - Plant.UpdateShooter, read from the decompile - only counts
// mLaunchCounter down and calls FindTargetAndFire at zero; it never looks for zombies in any lane, so nothing
// gates the lanes above and below. This controller decides everything itself: a zombie in the flame zone of any
// of the three lanes starts the breath.)
//
// Two animation states: the Animator default (idle) and "attack" (41 frames at 30 fps). The PAM marks the
// breath frame (use_action on +19); the bundle build bakes an ExecuteAttack event there plus the breath sound
// (PlaySound), so both follow the game speed. The flames are a separate prefab (the PvZ2 fire effect).
//
// PvZ2's data (POWERS: SnapdragonNormalAttackLvl1, a "LinearBurst"): damage area 165 px long and 220 px tall
// from the plant, i.e. two tiles and three lanes; BaseDamage 30 with fire / ash-death flags; the burst hits tile
// by tile, TimeBetweenGridHits 0.2 s apart. It is done the same way: the near tile is hit on the event, the far tile
// 20 ticks later (ticks, so it follows the game speed).
[RegisterTypeInIl2Cpp]
public class SnapdragonBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // PvZ2: 165 board px = two tiles of 82.5. Replanted's lawn column is 80 wide.
    private const float TileWidth = 80f;
    private const int TilesDeep = 2;

    // PvZ2 BaseDamage 30.
    private const int FireDamage = 30;

    // TimeBetweenGridHits 0.2 s at ~100 ticks a second.
    private const int SecondTileDelayTicks = 20;

    // The flame zone: from the middle of the plant to two tiles further on, across the plant's lane and the lane
    // either side. The breath starts whenever a zombie is in this zone (the same test as the damage uses).
    private const float ZoneLength = TilesDeep * TileWidth;

    // A breath every 1.85-2 s (PvZ2): the next one may start this many ticks after the last one began (the
    // "attack" clip is 41 frames = 137 ticks), checked every few ticks; the first one may start half a second
    // after planting (PvZ2 InitialMinCooldownTime 0.5).
    private const int BreathCooldownTicks = 190;
    private const int FirstBreathTicks = 50;
    private const int CheckInterval = 5;

    // Flames: the effect's anchor sits (+45, +261) PAM pixels from the plant's anchor (worked out from the two
    // manifest.json files; see PlantEffectSpawner). Its clip is 25 frames at the PAM's own 24 fps = 1.04 s.
    private const float FireDx = 45f;
    private const float FireDyDown = 261f;
    private const float FireLifetimeSeconds = 1.15f;

    private int ticksSinceBreath = BreathCooldownTicks - FirstBreathTicks;
    private int secondTileCountdown;
    private float damageLeft;
    private float damageRight;

    #endregion

    #region Constructors

    public SnapdragonBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        var plantRect = Plant.GetPlantRect();
        float centerX = plantRect.x + plantRect.width / 2f;

        // Tile 1 now, tile 2 after the delay.
        BurnZone(centerX, centerX + TileWidth);
        damageLeft = centerX + TileWidth;
        damageRight = centerX + TilesDeep * TileWidth;
        secondTileCountdown = SecondTileDelayTicks;

        SpawnFireArt();
    }

    public override void PostPlantUpdate()
    {
        base.PostPlantUpdate();

        if (secondTileCountdown > 0 && --secondTileCountdown == 0)
        {
            BurnZone(damageLeft, damageRight);
        }

        // Breathe as soon as there is a zombie in the flame zone and the cooldown is over. (Our own timer instead of
        // the game's launch counter: that one only fires on a fixed rhythm, so a zombie that arrives just after a
        // beat would wait for the next one.)
        ticksSinceBreath++;
        if (ticksSinceBreath < BreathCooldownTicks || ticksSinceBreath % CheckInterval != 0 || !IsSkinApplied)
        {
            return;
        }

        if (AnyZombieInReach())
        {
            ticksSinceBreath = 0;
            PlayAnimation("attack");
        }
    }

    // 30 fire damage to every live ground zombie of the plant's lane and the lanes next to it whose body overlaps
    // the strip [left, right].
    private void BurnZone(float left, float right)
    {
        foreach (var entry in Board.m_zombies.m_list.ToList())
        {
            var zombie = entry.mItem;
            if (zombie == null
                || zombie.IsDeadOrDying()
                || zombie.mMindControlled
                || zombie.mZombieType == ZombieType.Gravestone
                || Math.Abs(zombie.mRow - Plant.mRow) > 1
                || !zombie.EffectedByDamage(DamageRangeFlags.Ground))
            {
                continue;
            }

            var zombieRect = zombie.GetZombieRect();
            if (zombieRect.x >= right || zombieRect.x + zombieRect.width <= left)
            {
                continue;
            }

            // Fire melts a chill, like the native fireball does.
            if (zombie.mChilledCounter > 0)
            {
                zombie.mChilledCounter = 0;
            }

            zombie.TakeDamage(FireDamage, (DamageFlags)0);
        }
    }

    private bool AnyZombieInReach()
    {
        if (Board?.m_zombies?.m_list == null)
        {
            return false;
        }

        var plantRect = Plant.GetPlantRect();
        float centerX = plantRect.x + plantRect.width / 2f;

        foreach (var entry in Board.m_zombies.m_list.ToList())
        {
            var zombie = entry.mItem;
            if (zombie == null
                || zombie.IsDeadOrDying()
                || zombie.mMindControlled
                || zombie.mZombieType == ZombieType.Gravestone
                || Math.Abs(zombie.mRow - Plant.mRow) > 1
                || !zombie.EffectedByDamage(DamageRangeFlags.Ground))
            {
                continue;
            }

            var zombieRect = zombie.GetZombieRect();
            if (zombieRect.x < centerX + ZoneLength && zombieRect.x + zombieRect.width > centerX)
            {
                return true;
            }
        }

        return false;
    }

    // Board pixels -> world units: Constants.BOARD_SCALE_AMOUNT (the same value Chilly Pepper's lane ice uses).
    private const float BoardScale = 2.82f;

    // The fire art is about two lanes tall and centred on the plant's own lane, but the breath damages three lanes,
    // so one copy of the effect (same art, never rescaled) is spawned per lane the damage covers: the plant's lane
    // and the lane above and below when the board has them. Each copy is moved by that lane's distance from the
    // plant's lane (the board's own row positions, so a 6-lane pool level and the sloping roof are right) and sorts
    // with its own lane (like Jalapeno's flames).
    private void SpawnFireArt()
    {
        var prefab = SnapdragonDefinition.FirePrefab;
        if (prefab == null || !PlantEffectSpawner.TryGetAnchor(Plant, out var anchor, out float unit))
        {
            return;
        }

        var plantTransform = Plant.mController.gameObject.transform;
        float parentScale = plantTransform.parent != null ? plantTransform.parent.lossyScale.x : 1f;
        float worldPerPixel = BoardScale * parentScale;

        int plantRow = Plant.mRow;
        int column = Plant.mPlantCol;
        int rows = Board.GetNumRows();

        for (int laneOffset = -1; laneOffset <= 1; laneOffset++)
        {
            int row = plantRow + laneOffset;
            if (row < 0 || row >= rows)
            {
                continue;
            }

            // Y grows downward on the board and upward in the world, hence the minus.
            float laneShift = -(Board.GridToPixelY(column, row) - Board.GridToPixelY(column, plantRow)) * worldPerPixel;
            int sortingOrder = Il2CppReloaded.Utils.SortingUtils.RemapSortingLayers(
                Board.MakeRenderOrder(RenderLayer.Particle, row, 1));

            PlantEffectSpawner.Spawn(prefab, anchor + new Vector3(0f, laneShift, 0f), unit,
                FireDx, FireDyDown, sortingOrder, FireLifetimeSeconds);
        }
    }

    public override void Reset()
    {
        base.Reset();

        secondTileCountdown = 0;
        ticksSinceBreath = BreathCooldownTicks - FirstBreathTicks;
    }

    #endregion
}
