using MelonLoader;
using PvZCompleteCollection;
using PvZCompleteCollection.Plants.Defs;
using PvZReCoreLib.Content;
using PvZReCoreLib.Util;
using UnityEngine;

[assembly: MelonInfo(typeof(CompleteCollectionMod), "PvZ Complete Collection", "0.1", "Draco9990")]
[assembly: MelonGame("PopCap Games", "PvZ Replanted")]
[assembly: MelonAdditionalDependencies(new [] { "PvZReCoreLib" })]

namespace PvZCompleteCollection;

public class CompleteCollectionMod : MelonMod
{
    #region Variables

    public static string ModId = "PvZCompleteCollection";

    public static string EgyptianAssetBundleId = "PvZCC_EgyptianAssets";
    public static string MintAssetBundleId = "PvZCC_MintAssets";
    public static string FirepeashooterBundleId = "PvZCC_FirepeashooterAssets";
    public static string BambooSpartanBundleId = "PvZCC_BambooSpartanAssets";
    public static string EndurianBundleId = "PvZCC_EndurianAssets";
    public static string CelerystalkerBundleId = "PvZCC_CelerystalkerAssets";
    public static string TumbleweedBundleId = "PvZCC_TumbleweedAssets";
    public static string IceweedBundleId = "PvZCC_IceweedAssets";
    public static string DuskLobberBundleId = "PvZCC_DuskLobberAssets";
    public static string PrimalWallnutBundleId = "PvZCC_PrimalWallnutAssets";
    public static string PeanutBundleId = "PvZCC_PeanutAssets";
    public static string PeapodBundleId = "PvZCC_PeapodAssets";
    public static string PrimalSunflowerBundleId = "PvZCC_PrimalSunflowerAssets";
    public static string GoldBloomBundleId = "PvZCC_GoldBloomAssets";

    #endregion

    #region Constructors



    #endregion

    #region Methods

    public override void OnLateInitializeMelon()
    {
        base.OnLateInitializeMelon();
        
        RegistryBridge.OnRegistryBridgeInit += () =>
        {
            RegistryBridge.RegisterAssetBundle(EgyptianAssetBundleId, "Mods/CompleteCollection/pvzegyptianbundle");
            RegistryBridge.RegisterAssetBundle(MintAssetBundleId, "Mods/CompleteCollection/pvzmintbundle");

            RegistryBridge.RegisterAssetBundle(FirepeashooterBundleId, "Mods/CompleteCollection/pvzfirepeashooterbundle");
            RegistryBridge.RegisterAssetBundle(BambooSpartanBundleId, "Mods/CompleteCollection/pvzbamboospartanbundle");
            RegistryBridge.RegisterAssetBundle(EndurianBundleId, "Mods/CompleteCollection/pvzendurianbundle");
            RegistryBridge.RegisterAssetBundle(CelerystalkerBundleId, "Mods/CompleteCollection/pvzcelerystalkerbundle");
            RegistryBridge.RegisterAssetBundle(TumbleweedBundleId, "Mods/CompleteCollection/pvztumbleweedbundle");
            RegistryBridge.RegisterAssetBundle(IceweedBundleId, "Mods/CompleteCollection/pvziceweedbundle");
            RegistryBridge.RegisterAssetBundle(DuskLobberBundleId, "Mods/CompleteCollection/pvzdusklobberbundle");
            RegistryBridge.RegisterAssetBundle(PrimalWallnutBundleId, "Mods/CompleteCollection/pvzprimal_wallnutbundle");
            RegistryBridge.RegisterAssetBundle(PeanutBundleId, "Mods/CompleteCollection/pvzpeanutbundle");
            RegistryBridge.RegisterAssetBundle(PeapodBundleId, "Mods/CompleteCollection/pvzpeapodbundle");
            RegistryBridge.RegisterAssetBundle(PrimalSunflowerBundleId, "Mods/CompleteCollection/pvzprimal_sunflowerbundle");
            RegistryBridge.RegisterAssetBundle(GoldBloomBundleId, "Mods/CompleteCollection/pvzgoldbloombundle");

        };
        
        CustomContentRegistry.PostInit += () =>
        {
            CustomContentRegistry.RegisterCustomProjectile(ScriptableObject.CreateInstance<BloomerangProjectileDefinition>());
            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<BloomerangDefinition>());
            
            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<BonkchoyDefinition>());
            
            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<IcebergLettuceDefinition>());

            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<EnlightenMintDefinition>());

            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<FirePeashooterDefinition>());

            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<BambooSpartanDefinition>());

            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<EndurianDefinition>());

            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<CelerystalkerDefinition>());

            CustomContentRegistry.RegisterCustomProjectile(ScriptableObject.CreateInstance<TumbleweedProjectileDefinition>());
            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<TumbleweedDefinition>());

            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<IceweedDefinition>());

            CustomContentRegistry.RegisterCustomProjectile(ScriptableObject.CreateInstance<DuskLobberProjectileDefinition>());
            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<DuskLobberDefinition>());

            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<PrimalWallnutDefinition>());
            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<PeanutDefinition>());
            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<PeapodDefinition>());
            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<PrimalSunflowerDefinition>());
            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<GoldBloomDefinition>());

            ZombieCards.RegisterAll();
        };
    }
    
    #endregion
}