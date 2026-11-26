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

namespace PvZCompleteCollection.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class ColdSnapdragonDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    // PvZ2's COLD_SNAPDRAGON_ICE (the ice crystals one breath leaves on each tile it hits), ripped into the plant's
    // own bundle as an animated prefab (PvZRipTools plants.json "effects"). Stays null on an older bundle (no ice
    // drawn, the damage and the chill still happen).
    public static GameObject IcePrefab;
    private const string IcePrefabPath = "assets/plantrip/cold_snapdragon/models/default/effects/ice/ice.prefab";

    public ColdSnapdragonDefinition(IntPtr pointer) : base(pointer)
    {
        // Same shape as Snapdragon: not a Shooter (the game's shooter update never looks at zombies and the plant
        // fires no projectile); the controller keeps its own breath timer.
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Cold Snapdragon", false);
        SetDescription("Cold Snapdragon breathes a short burst of frost over the two tiles in front of him, across his own lane and the ones next to it. Everything that stands in it is hurt and chilled, and walks at half speed for ten seconds.", false);
        SetTooltip("Breathes frost over the 2 tiles in front of him, in 3 lanes: damages and chills.", false);

        // PvZ2 ColdSnapdragonDefault: Cost 150, Hitpoints 300, PacketCooldown 5, a breath every 1.85-2 s.
        // PacketCooldown 5 -> m_refreshTime 500 (~100 ticks/sec, same mapping as every other port here).
        m_seedCost = 150;
        m_versusCost = 150;
        m_health = 300;

        m_refreshTime = 500;
        m_versusBaseRefreshTime = 500;
        m_versusSuddenDeathRefreshTime = 125;

        // PvZ2 family: Winter-mint (the frost plants).
        m_mintFamily = MintFamily.WinterMint;

        SetSeedPacketImage(CompleteCollectionMod.ColdSnapdragonBundleId, "assets/plantrip/cold_snapdragon/spr_seedpacket_cold_snapdragon.png");
        SetPreviewImage(CompleteCollectionMod.ColdSnapdragonBundleId, "assets/plantrip/cold_snapdragon/spr_seedpacket_cold_snapdragon.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "ColdSnapdragon_Default",
            AssetBundleId = CompleteCollectionMod.ColdSnapdragonBundleId,
            SkinPrefabId = "assets/plantrip/cold_snapdragon/models/default/plant/cold_snapdragon.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the breath sound is a PlaySound Animation Event baked into the
        // "attack" clip (PvZBundleBuilder.HandBakedSounds - the PAM's own sound event resolves to nothing, the choice
        // of variant is a guess).

        LoadIcePrefab();

        PlantSeedType = m_seedType;
    }

    private static void LoadIcePrefab()
    {
        try
        {
            RegistryBridge.LoadAssetFromAssetBundle<GameObject>(
                CompleteCollectionMod.ColdSnapdragonBundleId, IcePrefabPath, prefab => IcePrefab = prefab);
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"[ColdSnapdragon] ice prefab not loaded, the breath leaves no ice art: {e.Message}");
        }
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<ColdSnapdragonBehaviorController>();
    }
}

// Same mechanics as Snapdragon (see there): two animation states, the Animator default (idle) and "attack" (42 frames at
// 30 fps). The PAM marks the breath frame (use_action on +11); the bundle build bakes an ExecuteAttack event there plus the
// breath sound (PlaySound), so both follow the game speed. The ice is a separate prefab, one copy per tile hit.
//
// PvZ2's data (POWERS: ColdSnapdragonNormalAttackLvl1, a "LinearBurst"): the same damage area as Snapdragon's, 165 px long
// and 220 px tall from the plant (two tiles, three lanes), BaseDamage 30 and a 10 second "chill" condition instead of
// fire; the burst hits tile by tile, TimeBetweenGridHits 0.2 s apart, and the ice art (a "square effect") is shown on each
// square it covers (GridAreaForAnimation x 1..2 in front, 3 lanes).
[RegisterTypeInIl2Cpp]
public class ColdSnapdragonBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // PvZ2: 165 board px = two tiles of 82.5. Replanted's lawn column is 80 wide.
    private const float TileWidth = 80f;
    private const int TilesDeep = 2;

    // PvZ2 BaseDamage 30.
    private const int FrostDamage = 30;

    // TimeBetweenGridHits 0.2 s at ~100 ticks a second.
    private const int SecondTileDelayTicks = 20;

    // The frost zone: from the middle of the plant to two tiles further on, across the plant's lane and the lane
    // either side. The breath starts whenever a zombie is in this zone (the same test as the damage uses).
    private const float ZoneLength = TilesDeep * TileWidth;

    // A breath every 1.85-2 s (PvZ2): the next one may start this many ticks after the last one began (the "attack"
    // clip is 42 frames = 140 ticks), checked every few ticks; the first one may start half a second after planting.
    private const int BreathCooldownTicks = 190;
    private const int FirstBreathTicks = 50;
    private const int CheckInterval = 5;

    // The ice art: its anchor sits (-8, +99) PAM pixels from the plant's anchor (worked out from the two manifest.json
    // files; see PlantEffectSpawner) when both are placed on the same tile. Its clip is 36 frames at the PAM's own 24 fps
    // = 1.5 s. The tile centre the art is drawn for is the plant's own x plus whole tiles.
    private const float IceDx = -8f;
    private const float IceDyDown = 99f;
    private const float IceLifetimeSeconds = 1.65f;

    private int ticksSinceBreath = BreathCooldownTicks - FirstBreathTicks;
    private int secondTileCountdown;
    private float damageLeft;
    private float damageRight;

    #endregion

    #region Constructors

    public ColdSnapdragonBehaviorController(IntPtr pointer) : base(pointer)
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
        FrostZone(centerX, centerX + TileWidth);
        SpawnIceArt(1);
        damageLeft = centerX + TileWidth;
        damageRight = centerX + TilesDeep * TileWidth;
        secondTileCountdown = SecondTileDelayTicks;
    }

    public override void PostPlantUpdate()
    {
        base.PostPlantUpdate();

        if (secondTileCountdown > 0 && --secondTileCountdown == 0)
        {
            FrostZone(damageLeft, damageRight);
            SpawnIceArt(2);
        }

        // Breathe as soon as there is a zombie in the frost zone and the cooldown is over (our own timer, like Snapdragon).
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

    // 30 damage and a 10 second chill (the game's own chill: the zombie walks and chews at half speed, Zombie.ApplyChill)
    // to every live ground zombie of the plant's lane and the lanes next to it whose body overlaps the strip [left, right].
    private void FrostZone(float left, float right)
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

            zombie.TakeDamage(FrostDamage, (DamageFlags)0);
            if (!zombie.IsDeadOrDying())
            {
                zombie.ApplyChill(false);
            }
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

    // Board pixels -> world units: Constants.BOARD_SCALE_AMOUNT (the same value Snapdragon's flames and Chilly Pepper's ice use).
    private const float BoardScale = 2.82f;

    // One copy of the ice crystals (same art, never rescaled) on each tile the breath hits: the plant's lane and the
    // lane above and below when the board has them, the tile 'tilesAhead' columns in front of the plant. Each copy is
    // moved by that tile's distance from the plant's tile (the board's own row positions, so a 6-lane pool level and the
    // sloping roof are right) and sorts with its own lane (like Snapdragon's flames).
    private void SpawnIceArt(int tilesAhead)
    {
        var prefab = ColdSnapdragonDefinition.IcePrefab;
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

            // Y grows downward on the board and upward in the world, hence the minus; X is the tile ahead's distance.
            float laneShift = -(Board.GridToPixelY(column, row) - Board.GridToPixelY(column, plantRow)) * worldPerPixel;
            float tileShift = tilesAhead * TileWidth * worldPerPixel;
            int sortingOrder = Il2CppReloaded.Utils.SortingUtils.RemapSortingLayers(
                Board.MakeRenderOrder(RenderLayer.Particle, row, 1));

            PlantEffectSpawner.Spawn(prefab, anchor + new Vector3(tileShift, laneShift, 0f), unit,
                IceDx, IceDyDown, sortingOrder, IceLifetimeSeconds);
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
