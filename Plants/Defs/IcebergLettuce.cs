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
        
        // One pass over the shared zombie list: the actually-closest zombie within 30 (cheap tests first).
        Zombie targetZombie = null;
        float bestDistance = 0f;
        foreach (var entry in ZombieSnapshot.Items(Board))
        {
            var candidate = entry.mItem;
            if (candidate == null
                || (candidate.mRow != Plant.mRow && candidate.mZombieType != ZombieType.Boss))
            {
                continue;
            }

            float distance = Math.Abs(candidate.mPosX - Plant.mX);
            if (distance > 30f
                || (targetZombie != null && distance >= bestDistance)
                || candidate.IsDeadOrDying()
                || !candidate.EffectedByDamage(DamageRangeFlags.Ground))
            {
                continue;
            }

            targetZombie = candidate;
            bestDistance = distance;
        }

        if (targetZombie != null)
        {
            targetZombie.HitIceTrap();
            targetZombie.mIceTrapCounter = 1000;
            
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