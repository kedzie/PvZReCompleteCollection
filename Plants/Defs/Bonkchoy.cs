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
        
        var zombies = Board.m_zombies.m_list.ToList();
        zombies.RemoveAll(z => z.mItem == null);
        zombies.RemoveAll(z => z.mItem.IsDeadOrDying());
        zombies.RemoveAll(z => z.mItem.mRow != Plant.mRow && z.mItem.mZombieType != ZombieType.Boss);
        zombies.RemoveAll(z => !z.mItem.EffectedByDamage(DamageRangeFlags.Ground));
        zombies.RemoveAll(z => Math.Abs(z.mItem.mPosX - Plant.mX) > 130f);
        
        var frontZombies = zombies.Where(z => z.mItem.mPosX > Plant.mX).ToList();
        frontZombies.Sort((a, b) => a.mItem.mPosX.CompareTo(b.mItem.mPosX));
        
        var backZombies = zombies.Where(z => z.mItem.mPosX <= Plant.mX).ToList();
        backZombies.Sort((a, b) => b.mItem.mPosX.CompareTo(a.mItem.mPosX));
        
        
        if (backZombies.Any() && (lastPunchFront || !frontZombies.Any()))
        {
            var zombie = backZombies.FirstOrDefault();
            DamageZombie(zombie.mItem, 15, 0);
            if (!zombie.mItem.IsDeadOrDying())
            {
                PlayAnimation("attack_left");
            }
            else
            {
                PlayAnimation("attack_left_finish");
            }
            lastPunchFront = false;
        }
        else if(frontZombies.Any())
        {
            var zombie = frontZombies.FirstOrDefault();
            DamageZombie(zombie.mItem, 15, 0);
            if (!zombie.mItem.IsDeadOrDying())
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