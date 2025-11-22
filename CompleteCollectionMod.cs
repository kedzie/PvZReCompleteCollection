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
        };
        
        CustomContentRegistry.PostInit += () =>
        {
            CustomContentRegistry.RegisterCustomProjectile(ScriptableObject.CreateInstance<BloomerangProjectileDefinition>());
            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<BloomerangDefinition>());
            
            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<BonkchoyDefinition>());
            
            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<IcebergLettuceDefinition>());

            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<EnlightenMintDefinition>());
        };
    }
    
    #endregion
}