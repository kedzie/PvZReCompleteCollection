using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using Il2CppSource.Utils;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Content.Plants.Mint;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection2.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class PrimalSunflowerDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public PrimalSunflowerDefinition(IntPtr pointer) : base(pointer)
    {
        // Same trick as every other timed custom plant (Iceweed, Celery Stalker): the Shooter
        // subclass is what makes the native Plant.UpdateShooter count mLaunchCounter down, and
        // CoreLib turns each reset of it into OnLaunchCounterTriggered. Native
        // UpdateProductionPlant is only reached for the vanilla sun seed types, so a custom
        // sun plant produces its own sun from the controller (Board.AddCoin, as CoreLib's own
        // Sunflower mint effect does).
        m_subClass = PlantSubClass.Shooter;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Primal Sunflower", false);
        SetDescription("Primal Sunflowers are a little pricier than ordinary Sunflowers, but they make noticeably more sun.", false);
        SetTooltip("Primal Sunflower produces more sun than a regular Sunflower.", false);

        // PvZ2 PrimalSunflowerDefault: Cost 75, Hitpoints 300, PacketCooldown 5, one "sun" Action
        // on the same 32-36 s cooldown as PvZ2's Sunflower, but SunProducer75 against Sunflower's
        // SunProducer50 (1.5x). Cost 75 = 1.5x Replanted's Sunflower (50) as well. Hitpoints 300
        // match Replanted's Sunflower; PacketCooldown 5 -> m_refreshTime 500 (~100 ticks/sec).
        m_seedCost = 75;
        m_versusCost = 75;
        m_health = 300;

        m_refreshTime = 500;
        m_versusBaseRefreshTime = 500;
        m_versusSuddenDeathRefreshTime = 125;

        // Same production interval as Replanted's own Sunflower, read from its definition rather
        // than guessed (PvZ2 uses one cooldown for both too). The sun amount is where Primal
        // differs - see PrimalSunflowerBehaviorController.
        var sunflowerDef = ReloadedUtils.GetPlantDefinition(SeedType.Sunflower);
        m_launchRate = sunflowerDef != null && sunflowerDef.m_launchRate > 0 ? sunflowerDef.m_launchRate : 2400;

        // PvZ2 family: Enlighten-mint (CoreLib gives a boosted sun plant an extra sun per cycle).
        m_mintFamily = MintFamily.EnlightenMint;

        SetSeedPacketImage(CompleteCollectionMod.PrimalSunflowerBundleId, "assets/plantrip/primal_sunflower/spr_seedpacket_primal_sunflower.png");
        SetPreviewImage(CompleteCollectionMod.PrimalSunflowerBundleId, "assets/plantrip/primal_sunflower/spr_seedpacket_primal_sunflower.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "PrimalSunflower_Default",
            AssetBundleId = CompleteCollectionMod.PrimalSunflowerBundleId,
            SkinPrefabId = "assets/plantrip/primal_sunflower/models/default/plant/primal_sunflower.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the sun sound is a PlaySound Animation Event
        // baked into the "special" clip from the PAM (see PvZRipTools plant_sounds.py).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<PrimalSunflowerBehaviorController>();
    }
}

// Clips (30 fps): idle, and "special" = the sun-producing animation. "special" carries the PlaySound
// event and an ExecuteAttack event on the PAM's use_action frame, so the sun pops out on that frame
// (and the sound plays) at the game's animation speed.
[RegisterTypeInIl2Cpp]
public class PrimalSunflowerBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // PvZ2 Primal Sunflower makes 75 sun per cycle where its Sunflower makes 50 (1.5x). Replanted's
    // Sunflower makes one 25-sun coin per cycle, so 1.5x is 37.5, which no coin size gives:
    // a normal sun (25) plus a small sun (15) = 40 per cycle (1.6x) is the closest.
    private const CoinType MainCoin = CoinType.Sun;
    private const CoinType BonusCoin = CoinType.SmallSun;

    #endregion

    #region Constructors

    public PrimalSunflowerBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    // Native sun plants start with a first sun after RandRange(300, launchRate / 2) ticks (read from
    // Plant.PlantInitialize); a custom seed type otherwise gets RandRange(0, launchRate).
    public override void PostInitialize()
    {
        base.PostInitialize();

        int upper = Math.Max(301, Plant.mLaunchRate / 2);
        Plant.mLaunchCounter = UnityEngine.Random.Range(300, upper);
    }

    public override void OnLaunchCounterTriggered()
    {
        base.OnLaunchCounterTriggered();

        // The sun leaves on the clip's ExecuteAttack event (OnAnimationAttackEvent).
        PlayAnimation("special");
    }

    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        Board.AddCoin(Plant.mX, Plant.mY, MainCoin, CoinMotion.FromPlant);
        Board.AddCoin(Plant.mX, Plant.mY, BonusCoin, CoinMotion.FromPlant);

        // Same as CoreLib's own Sunflower: an active Enlighten-mint boost adds one more sun per cycle.
        if (bMintEffectActive)
        {
            Board.AddCoin(Plant.mX, Plant.mY, CoinType.Sun, CoinMotion.FromPlant);
        }
    }

    #endregion
}
