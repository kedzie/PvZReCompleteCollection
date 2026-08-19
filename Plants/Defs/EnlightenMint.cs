using Il2CppInterop.Runtime;
using Il2CppReloaded.Characters;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Content.Plants.Mint;
using UnityEngine;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection2.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class EnlightenMintDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;
    
    public EnlightenMintDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Normal;
        
        m_entryStatus = ReloadedEntryStatus.Ready;

        m_health = 5000000;

        SetName("Enlighten-Mint", false);
        SetDescription("Enlighten-mint wants to stress that while \"enlightenment\" is properly understood as \"full comprehension of a situation,\" she herself frequently likes to ask follow-up questions just to be sure.", false);
        SetTooltip("Enlighten-mints give a burst of sun when planted, and provide an additional temporary boost to Enlighten-mint Family plants.", false);
        
        m_seedCost = 0;
        m_versusCost = 0;
        
        m_refreshTime = 8500;
        m_versusBaseRefreshTime = 8500;
        m_versusSuddenDeathRefreshTime = 8500;
        
        m_launchRate = 5000;
        
        SetSeedPacketImage(CompleteCollectionMod.MintAssetBundleId, "assets/plant/enlightenmint/spr_seedpacket_enlightenmint.png");
        SetPreviewImage(CompleteCollectionMod.MintAssetBundleId, "assets/plant/enlightenmint/spr_preview_enlightenmint.png");
        
        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "EnlightenMint_Default",
            AssetBundleId = CompleteCollectionMod.MintAssetBundleId,
            SkinPrefabId = "assets/plant/enlightenmint/models/default/plant/enlightenmint.prefab",
            ScaleOverride = new Vector3(1.35f, 1.35f, 1)
        });
        
        SetAlmanacBackground(CompleteCollectionMod.MintAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_warp.png");

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<EnlightenMintController>();
    }
}

[RegisterTypeInIl2Cpp]
public class EnlightenMintController : CustomPlantBehaviorController
{
    #region Variables
    
    private Phase currentPhase = Phase.Initial;
    float timeUntilNextPhase = 0.5f;

    #endregion

    #region Constructors

    public EnlightenMintController(IntPtr pointer) : base(pointer)
    {
        
    }

    #endregion

    #region Methods

    public override void Update()
    {
        base.Update();

        timeUntilNextPhase -= Time.deltaTime;
        if (timeUntilNextPhase <= 0)
        {
            if (currentPhase == Phase.Initial)
            {
                Board.AddCoin(Plant.mX, Plant.Y, CoinType.SmallSun, CoinMotion.FromPlant);
                Board.AddCoin(Plant.mX, Plant.Y, CoinType.Sun, CoinMotion.FromPlant);
                Board.AddCoin(Plant.mX, Plant.Y, CoinType.SmallSun, CoinMotion.FromPlant);
                currentPhase = Phase.GaveSun;
                timeUntilNextPhase = 4f;
            }
            else if (currentPhase == Phase.GaveSun)
            {
                List<MintFamilyBehaviorController> allMints = MintUtils.GetAllControllersForFamily(mBoard, MintFamily.EnlightenMint);
                foreach (MintFamilyBehaviorController mint in allMints)
                {
                    mint.StartBuffEffect();
                }
                currentPhase = Phase.BuffedPlants;
                timeUntilNextPhase = 5f;
            }
            else if (currentPhase == Phase.BuffedPlants)
            {
                List<MintFamilyBehaviorController> allMints = MintUtils.GetAllControllersForFamily(mBoard, MintFamily.EnlightenMint);
                foreach (MintFamilyBehaviorController mint in allMints)
                {
                    mint.FinishBuffEffect();
                }
                currentPhase = Phase.BuffRanOut;
                
                Plant.mController.AnimationController.PlayAnimation("outro", CharacterTracks.NULL, 30, AnimLoopType.PlayOnce);
                
                timeUntilNextPhase = 1f;
            }
            else if (currentPhase == Phase.BuffRanOut)
            {
                Plant.Die();
                currentPhase = Phase.Dead;
                timeUntilNextPhase = 9999f;
            }
        }
    }

    public override void Reset()
    {
        base.Reset();
        
        currentPhase = Phase.Initial;
        timeUntilNextPhase = 0.5f;
    }

    #endregion

    private enum Phase
    {
        Initial,
        GaveSun,
        BuffedPlants,
        BuffRanOut,
        Dead
    }
}