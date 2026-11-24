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
public class AloeDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    // PvZ2's ALOE_HEAL_EFFECT (the droplet and the green sparkle on a healed plant), ripped into the plant's own
    // bundle as an animated prefab (PvZRipTools plants.json "effects"). Stays null on an older bundle (the heal
    // still happens, without the sparkle).
    public static GameObject HealPrefab;
    private const string HealPrefabPath = "assets/plantrip/aloe/models/default/effects/heal/heal.prefab";

    public AloeDefinition(IntPtr pointer) : base(pointer)
    {
        // Behaviour entirely from the controller (charge timer, hurt-plant check, heal).
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Aloe", false);
        SetDescription("Aloe soothes the plants around her. Whenever one of them is hurt, she heals it - then needs a few seconds to charge up again.", false);
        SetTooltip("Heals hurt plants in the 3x3 area around her, every 5 seconds.", false);

        // PvZ2 AloeDefault: Cost 75, Hitpoints 300, PacketCooldown 20. PacketCooldown 20 -> m_refreshTime 2000
        // (~100 ticks/sec, same mapping as every other port here).
        m_seedCost = 75;
        m_versusCost = 75;
        m_health = 300;

        m_refreshTime = 2000;
        m_versusBaseRefreshTime = 2000;
        m_versusSuddenDeathRefreshTime = 500;

        // PvZ2 family: Ail-mint.
        m_mintFamily = MintFamily.AilMint;

        SetSeedPacketImage(CompleteCollectionMod.AloeBundleId, "assets/plantrip/aloe/spr_seedpacket_aloe.png");
        SetPreviewImage(CompleteCollectionMod.AloeBundleId, "assets/plantrip/aloe/spr_seedpacket_aloe.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Aloe_Default",
            AssetBundleId = CompleteCollectionMod.AloeBundleId,
            SkinPrefabId = "assets/plantrip/aloe/models/default/plant/aloe.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: PvZ2's data has no sound for Aloe's heal (the events exist
        // but are empty), only for her Plant Food, which this mod does not have.

        LoadHealPrefab();

        PlantSeedType = m_seedType;
    }

    private static void LoadHealPrefab()
    {
        try
        {
            RegistryBridge.LoadAssetFromAssetBundle<GameObject>(
                CompleteCollectionMod.AloeBundleId, HealPrefabPath, prefab => HealPrefab = prefab);
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"[Aloe] heal prefab not loaded, healing has no sparkle: {e.Message}");
        }
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<AloeBehaviorController>();
    }
}

// Clips (30 fps): "idle" (the charged look, default), "attack" (38 frames: the heal; the PAM marks the heal frame
// with use_action on +13 and the bundle build bakes an ExecuteAttack event there), "idle_nocharge" (loops while
// she recharges), "charged" (45 frames: the "ready again" flourish, then back to idle).
//
// PvZ2 AloeDefault: HealCooldown 5 s, TargetHPTriggerPercent 0.8 (a plant is healed once it is at 80 % health or
// less), TargetHPHealAmount 500, "range: close" (taken as the 3x3 tiles around her). UnhealablePlants lists
// Explode-o-nut, Chili Bean, Hypno-shroom, Sun Bean, Stallia, Squash, Shadow-shroom, Stunion, Bamboo Spartan. The
// recharge is counted in game ticks, so it follows the game speed.
[RegisterTypeInIl2Cpp]
public class AloeBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    private const int HealCooldownTicks = 5 * 100;
    private const float HealTriggerFraction = 0.8f;
    private const int HealAmount = 500;
    private const int TileRadius = 1;

    // How often (ticks) a charged Aloe looks around for a hurt plant.
    private const int CheckInterval = 10;

    // Heal sparkle placement: the effect's anchor sits (-107, +120) PAM pixels from the plant's anchor (worked out
    // from the two manifest.json files; see PlantEffectSpawner), and it is spawned at the healed plant's own anchor
    // (reconstructed from Aloe's skin, see below). Its clip is 74 frames = 2.47 s at 30 fps.
    private const float HealDx = -107f;
    private const float HealDyDown = 120f;
    private const float HealLifetimeSeconds = 2.6f;

    private int ticks;
    private bool charged = true;
    private int rechargeTicksLeft;

    #endregion

    #region Constructors

    public AloeBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override void PostPlantUpdate()
    {
        base.PostPlantUpdate();

        if (!charged)
        {
            if (--rechargeTicksLeft <= 0)
            {
                charged = true;
                PlayAnimation("charged");
            }

            return;
        }

        ticks++;
        if (ticks % CheckInterval != 0 || !IsSkinApplied)
        {
            return;
        }

        if (FindHurtNeighbours().Count > 0)
        {
            charged = false;
            rechargeTicksLeft = HealCooldownTicks;
            PlayAnimation("attack");
        }
    }

    // ExecuteAttack event on the heal frame of the attack clip.
    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        var hurt = FindHurtNeighbours();
        if (hurt.Count == 0)
        {
            return;
        }

        bool haveAnchor = PlantEffectSpawner.TryGetAnchorOffset(Plant, out var anchorOffset, out float unit);
        var prefab = AloeDefinition.HealPrefab;

        foreach (var plant in hurt)
        {
            plant.mPlantHealth = Math.Min(plant.mPlantMaxHealth, plant.mPlantHealth + HealAmount);

            if (haveAnchor && prefab != null && plant.mController != null)
            {
                var anchor = plant.mController.gameObject.transform.position + anchorOffset;
                int sortingOrder = Il2CppReloaded.Utils.SortingUtils.RemapSortingLayers(
                    Board.MakeRenderOrder(RenderLayer.Particle, plant.mRow, 1));
                PlantEffectSpawner.Spawn(prefab, anchor, unit, HealDx, HealDyDown, sortingOrder, HealLifetimeSeconds);
            }
        }
    }

    // Every live, healable plant in the 3x3 tiles around Aloe (herself included) at or under 80 % health.
    private List<Plant> FindHurtNeighbours()
    {
        var result = new List<Plant>();
        if (Board?.m_plants?.m_list == null)
        {
            return result;
        }

        foreach (var item in Board.m_plants.m_list)
        {
            var plant = item.mItem;
            if (plant == null
                || plant.mDead
                || Math.Abs(plant.mPlantCol - Plant.mPlantCol) > TileRadius
                || Math.Abs(plant.mRow - Plant.mRow) > TileRadius
                || plant.mPlantMaxHealth <= 0
                || plant.mPlantHealth <= 0
                || plant.mPlantHealth >= plant.mPlantMaxHealth
                || plant.mPlantHealth > plant.mPlantMaxHealth * HealTriggerFraction
                || IsUnhealable(plant.mSeedType))
            {
                continue;
            }

            result.Add(plant);
        }

        return result;
    }

    // PvZ2's UnhealablePlants list, as far as this game has the plants.
    private static bool IsUnhealable(SeedType seedType)
    {
        return seedType == SeedType.ExplodeONut
               || seedType == SeedType.Hypnoshroom
               || seedType == SeedType.Squash
               || seedType == ExplodeONutDefinition.PlantSeedType
               || seedType == StalliaDefinition.PlantSeedType
               || seedType == BambooSpartanDefinition.PlantSeedType;
    }

    public override void Reset()
    {
        base.Reset();

        ticks = 0;
        charged = true;
        rechargeTicksLeft = 0;
    }

    #endregion
}
