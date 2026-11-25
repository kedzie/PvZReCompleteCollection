using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Util;
using UnityEngine;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class ChillyPepperDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    // PvZ2's CHILLYPEPPER_TILE_ICE (the ice patch on every tile of the blast lane), ripped into the plant's own
    // bundle as its own animated prefab (PvZRipTools plants.json "effects"). Loaded once here, instantiated per
    // tile by ChillyPepperBehaviorController. Stays null if the bundle is an older one without it (no ice, no error).
    public static GameObject TileIcePrefab;
    private const string TileIcePrefabPath = "assets/plantrip/chillypepper/models/default/effects/tile_ice/tile_ice.prefab";

    public ChillyPepperDefinition(IntPtr pointer) : base(pointer)
    {
        // Instant plant: no launch counter, the controller drives everything from its own Update
        // and from the Animation Event baked into its "attack" clip (same shape as Gold Bloom).
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Chilly Pepper", false);
        SetDescription("Chilly Pepper lets out a freezing blast down its whole lane, freezing every zombie in it and hurting the ones close by. The lanes next to it are only chilled.", false);
        SetTooltip("Freezes every zombie in its lane, chills the next lanes, and damages the nearby ones.", false);

        // PvZ2 ChillyPepperDefault: Cost 125, Hitpoints 300, PacketCooldown 35, IsInstant/IsConsumable.
        // Replanted's Jalapeno costs 125 as well. PacketCooldown 35 -> m_refreshTime 3500
        // (~100 ticks/sec, same mapping as every other port here).
        m_seedCost = 125;
        m_versusCost = 125;
        m_health = 300;

        m_refreshTime = 3500;
        m_versusBaseRefreshTime = 3500;
        m_versusSuddenDeathRefreshTime = 875;

        // PvZ2 lists the Wintermint family, but the plant is gone within about two seconds, so
        // there is nothing for a mint to buff (m_mintFamily stays None, like Gold Bloom).

        SetSeedPacketImage(CompleteCollectionMod.ChillyPepperBundleId, "assets/plantrip/chillypepper/spr_seedpacket_chillypepper.png");
        SetPreviewImage(CompleteCollectionMod.ChillyPepperBundleId, "assets/plantrip/chillypepper/spr_seedpacket_chillypepper.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "ChillyPepper_Default",
            AssetBundleId = CompleteCollectionMod.ChillyPepperBundleId,
            SkinPrefabId = "assets/plantrip/chillypepper/models/default/plant/chillypepper.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the blast sound is a PlaySound Animation Event
        // baked into the "attack" clip from the PAM (see PvZRipTools plant_sounds.py). The frost
        // burst ("CHILOOSH!") is part of that clip's art; the ice left on the lane tiles is the separate
        // tile_ice prefab loaded below.

        LoadTileIcePrefab();

        PlantSeedType = m_seedType;
    }

    private static void LoadTileIcePrefab()
    {
        try
        {
            RegistryBridge.LoadAssetFromAssetBundle<GameObject>(
                CompleteCollectionMod.ChillyPepperBundleId, TileIcePrefabPath, prefab => TileIcePrefab = prefab);
        }
        catch (Exception e)
        {
            // An older pvzchillypepperbundle has no tile_ice prefab: the plant still works, without the lane ice.
            MelonLogger.Warning($"[ChillyPepper] tile_ice prefab not loaded, the lane ice is skipped: {e.Message}");
        }
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<ChillyPepperBehaviorController>();
    }
}

// The "attack" clip (30 fps, 59 frames) has the PlaySound event on its first frame, one PAM
// use_action marker on frame +25 (the blast; the bundle build bakes an ExecuteAttack event there) and
// an OwnerDie event near its end that removes the plant (AnimationScripts.OwnerDie -> Plant.Die).
// The controller only has to start the clip and do the chilling (and spawn the lane ice) on the
// ExecuteAttack event.
[RegisterTypeInIl2Cpp]
public class ChillyPepperBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // Approximate width of one lawn column (the same guess Tumbleweed and ZombiePushBack use).
    private const float TileWidth = 80f;

    // PvZ2 ChillyPepperDefault, first Action ("radius_tiered_row"): ExplodeRadius 5.0736 (squared
    // squares, like every PvZ2 radius: 2.0736 = 1.44 tiles) = 2.25 tiles, Damage 200 in the plant's own
    // lane, SplashDamage 100 in the lanes next to it.
    private const float DamageRadius = 2.25f * TileWidth;
    private const int LaneDamage = 200;
    private const int AdjacentLaneDamage = 100;

    // The other two Actions ("row": Damage 0, FreezeDuration 1; "row_adjacent": ChillDuration 1 +
    // FreezeDuration 1) apply to the WHOLE lane and the two next to it, no radius. The plant's own lane is
    // frozen solid the way Replanted's Ice-shroom does it (Zombie.HitIceTrap: freeze for 4-6 s, then
    // chilled); the neighbouring lanes only get Replanted's chill (Zombie.ApplyChill(false): slow for
    // 10 s) - freezing three whole lanes would be a lot, and the PvZ2 numbers do not say how long
    // "1" is. DamageFlags.Freeze makes the radius damage chill too.
    private const DamageFlags ChillDamageFlags = DamageFlags.Freeze;

    // Zombies are only chilled once they are on the lawn: ignore anything further right than the last
    // column plus this slack (zombies spawn off-screen to the right).
    private const int LastColumnIndex = 8;
    private const float RightEdgeSlack = 120f;

    // ---- Lane ice: PvZ2's CHILLYPEPPER_TILE_ICE on every tile of the plant's lane ----
    // The PvZ2 data only ships the effect's PAM (20 frames at 30 fps, ice slab + rising frost); nothing in
    // PLANTPROPERTIES / the plant's PAM says how it is spawned, so the look below is a choice: one patch per
    // column, starting under the plant and travelling outward in both directions (a "whoosh" along the row),
    // in the plant's own lane only (the next lanes are chilled, not frozen - no ice there).
    // Board pixels -> Unity world units: Constants.BOARD_SCALE_AMOUNT (a tile is 80 x 100 board px).
    private const float BoardScale = 2.82f;

    // Delay between a column and the next one further from the plant: the farthest tile of the 9 gets its patch
    // 0.4 s after the blast (the zombies themselves freeze instantly).
    private const float IceStaggerSeconds = 0.05f;

    // Each patch is destroyed when its clip has played out: tile_ice is 20 PAM frames + 30 held + 15 fading out =
    // 65 frames = 2.17 s at 30 fps (plants.json "hold_frames"/"fade_frames"; the Animator keeps the invisible last
    // frame until then). Update this if those two numbers change.
    private const float IceLifetimeSeconds = 2.25f;

    // Drawn on the ground: RenderLayer.Ground is where the native grid items (craters, scorch marks) live,
    // below the plants (302000) and zombies (303000) of every row. +4 puts it just over those.
    private const int IceRenderOffset = 4;

    // The plant's controller sits at the tile's top edge (GridToPixelY is the top of the tile), and the prefab's
    // anchor is the fixed bottom-middle point of the lowest frame of the animation (the frost cloud's lowest
    // point). The ice slab's middle is 168 px above that anchor (slab 908..1104 in the PAM, anchor at 1174), so the
    // patch is dropped half a tile (50 board px) to the tile centre, and a further 168 so the slab, not the anchor,
    // is what ends up on the tile centre.
    private const float TileHalfHeightBoardPx = 50f;
    private const float IceSlabCentreAboveAnchor = 168f;

    private sealed class PendingIce
    {
        public float SpawnTime;
        public Vector3 Position;
    }

    private readonly List<PendingIce> pendingIce = new();
    private float iceElapsed;
    private int iceSortingOrder;

    // Let the skin finish applying before asking it to play a clip.
    private const float StartDelaySeconds = 0.1f;

    // Safety net only: if the OwnerDie event never fires, remove the plant anyway. The real
    // clip is about 2 s at normal speed.
    private const float GiveUpSeconds = 12f;

    private float elapsed;
    private bool started;
    private bool blasted;

    #endregion

    #region Constructors

    public ChillyPepperBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override void Update()
    {
        base.Update();

        elapsed += Time.deltaTime;

        if (!started && elapsed >= StartDelaySeconds && IsSkinApplied)
        {
            started = true;
            PlayAnimation("attack");
        }

        if (pendingIce.Count > 0)
        {
            SpawnDueIce();
        }

        if (elapsed >= GiveUpSeconds)
        {
            elapsed = float.MinValue;
            Plant.Die();
        }
    }

    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        if (blasted)
        {
            return;
        }

        blasted = true;
        ChillLanes();
        QueueLaneIce();
        BossBalls.PutOutFireBall(Board, Plant.mRow);
    }

    // One patch per column of the plant's lane, delayed by its distance from the plant. The world position of
    // each tile is the plant's own controller position plus the board-pixel difference between the two tiles
    // (GridToPixelY differs per column on the roof, so the row of patches follows the slope).
    private void QueueLaneIce()
    {
        if (ChillyPepperDefinition.TileIcePrefab == null || Plant.mController == null)
        {
            return;
        }

        var plantTransform = Plant.mController.gameObject.transform;
        float parentScale = plantTransform.parent != null ? plantTransform.parent.lossyScale.x : 1f;
        float worldPerPixel = BoardScale * parentScale;

        int row = Plant.mRow;
        int plantColumn = Plant.mPlantCol;
        int plantPixelX = Board.GridToPixelX(plantColumn, row);
        int plantPixelY = Board.GridToPixelY(plantColumn, row);
        iceSortingOrder = Il2CppReloaded.Utils.SortingUtils.RemapSortingLayers(
            Board.MakeRenderOrder(RenderLayer.Ground, row, IceRenderOffset));

        iceElapsed = 0f;
        for (int column = 0; column <= LastColumnIndex; column++)
        {
            float dx = (Board.GridToPixelX(column, row) - plantPixelX) * worldPerPixel;
            float dy = -(Board.GridToPixelY(column, row) - plantPixelY) * worldPerPixel
                       - TileHalfHeightBoardPx * worldPerPixel - IceSlabCentreAboveAnchor;
            pendingIce.Add(new PendingIce
            {
                SpawnTime = Math.Abs(column - plantColumn) * IceStaggerSeconds,
                Position = plantTransform.position + new Vector3(dx, dy, 0f)
            });
        }

        SpawnDueIce();
    }

    private void SpawnDueIce()
    {
        iceElapsed += Time.deltaTime;

        var prefab = ChillyPepperDefinition.TileIcePrefab;
        for (int i = pendingIce.Count - 1; i >= 0; i--)
        {
            var ice = pendingIce[i];
            if (ice.SpawnTime > iceElapsed)
            {
                continue;
            }

            pendingIce.RemoveAt(i);
            if (prefab == null)
            {
                continue;
            }

            // Same way the Dusk Lobber / Tumbleweed hit effects are spawned: a free-standing instance whose
            // Animator plays its default (only) clip by itself, destroyed once that clip is done.
            var instance = UnityEngine.Object.Instantiate(prefab, ice.Position, Quaternion.identity);
            foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.sortingOrder = iceSortingOrder;
            }

            UnityEngine.Object.Destroy(instance, IceLifetimeSeconds);
        }
    }

    private void ChillLanes()
    {
        var plantRect = Plant.GetPlantRect();
        float centerX = plantRect.x + plantRect.width / 2f;

        foreach (var entry in Board.m_zombies.m_list.ToList())
        {
            var zombie = entry.mItem;
            if (zombie == null
                || zombie.IsDeadOrDying()
                || zombie.mMindControlled
                || zombie.mZombieType == ZombieType.Gravestone)
            {
                continue;
            }

            int laneDistance = Math.Abs(zombie.mRow - Plant.mRow);
            if (laneDistance > 1 || zombie.mPosX > Board.GridToPixelX(LastColumnIndex, zombie.mRow) + RightEdgeSlack)
            {
                continue;
            }

            // The whole lane is frozen, the next two lanes are chilled ...
            if (laneDistance == 0)
            {
                zombie.HitIceTrap();
            }
            else
            {
                zombie.ApplyChill(false);
            }

            // ... and the ones within the blast radius also take the damage.
            var zombieRect = zombie.GetZombieRect();
            float zombieCenterX = zombieRect.x + zombieRect.width / 2f;
            if (Mathf.Abs(zombieCenterX - centerX) <= DamageRadius && zombie.EffectedByDamage(DamageRangeFlags.Ground))
            {
                zombie.TakeDamage(laneDistance == 0 ? LaneDamage : AdjacentLaneDamage, ChillDamageFlags);
            }
        }
    }

    public override void Reset()
    {
        base.Reset();

        elapsed = 0f;
        started = false;
        blasted = false;
        pendingIce.Clear();
        iceElapsed = 0f;
    }

    #endregion
}
