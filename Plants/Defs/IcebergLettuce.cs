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
public class IcebergLettuceDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;
    
    public IcebergLettuceDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        m_entryStatus = ReloadedEntryStatus.Ready;
        
        SetName("Iceberg Lettuce", false);
        SetDescription("Don't get lost staring into Iceberg's adorably cute eyes. If you do, it will be your last step... but only if you're a zombie.", false);
        SetTooltip("Iceberg Lettuces freeze a zombie when stepped on.", false);

        m_seedCost = 0;
        m_versusCost = 0;
        
        m_refreshTime = 2000;
        m_versusBaseRefreshTime = 2000;
        m_versusSuddenDeathRefreshTime = 2000;
        
        m_launchRate = 10;
        
        SetSeedPacketImage(CompleteCollectionMod.EgyptianAssetBundleId, "assets/plant/iceberglettuce/spr_seedpacket_iceberglettuce.png");
        SetPreviewImage(CompleteCollectionMod.EgyptianAssetBundleId, "assets/plant/iceberglettuce/spr_preview_iceberglettuce.png");
        
        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "IcebergLettuce_Default",
            AssetBundleId = CompleteCollectionMod.EgyptianAssetBundleId,
            SkinPrefabId = "assets/plant/iceberglettuce/models/default/plant/iceberglettuce.prefab",
            ScaleOverride = new Vector3(1.35f, 1.35f, 1)
        });
        
        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");
        
        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<IcebergLettuceBehaviorController>();
    }
}

[RegisterTypeInIl2Cpp]
public class IcebergLettuceBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    private bool triggered = false;
    
    #endregion

    #region Constructors

    public IcebergLettuceBehaviorController(IntPtr pointer) : base(pointer)
    {
        
    }

    #endregion

    #region Methods

    public override void OnLaunchCounterTriggered()
    {
        base.OnLaunchCounterTriggered();

        if (triggered)
        {
            return;
        }
        
        var zombies = Board.m_zombies.m_list.ToList();
        zombies.RemoveAll(z => z.mItem == null);
        zombies.RemoveAll(z => z.mItem.IsDeadOrDying());
        zombies.RemoveAll(z => z.mItem.mRow != Plant.mRow && z.mItem.mZombieType != ZombieType.Boss);
        zombies.RemoveAll(z => !z.mItem.EffectedByDamage(DamageRangeFlags.Ground));
        zombies.RemoveAll(z => Math.Abs(z.mItem.mPosX - Plant.mX) > 30f);
        // Was sorting by each zombie's raw absolute board position (missing the
        // "- Plant.mX" the filter above correctly uses), so with multiple
        // zombies in range it could pick an arbitrary one instead of the
        // actually-closest.
        zombies.Sort((a, b) => Math.Abs(a.mItem.mPosX - Plant.mX).CompareTo(Math.Abs(b.mItem.mPosX - Plant.mX)));

        if (zombies.Any())
        {
            var targetZombie = zombies[0];
            targetZombie.mItem.HitIceTrap();
            targetZombie.mItem.mIceTrapCounter = 1000;
            
            PlayAnimation("attack");

            triggered = true;
        }
    }

    public override void Reset()
    {
        base.Reset();

        triggered = false;
    }

    #endregion
}