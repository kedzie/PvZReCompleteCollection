using MelonLoader;
using PvZCompleteCollection2;
using PvZCompleteCollection2.Plants.Defs;
using PvZReCoreLib.Content;
using PvZReCoreLib.Util;
using UnityEngine;

[assembly: MelonInfo(typeof(CompleteCollectionMod), "Pvz Complete Collection2", "1.1", "Kedzie")]
[assembly: MelonGame("PopCap Games", "PvZ Replanted")]
[assembly: MelonAdditionalDependencies(new [] { "PvZReCoreLib" })]

namespace PvZCompleteCollection2;

public class CompleteCollectionMod : MelonMod
{
    #region Variables

    public static string ModId = "PvZCompleteCollection2";

    public static string EgyptianAssetBundleId = "PvZCC_EgyptianAssets";
    public static string MintAssetBundleId = "PvZCC_MintAssets";
    public static string FirepeashooterBundleId = "PvZCC_FirepeashooterAssets";
    public static string BambooSpartanBundleId = "PvZCC_BambooSpartanAssets";
    public static string EndurianBundleId = "PvZCC_EndurianAssets";
    public static string CelerystalkerBundleId = "PvZCC_CelerystalkerAssets";

    #endregion

    #region Constructors



    #endregion

    #region Methods

    public override void OnLateInitializeMelon()
    {
        base.OnLateInitializeMelon();

        RegistryBridge.OnRegistryBridgeInit += () =>
        {
            // NOT registered here: RegistryBridge is a shared service across all loaded
            // mods, and the original CompleteCollection mod already registers this exact
            // key ("PvZCC_EgyptianAssets"). RegisterAssetBundle does a plain Dictionary.Add
            // with no existing-key check, so re-registering it here throws and aborts the
            // rest of this callback - which was silently skipping the FirepeashooterBundleId
            // registration right below it. FirePeashooter's SetAlmanacBackground call just
            // needs the original mod to have registered this key first (requires running
            // this build alongside the original, which is the whole point of this variant).
            // RegistryBridge.RegisterAssetBundle(EgyptianAssetBundleId, "Mods/CompleteCollection/pvzegyptianbundle");
            // RegistryBridge.RegisterAssetBundle(MintAssetBundleId, "Mods/CompleteCollection/pvzmintbundle"); // standalone build: only FirePeashooter is registered below

            RegistryBridge.RegisterAssetBundle(FirepeashooterBundleId, "Mods/CompleteCollection/pvzfirepeashooterbundle");
            RegistryBridge.RegisterAssetBundle(BambooSpartanBundleId, "Mods/CompleteCollection/pvzbamboospartanbundle");
            RegistryBridge.RegisterAssetBundle(EndurianBundleId, "Mods/CompleteCollection/pvzendurianbundle");
            RegistryBridge.RegisterAssetBundle(CelerystalkerBundleId, "Mods/CompleteCollection/pvzcelerystalkerbundle");

        };

        CustomContentRegistry.PostInit += () =>
        {
            // Standalone build: only our own FirePeashooter is registered here so this
            // can run alongside the original CompleteCollection mod without duplicate
            // seed packets. These 4 stay compiled into the DLL but inactive.
            // CustomContentRegistry.RegisterCustomProjectile(ScriptableObject.CreateInstance<BloomerangProjectileDefinition>());
            // CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<BloomerangDefinition>());
            //
            // CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<BonkchoyDefinition>());
            //
            // CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<IcebergLettuceDefinition>());
            //
            // CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<EnlightenMintDefinition>());

            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<FirePeashooterDefinition>());
            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<BambooSpartanDefinition>());
            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<EndurianDefinition>());
            CustomContentRegistry.RegisterCustomPlant(ScriptableObject.CreateInstance<CelerystalkerDefinition>());
        };
    }

    #endregion
}
