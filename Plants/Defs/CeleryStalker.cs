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
public class CelerystalkerDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public CelerystalkerDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Celery Stalker", false);
        SetDescription("Celery Stalker hides underground, completely safe from zombies, until one wanders close enough to trigger an ambush.", false);
        SetTooltip("While hidden, zombies just walk past Celery Stalker without stopping. Once he pops up to attack, he's a normal target again, and burrows back down if left alone.", false);

        // Real CeleryStalkerDefault@PlantProperties: Cost 50, Hitpoints 1200,
        // PacketCooldown 15 - same 15s recharge tier as Endurian/Bamboo
        // Spartan. No damage-tier art like Endurian/Wallnut have (confirmed:
        // his rip has no "damage"-named folder at all, just a hidden/revealed
        // state) - just idle/idle_down.
        m_seedCost = 50;
        m_versusCost = 50;
        m_health = 1200;

        // Real PlantGridType is "ground" - like Potato Mine/Spikeweed, he
        // needs actual ground to burrow into and can't be placed on a
        // lilypad over water.
        m_requiresGround = true;

        m_refreshTime = 1500;
        m_versusBaseRefreshTime = 1500;
        m_versusSuddenDeathRefreshTime = 375;

        // Real Actions cooldown is 0.5s; same conversion used for Bamboo
        // Spartan's real 0.5s -> m_launchRate 50.
        m_launchRate = 50;

        SetSeedPacketImage(CompleteCollectionMod.CelerystalkerBundleId, "assets/plantrip/celerystalker/spr_seedpacket_celerystalker.png");
        SetPreviewImage(CompleteCollectionMod.CelerystalkerBundleId, "assets/plantrip/celerystalker/spr_seedpacket_celerystalker.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Celerystalker_Default",
            AssetBundleId = CompleteCollectionMod.CelerystalkerBundleId,
            SkinPrefabId = "assets/plantrip/celerystalker/models/default/plant/celerystalker.prefab",
            ScaleOverride = new Vector3(1.0f, 1.0f, 1)
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<CelerystalkerBehaviorController>();
    }
}

[RegisterTypeInIl2Cpp]
public class CelerystalkerBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // idle_down2/idle_down3 and idle2/idle3/idle4 exist in the rip but go
    // unused - like Wallnut's own idle/idle2, these are a pure visual-variety
    // pool (so a screen full of the same plant doesn't animate in lockstep),
    // not damage-tier art. He only ever needs "idle" and "idle_down".

    // Real Actions damage is a flat 100 per 0.5s tick while revealed and a
    // zombie's in range - no per-tier scaling like Endurian, since nothing
    // in the tooltip claims that for this one.
    private const int AttackDamage = 100;

    // Real RectTriggerRange is {mX:-87, mY:-50, mWidth:32, mHeight:60} - an
    // entirely negative mX range (unlike Bamboo Spartan's forward-positive
    // 10-160), confirmed live: he was punching right and needed to only
    // punch left. So negative-relative-X really does mean "left" here, same
    // as everywhere else.
    //
    // Originally treated this as a flat 0-90 band touching his own tile, but
    // the real range never touches zero at all - it's a narrow ~32-unit band
    // starting 55 units away and reaching to 87. Confirmed live too: in real
    // PvZ2 he doesn't react to a zombie the instant it's anywhere nearby,
    // only once it's essentially in the next block over.
    private const float DetectionMinDistance = 55f;
    private const float DetectionMaxDistance = 87f;

    // OnLaunchCounterTriggered fires on the plant's own ~0.5s cooldown
    // cadence (m_launchRate 50, same conversion as everywhere else in this
    // file) - both constants below are expressed as a call count at that
    // cadence rather than raw engine ticks.
    private const int RetractionDelayCalls = 6; // real RetractionDelay is 3s
    private const int UpTransitionSettleCalls = 6; // "up" clip is ~2.67s (32 frames @ 12fps)

    private bool isUp = false;
    private bool isFullyUp = false;
    private bool isAttacking = false;
    private int ticksSinceZombieSeen = 0;
    private int callsSinceUpTriggered = 0;

    #endregion

    #region Constructors

    public CelerystalkerBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    private Zombie FindZombieInRange()
    {
        return Board.m_zombies.m_list.ToList()
            .Where(z => z.mItem != null
                        && (z.mItem.mRow == Plant.mRow || z.mItem.mZombieType == ZombieType.Boss)
                        && !z.mItem.IsDeadOrDying()
                        && z.mItem.EffectedByDamage(DamageRangeFlags.Ground)
                        && z.mItem.mPosX - Plant.mX <= -DetectionMinDistance
                        && z.mItem.mPosX - Plant.mX >= -DetectionMaxDistance)
            .OrderBy(z => Plant.mX - z.mItem.mPosX)
            .Select(z => z.mItem)
            .FirstOrDefault();
    }

    // While hidden, zombies simply walk past instead of stopping to eat him -
    // deliberately CanBeTargetedBy rather than IsSpiky, so a vehicle like
    // Zomboni just drives over him too instead of getting destroyed on
    // contact the way it would against a true Spikeweed-style plant. Once
    // he's actually finished standing up (isFullyUp, not just isUp) he's a
    // completely normal target again, same as Endurian - not the instant
    // isUp flips true, or zombies start biting him mid-rise.
    public override bool CanBeTargetedBy(ZombieAttackType attackType)
    {
        return isFullyUp;
    }

    public override void OnLaunchCounterTriggered()
    {
        base.OnLaunchCounterTriggered();

        if (isUp && !isFullyUp)
        {
            // Counts real elapsed ticks since "up" started regardless of
            // whether a target is still in range, since the clip itself
            // keeps playing either way - UpTransitionSettleCalls at this
            // ~0.5s cadence covers its ~2.67s length.
            callsSinceUpTriggered++;
            if (callsSinceUpTriggered >= UpTransitionSettleCalls)
            {
                isFullyUp = true;
            }
        }

        var target = FindZombieInRange();

        if (target != null)
        {
            ticksSinceZombieSeen = 0;

            if (!isUp)
            {
                isUp = true;
                callsSinceUpTriggered = 0;
                // up's own baked exit-time transition carries into idle.
                PlayAnimation("up");
                return;
            }

            if (!isFullyUp)
            {
                // Still rising - the clip needs to actually finish before
                // he's safe to attack from or be attacked.
                return;
            }

            if (!isAttacking)
            {
                isAttacking = true;
                // attack's own baked exit-time transition carries into
                // attack_loop, which just loops until explicitly ended
                // below.
                PlayAnimation("attack");
            }

            DamageZombie(target, AttackDamage, 0);
            return;
        }

        ticksSinceZombieSeen++;

        if (isAttacking)
        {
            isAttacking = false;
            // attack_end's own baked exit-time transition carries back into
            // idle.
            PlayAnimation("attack_end");
        }
        else if (isFullyUp && ticksSinceZombieSeen >= RetractionDelayCalls)
        {
            isUp = false;
            isFullyUp = false;
            // down's own baked exit-time transition carries into idle_down.
            PlayAnimation("down");
        }
    }

    public override void Reset()
    {
        base.Reset();

        isUp = false;
        isFullyUp = false;
        isAttacking = false;
        ticksSinceZombieSeen = 0;
        callsSinceUpTriggered = 0;
    }

    #endregion
}
