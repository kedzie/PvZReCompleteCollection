using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Projectile;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Content.Plants.Mint;
using PvZReCoreLib.Content.Projectiles;
using PvZReCoreLib.Util;
using UnityEngine;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection2.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class MegaGatlingDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public MegaGatlingDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Mega Gatling Pea", false);
        SetDescription("Mega Gatling Pea spins up a barrage of peas down its lane, and now and then throws in a giant pea that flattens whatever it hits.", false);
        SetTooltip("Fires a stream of peas, and every few volleys a giant pea.", false);

        // PvZ2 MegaGatlingDefault: Cost 400, Hitpoints 300 (= Replanted's Peashooter), PacketCooldown 5.
        // PacketCooldown 5 -> m_refreshTime 500 (~100 ticks/sec), like Peapod and Primal Wall-nut.
        m_seedCost = 400;
        m_versusCost = 400;
        m_health = 300;

        m_refreshTime = 500;
        m_versusBaseRefreshTime = 500;
        m_versusSuddenDeathRefreshTime = 125;

        // PvZ2's pea Actions cool down 1.35-1.5 s - the same as Peapod's and FirePeashooter's, which
        // use 140. Each cooldown starts one volley (see MegaGatlingBehaviorController).
        m_launchRate = 140;

        // PvZ2 family: Appease-mint.
        m_mintFamily = MintFamily.AppeaseMint;

        SetSeedPacketImage(CompleteCollectionMod.MegaGatlingBundleId, "assets/plantrip/megagatling/spr_seedpacket_megagatling.png");
        SetPreviewImage(CompleteCollectionMod.MegaGatlingBundleId, "assets/plantrip/megagatling/spr_seedpacket_megagatling.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "MegaGatling_Default",
            AssetBundleId = CompleteCollectionMod.MegaGatlingBundleId,
            SkinPrefabId = "assets/plantrip/megagatling/models/default/plant/megagatling.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // PvZ2 gives the pea Actions an empty ProjectileLaunchSound and the PAM has no PlaySample on
        // the attack, so firing is silent apart from the peas' own hit sounds (the giant pea's is
        // code-driven, see MegaGatlingGiantPeaDefinition).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<MegaGatlingBehaviorController>();
    }
}

// Two animation states: the Animator default (idle) and "attack" (31 frames at 30 fps, the barrels spin
// up and out). The PAM marks one launch frame (use_action on +16); the bundle build bakes ExecuteAttack
// events on 5 frames from there, 3 frames (0.1 s) apart (PvZBundleBuilder.ExtraLaunchOffsets), so one
// attack clip is one volley of ShotsPerVolley shots - and the shots follow the game's animation speed,
// no timers in code. Each event spawns ONE projectile: a native pea, or a giant pea.
[RegisterTypeInIl2Cpp]
public class MegaGatlingBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // Number of ExecuteAttack events baked on the attack clip (PvZBundleBuilder.ExtraLaunchOffsets,
    // "megagatling"). Only used to document; the code reacts to each event as it comes.
    private const int ShotsPerVolley = 5;

    // PvZ2 only fires the giant pea (GiantPeaDefault, 600 damage - thirty peas' worth) as the Plant Food
    // action, which this mod does not have, so it is rationed instead: the first shot of every Nth volley
    // is a giant pea, the rest are normal peas. Tune to taste.
    private const int VolleysPerGiantPea = 3;

    // Same nudge FirePeashooter / Peapod use (identical PvZ2 spawn offset, 35/-32). Tune by eye in game.
    private const float MouthHeightOffset = 23f;

    // The giant pea art is anchored at the BOTTOM of its (~90 px) ball, unlike the native pea. Y grows
    // downward in this engine, so to put the ball's centre where a native pea's is, the anchor goes half
    // a ball height lower. First-pass guess, tune by eye in game.
    private const float GiantPeaAnchorDrop = 45f;

    // Shots seen since the current volley started, and volleys started so far (for the giant pea rhythm).
    private int shotsThisVolley;
    private int volleys;

    #endregion

    #region Constructors

    public MegaGatlingBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    // The launch counter only says "time to shoot": start the attack clip if there is something to hit.
    // The projectiles themselves leave on the clip's ExecuteAttack events.
    public override void OnLaunchCounterTriggered()
    {
        base.OnLaunchCounterTriggered();

        if (FindTarget() == null)
        {
            return;
        }

        shotsThisVolley = 0;
        volleys++;
        PlayAnimation("attack");
    }

    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        var target = FindTarget();
        if (target == null)
        {
            return;
        }

        shotsThisVolley++;
        bool giant = shotsThisVolley == 1 && volleys % VolleysPerGiantPea == 0;

        // Real native peas, like Peapod's (see FirePeashooter for why the mouth offset and
        // mDamageRangeFlags are needed with SpawnProjectile).
        var projectile = SpawnProjectile(giant
            ? MegaGatlingGiantPeaDefinition.GiantPeaProjectileType
            : ProjectileType.PeashooterPea);
        projectile.mPosY += MouthHeightOffset + (giant ? GiantPeaAnchorDrop : 0f);
        projectile.mTargetZombieID = target.mRelatedZombieID;
        projectile.mDamageRangeFlags = DamageRangeFlags.Ground;
    }

    // Nearest living zombie ahead of the plant in its lane.
    private Zombie FindTarget()
    {
        // The board is already gone when a last animation event fires while leaving the level.
        if (Board?.m_zombies?.m_list == null)
        {
            return null;
        }

        return Board.m_zombies.m_list.ToList()
            .Where(z => z.mItem != null
                        && !z.mItem.IsDeadOrDying()
                        && (z.mItem.mRow == Plant.mRow || z.mItem.mZombieType == ZombieType.Boss)
                        && z.mItem.EffectedByDamage(DamageRangeFlags.Ground)
                        && z.mItem.mPosX > Plant.mX)
            .OrderBy(z => z.mItem.mPosX)
            .Select(z => z.mItem)
            .FirstOrDefault();
    }

    public override void Reset()
    {
        base.Reset();

        shotsThisVolley = 0;
        volleys = 0;
    }

    #endregion
}

// PvZ2 GiantPeaDefault (the Plant Food shot of the pea plants): BaseDamage 600, shooter damage flags,
// speed 400 against the pea's 333, art REPEATER_PLANTFOOD_GIANTPEA, splat SPLAT_GIANTPEA, impact sound
// Play_Plant_Peapod_Impact_Nitro. Replanted has no bigger pea of its own (the Melon is a lobbed shot), so
// this is a custom projectile that flies like a pea (Straight motion, same speed as a native pea).
[RegisterTypeInIl2Cpp]
public class MegaGatlingGiantPeaDefinition : CustomProjectileDefinition
{
    #region Variables

    public static ProjectileType GiantPeaProjectileType;

    // Instantiated per impact by MegaGatlingGiantPeaBehaviorController.
    public GameObject HitEffectPrefab;

    #endregion

    #region Constructors

    public MegaGatlingGiantPeaDefinition(IntPtr pointer) : base(pointer)
    {
        GiantPeaProjectileType = m_projectileType;

        // PvZ2: pea 20, giant pea 600.
        m_damage = 600;
        m_motionType = ProjectileMotion.Straight;
        SetDamageRangeFlags(DamageRangeFlags.Ground);
        // Impact sounds are code-driven (not in the PAM): Play_Plant_Peapod_Impact_Nitro, first variant.
        SetHitSfx(CompleteCollectionMod.MegaGatlingBundleId, "assets/plantrip/megagatling/sounds/184881107.ogg");

        RegistryBridge.LoadAssetFromAssetBundle<GameObject>(
            CompleteCollectionMod.MegaGatlingBundleId,
            "assets/plantrip/megagatling/models/default/projectile_hit/projectile_hit.prefab",
            prefab => HitEffectPrefab = prefab);

        RegisterSkin(new SpriteRendererProjectileSkin()
        {
            skinId = "MegaGatlingGiantPea_Default",
            AssetBundleId = CompleteCollectionMod.MegaGatlingBundleId,
            SkinPrefabId = "assets/plantrip/megagatling/models/default/projectile/projectile.prefab",
        });
    }

    #endregion

    #region Methods

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<MegaGatlingGiantPeaBehaviorController>();
    }

    #endregion
}

[RegisterTypeInIl2Cpp]
public class MegaGatlingGiantPeaBehaviorController : CustomProjectileBehaviorController
{
    #region Variables

    #endregion

    #region Constructors

    public MegaGatlingGiantPeaBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    // The engine only knows its own projectile types, so the hit is done here (same as Dusk Lobber):
    // the zombie takes the full damage (and the hit sound plays), then the pea is gone.
    public override bool PreDoImpact(Zombie theZombie)
    {
        var customDef = ProjectileDefinition.TryCast<MegaGatlingGiantPeaDefinition>();

        if (theZombie != null)
        {
            DamageZombie(theZombie);
            SpawnHitEffect(customDef, theZombie);
        }

        // Returning false skips the native DoImpact, which is what normally ends the projectile.
        Projectile.Die();
        return false;
    }

    private static void SpawnHitEffect(MegaGatlingGiantPeaDefinition customDef, Zombie theZombie)
    {
        if (customDef?.HitEffectPrefab == null || theZombie.mController == null)
        {
            return;
        }

        var instance = UnityEngine.Object.Instantiate(customDef.HitEffectPrefab, theZombie.mController.gameObject.transform.position, Quaternion.identity);
        UnityEngine.Object.Destroy(instance, 1f);
    }

    #endregion
}
