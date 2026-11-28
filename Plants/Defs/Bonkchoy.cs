using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using UnityEngine;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class BonkchoyDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;
    
    public BonkchoyDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        m_entryStatus = ReloadedEntryStatus.Ready;
        
        SetName("Bonk Choy", false);
        SetDescription("A freelance dentist on the side, he's offering a special this month - he'll knock two of your teeth out for the price of one!", false);
        SetTooltip("Bonk Choys rapidly punch nearby enemies that are ahead or behind them.", false);

        m_seedCost = 150;
        m_versusCost = 150;
        
        m_refreshTime = 500;
        m_versusBaseRefreshTime = 500;
        m_versusSuddenDeathRefreshTime = 125;
        
        m_launchRate = 30;
        
        SetSeedPacketImage(CompleteCollectionMod.EgyptianAssetBundleId, "assets/plant/bonkchoy/spr_seedpacket_bonkchoy.png");
        SetPreviewImage(CompleteCollectionMod.EgyptianAssetBundleId, "assets/plant/bonkchoy/spr_preview_bonkchoy.png");
        
        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "BonkChoy_Default",
            AssetBundleId = CompleteCollectionMod.EgyptianAssetBundleId,
            SkinPrefabId = "assets/plant/bonkchoy/models/default/plant/bonkchoy.prefab",
            ScaleOverride = new Vector3(1.35f, 1.35f, 1)
        });
        
        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");
        
        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<BongchoyBehaviorController>();
    }
}

[RegisterTypeInIl2Cpp]
public class BongchoyBehaviorController : CustomPlantBehaviorController
{
    #region Variables
    
    private bool lastPunchFront = false;
    
    #endregion

    #region Constructors

    public BongchoyBehaviorController(IntPtr pointer) : base(pointer)
    {
        
    }

    #endregion

    #region Methods

    public override void OnLaunchCounterTriggered()
    {
        base.OnLaunchCounterTriggered();
        
        // One pass over the shared zombie list: the nearest zombie ahead and the nearest behind, within 130.
        Zombie front = null;
        Zombie back = null;
        foreach (var entry in ZombieSnapshot.Items(Board))
        {
            var candidate = entry.mItem;
            if (candidate == null
                || (candidate.mRow != Plant.mRow && candidate.mZombieType != ZombieType.Boss)
                || Math.Abs(candidate.mPosX - Plant.mX) > 130f)
            {
                continue;
            }

            bool ahead = candidate.mPosX > Plant.mX;
            if (ahead ? (front != null && candidate.mPosX >= front.mPosX) : (back != null && candidate.mPosX <= back.mPosX))
            {
                continue;
            }

            if (candidate.IsDeadOrDying() || !candidate.EffectedByDamage(DamageRangeFlags.Ground))
            {
                continue;
            }

            if (ahead)
            {
                front = candidate;
            }
            else
            {
                back = candidate;
            }
        }

        if (back != null && (lastPunchFront || front == null))
        {
            var zombie = back;
            DamageZombie(zombie, 15, 0);
            if (!zombie.IsDeadOrDying())
            {
                PlayAnimation("attack_left");
            }
            else
            {
                PlayAnimation("attack_left_finish");
            }
            lastPunchFront = false;
        }
        else if (front != null)
        {
            var zombie = front;
            DamageZombie(zombie, 15, 0);
            if (!zombie.IsDeadOrDying())
            {
                PlayAnimation("attack_right");
            }
            else
            {
                PlayAnimation("attack_right_finish");
            }
            lastPunchFront = true;
        }
    }

    public override void Reset()
    {
        base.Reset();
        
        lastPunchFront = false;
    }

    #endregion
}