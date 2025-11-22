using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Projectile;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Content.Projectiles;
using UnityEngine;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class BloomerangDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;
    
    public BloomerangDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        m_entryStatus = ReloadedEntryStatus.Ready;
        
        SetName("Bloomerang", false);
        SetDescription("As the first new member to your home defense team, Bloomerang enjoys long walkabouts with his friend, Koala Bear, and listening to his Bob Barley albums out back.", false);
        SetTooltip("Bloomerangs can hit up to three targets in their lane, twice each coming and going!", false);

        m_seedCost = 175;
        m_versusCost = 175;
        
        m_refreshTime = 500;
        m_versusBaseRefreshTime = 500;
        m_versusSuddenDeathRefreshTime = 125;
        
        m_launchRate = 300;
        
        SetSeedPacketImage(CompleteCollectionMod.EgyptianAssetBundleId, "assets/plant/bloomerang/spr_seedpacket_bloomerang.png");
        SetPreviewImage(CompleteCollectionMod.EgyptianAssetBundleId, "assets/plant/bloomerang/spr_preview_bloomerang.png");
        
        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Bloomerang_Default",
            AssetBundleId = CompleteCollectionMod.EgyptianAssetBundleId,
            SkinPrefabId = "assets/plant/bloomerang/models/default/plant/bloomerang.prefab",
            ScaleOverride = new Vector3(1.35f, 1.35f, 1)
        });
        
        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");
        
        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<BloomerangBehaviorController>();
    }
}

[RegisterTypeInIl2Cpp]
public class BloomerangBehaviorController : CustomPlantBehaviorController
{
    #region Variables
    
    #endregion

    #region Constructors

    public BloomerangBehaviorController(IntPtr pointer) : base(pointer)
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
        zombies.RemoveAll(z => z.mItem.mPosX <= Plant.mX);
        zombies.Sort((a, b) => a.mItem.mY.CompareTo(b.mItem.mY));

        if (!zombies.Any())
        {
            return;
        }
        
        var m_currentTarget = zombies.Count > 3 ? zombies[2].mItem : zombies.Last().mItem;
        
        var projectile = SpawnProjectile(BloomerangProjectileDefinition.BloomerangProjectileType);
        projectile.mTargetZombieID = m_currentTarget.mRelatedZombieID;
    }

    #endregion
}

[RegisterTypeInIl2Cpp]
public class BloomerangProjectileDefinition : CustomProjectileDefinition
{
    #region Variables

    public static ProjectileType BloomerangProjectileType;

    #endregion

    #region Constructors

    public BloomerangProjectileDefinition(IntPtr pointer) : base(pointer)
    {
        BloomerangProjectileType = m_projectileType;

        m_damage = 20;
        SetDamageRangeFlags(DamageRangeFlags.Ground);
        SetHitSfx(CompleteCollectionMod.EgyptianAssetBundleId, "assets/plant/bloomerang/models/default/projectile_hit/sfx_hit.wav");
        
        RegisterSkin(new SpriteRendererProjectileSkin()
        {
            skinId = "BloomerangProjectile_Default",
            AssetBundleId = CompleteCollectionMod.EgyptianAssetBundleId,
            SkinPrefabId = "assets/plant/bloomerang/models/default/projectile/projectile.prefab",
            ScaleOverride = new Vector3(1.275f, 1.275f, 1)
        });
    }

    #endregion

    #region Methods

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<BloomerangProjectileBehaviorController>();
    }

    #endregion
}

[RegisterTypeInIl2Cpp]
public class BloomerangProjectileBehaviorController : CustomProjectileBehaviorController
{
    #region Variables

    private float movementMult = 1;
    private float startMx = -1;
    
    private List<Zombie> hitZombiesForward = new List<Zombie>();
    private List<Zombie> hitZombiesBackward = new List<Zombie>();
    
    private Zombie nextTarget;
    private float minX = -1;

    #endregion

    #region Constructors

    public BloomerangProjectileBehaviorController(IntPtr pointer) : base(pointer)
    {
        
    }

    #endregion

    #region Methods

    public override bool PreUpdateNormalMotion()
    {
        base.PreUpdateNormalMotion();
        
        if(startMx < 0)
        {
            startMx = Projectile.mPosX;
        }
        
        Projectile.mPosX += 200f * Time.deltaTime * movementMult; //TODO
        
        Projectile.CheckForCollision();

        if (nextTarget != null && !nextTarget.IsDeadOrDying())
        {
            minX = nextTarget.mPosX;
        }

        if (Mathf.Approximately(movementMult, 1f) && (nextTarget == null || nextTarget.IsDeadOrDying()))
        {
            nextTarget = FindZombieTarget();
        }

        float movementDampen = 0.85f;
        if(movementMult > -1 && (hitZombiesForward.Count >= 3 || (nextTarget == null && Projectile.mPosX >= minX)))
        {
            minX = -1;
            if (movementMult > 0)
            {
                movementMult *= movementDampen;
                if(movementMult <= 0.1)
                {
                    Projectile.mProjectileAge = 0;
                    movementMult = -0.125f;
                }
            }
            else
            {
                movementMult /= movementDampen;
            }
        }

        if (movementMult < -1)
        {
            movementMult = -1;
        }
        
        if(Projectile.mPosX <= startMx)
        {
            Projectile.Die();
        }

        return false;
    }

    public override bool PreDoImpact(Zombie theZombie)
    {
        base.PreDoImpact(theZombie);
        
        var hitList = movementMult > 0 ? hitZombiesForward : hitZombiesBackward;
        if (hitList.Contains(theZombie))
        {
            return false;
        }
        
        DamageZombie(theZombie);
        hitList.Add(theZombie);

        if (theZombie == nextTarget)
        {
            nextTarget = null;
        }

        return false;
    }

    public Zombie FindZombieTarget()
    {
        var zombies = Board.m_zombies.m_list.ToList();
        zombies.RemoveAll(z => z.mItem == null);
        zombies.RemoveAll(z => z.mItem.IsDeadOrDying());
        zombies.RemoveAll(z => z.mItem.mRow != Projectile.mRow && z.mItem.mZombieType != ZombieType.Boss);
        zombies.RemoveAll(z => !z.mItem.EffectedByDamage(DamageRangeFlags.Ground));
        zombies.RemoveAll(z => z.mItem.mPosX <= Projectile.mPosX);
        zombies.RemoveAll(z => hitZombiesForward.Contains(z.mItem) || hitZombiesBackward.Contains(z.mItem));
        zombies.Sort((a, b) => a.mItem.mY.CompareTo(b.mItem.mY));

        if (!zombies.Any())
        {
            return null;
        }
        
        return zombies.Count > 3 ? zombies[2].mItem : zombies.Last().mItem;
    }

    public override void Reset()
    {
        base.Reset();

        movementMult = 1;
        startMx = -1;
        
        hitZombiesForward.Clear();
        hitZombiesBackward.Clear();
        
        nextTarget = null;
        minX = -1;
    }

    #endregion
}