using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using UnityEngine;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection2.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class GoldBloomDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public GoldBloomDefinition(IntPtr pointer) : base(pointer)
    {
        // Like Enlighten-Mint (the other instant sun plant): no launch counter, the controller
        // drives everything from its own Update.
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Gold Bloom", false);
        SetDescription("Gold Blooms produce a burst of sun all at once, then are gone.", false);
        SetTooltip("Gold Bloom gives an instant burst of sun.", false);

        // PvZ2 GoldbloomDefault: Cost 0, Hitpoints 100, PacketCooldown 75, IsInstant/IsConsumable,
        // one "sun" Action. PacketCooldown 75 -> m_refreshTime 7500 (~100 ticks/sec; same mapping
        // Enlighten-Mint uses, 85 -> 8500).
        m_seedCost = 0;
        m_versusCost = 0;
        m_health = 100;

        m_refreshTime = 7500;
        m_versusBaseRefreshTime = 7500;
        m_versusSuddenDeathRefreshTime = 7500;

        // PvZ2 lists the Enlighten-mint family, but the plant is gone within about two seconds, so
        // there is no point attaching the mint buff controller to it (m_mintFamily stays None).

        SetSeedPacketImage(CompleteCollectionMod.GoldBloomBundleId, "assets/plantrip/goldbloom/spr_seedpacket_goldbloom.png");
        SetPreviewImage(CompleteCollectionMod.GoldBloomBundleId, "assets/plantrip/goldbloom/spr_seedpacket_goldbloom.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "GoldBloom_Default",
            AssetBundleId = CompleteCollectionMod.GoldBloomBundleId,
            SkinPrefabId = "assets/plantrip/goldbloom/models/default/plant/goldbloom.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the burst sounds are PlaySound Animation
        // Events baked into the "attack" clip from the PAM (see PvZRipTools plant_sounds.py).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<GoldBloomBehaviorController>();
    }
}

// The "attack" clip (30 fps, 72 frames) has three PAM use_action frames (+20, +29, +38); the bundle
// build bakes an ExecuteAttack event on each, so the sun comes out in three pulses in time with
// the animation, plus an OwnerDie event near the clip's end that removes the plant
// (AnimationScripts.OwnerDie -> Plant.Die). The controller only has to start the clip.
[RegisterTypeInIl2Cpp]
public class GoldBloomBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // PvZ2 Gold Bloom: SunProducer125, against 50 for PvZ2's Sunflower (2.5x). Replanted's
    // Sunflower makes one 25-sun coin, so 2.5x is 62.5: normal + normal + small = 65, one coin per pulse.
    private static readonly CoinType[] PulseCoins = { CoinType.Sun, CoinType.Sun, CoinType.SmallSun };

    // Let the skin finish applying before asking it to play a clip.
    private const float StartDelaySeconds = 0.25f;

    // Safety net only: if the OwnerDie event never fires, remove the plant anyway. The real
    // clip is about 2.4 s at normal speed.
    private const float GiveUpSeconds = 12f;

    private float elapsed;
    private bool started;
    private int pulses;

    #endregion

    #region Constructors

    public GoldBloomBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override void Update()
    {
        base.Update();

        elapsed += Time.deltaTime;

        if (!started && elapsed >= StartDelaySeconds)
        {
            started = true;
            PlayAnimation("attack");
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

        if (pulses >= PulseCoins.Length)
        {
            return;
        }

        Board.AddCoin(Plant.mX, Plant.mY, PulseCoins[pulses], CoinMotion.FromPlant);
        pulses++;
    }

    public override void Reset()
    {
        base.Reset();

        elapsed = 0f;
        started = false;
        pulses = 0;
    }

    #endregion
}
