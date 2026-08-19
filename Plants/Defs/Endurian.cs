using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Content.Plants.Mint;
using UnityEngine;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class EndurianDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public EndurianDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Endurian", false);
        SetDescription("Endurians are defensive plants that deal damage to zombies attacking them.", false);
        SetTooltip("Endurian jabs back at any zombie touching him. The more damage he's taken, the angrier - and stronger - he gets.", false);

        // Real EndurianDefault@PlantProperties: Cost 100, Hitpoints 3000,
        // PacketCooldown 15 - no shield split like Bamboo Spartan, just one
        // flat health pool with three purely-cosmetic damage-appearance
        // tiers (see BehaviorController).
        m_seedCost = 100;
        m_versusCost = 100;
        m_health = 3000;

        m_refreshTime = 1500;
        m_versusBaseRefreshTime = 1500;
        m_versusSuddenDeathRefreshTime = 375;

        // Real spike-damage action has a flat 1s cooldown; Bamboo Spartan's
        // real 0.5s maps to m_launchRate 50 (~100 ticks/sec), so 1s -> ~100.
        m_launchRate = 100;

        m_mintFamily = MintFamily.ReinforceMint;

        SetSeedPacketImage(CompleteCollectionMod.EndurianBundleId, "assets/plantrip/endurian/spr_seedpacket_endurian.png");
        SetPreviewImage(CompleteCollectionMod.EndurianBundleId, "assets/plantrip/endurian/spr_seedpacket_endurian.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Endurian_Default",
            AssetBundleId = CompleteCollectionMod.EndurianBundleId,
            SkinPrefabId = "assets/plantrip/endurian/models/default/plant/endurian.prefab",
            ScaleOverride = new Vector3(1.0f, 1.0f, 1)
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<EndurianBehaviorController>();
    }
}

[RegisterTypeInIl2Cpp]
public class EndurianBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // Real Hitpoints is a flat 3000 with no shield split - just three purely
    // cosmetic damage-appearance tiers matching the almanac card's stated
    // thresholds (>2000, >1000, >0). Each tier has its own idle pose and its
    // own intro/loop/outro attack animation set
    // ("attack_start/loop/end_damage[2|3]" - these keep their original ripped
    // names).
    //
    // The idle-pose states were renamed at the asset level (frame-rip
    // folders + AnimatorController, see PvZBundleBuilder.FixEndurianAnimatorController)
    // from "damage/damage2/damage3" to "idle/idle2/idle3": the game's own
    // periodic idle-refresh call expects literally-named "idle"/"idle2"/
    // "idle3" states for the three health tiers, and was instead snapping him
    // to whatever was named "idle" when we used that name for something else.
    // The genuine plant-food-upgraded look formerly at "idle"/"idle2" (never
    // used here) was renamed to "armor"/"armor2" to free up the name.
    private const int Tier1Threshold = 2000;
    private const int Tier2Threshold = 1000;

    private static readonly string[] IdleLabels = { "idle", "idle2", "idle3" };
    private static readonly string[] AttackStartLabels = { "attack_start_damage", "attack_start_damage2", "attack_start_damage3" };
    private static readonly string[] AttackEndLabels = { "attack_end_damage", "attack_end_damage2", "attack_end_damage3" };

    // Real Endurian Actions data only gives a single flat Damage:20 with no
    // per-tier value anywhere (the "damage2"/"damage3" PlantStats entries
    // are just a generic almanac sword-icon count shared across dozens of
    // unrelated plants, not an Endurian-specific scaling mechanic) - but the
    // tooltip ("the more damage he's taken, the angrier - and stronger - he
    // gets") is explicit that it should scale, so this is our own
    // progression. Tier 1 is anchored to Spikeweed's real damage (10, same
    // "reactive touch-range spike" archetype) rather than Endurian's own
    // flat 20, per design call.
    private static readonly int[] TierDamage = { 10, 20, 30 };

    // Real trigger rect is {mX:-25, width:50} - roughly centered on
    // Endurian's own tile in both directions, matching the almanac card's
    // "Touch" range: unlike Bamboo Spartan he doesn't reach out for
    // anything, he only ever hits whatever's already standing on/against
    // him.
    private const float TouchRange = 35f;

    private bool isAttacking = false;
    private int idleTierShown = -1;

    #endregion

    #region Constructors

    public EndurianBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    private int CurrentTier =>
        Plant.mPlantHealth > Tier1Threshold ? 0 :
        Plant.mPlantHealth > Tier2Threshold ? 1 : 2;

    private Zombie FindTouchingZombie()
    {
        return Board.m_zombies.m_list.ToList()
            .Where(z => z.mItem != null
                        && (z.mItem.mRow == Plant.mRow || z.mItem.mZombieType == ZombieType.Boss)
                        && !z.mItem.IsDeadOrDying()
                        && z.mItem.EffectedByDamage(DamageRangeFlags.Ground)
                        && Math.Abs(z.mItem.mPosX - Plant.mX) <= TouchRange)
            .OrderBy(z => Math.Abs(z.mItem.mPosX - Plant.mX))
            .Select(z => z.mItem)
            .FirstOrDefault();
    }

    public override void OnLaunchCounterTriggered()
    {
        base.OnLaunchCounterTriggered();

        // Zombie-presence check lives here (fires on the plant's own
        // ~1s cooldown cadence, per m_launchRate) rather than in
        // PostPlantUpdate (every single frame) - querying/sorting the full
        // zombie list 60x/sec was the actual source of the choppiness, not
        // just wasted work.
        int tier = CurrentTier;
        var target = FindTouchingZombie();

        if (target != null)
        {
            if (!isAttacking)
            {
                isAttacking = true;
                // attack_start_damage[N]'s own baked exit-time transition
                // carries this into the matching attack_loop_damage[N],
                // which just loops until we explicitly end it below.
                PlayAnimation(AttackStartLabels[tier]);
            }

            DamageZombie(target, TierDamage[tier], 0);
        }
        else if (isAttacking)
        {
            isAttacking = false;
            idleTierShown = tier;
            // attack_end_damage[N]'s own baked exit-time transition carries
            // this into the matching idle tier once it finishes.
            PlayAnimation(AttackEndLabels[tier]);
        }
        else if (tier != idleTierShown)
        {
            // Health dropped a tier from some other damage source (splash,
            // etc.) with nobody actually touching him at the time - nothing
            // else would ever notice and re-assert the correct idle pose.
            // Cheap to check here since this whole method only runs on the
            // ~1s cooldown cadence, not every frame.
            idleTierShown = tier;
            PlayAnimation(IdleLabels[tier]);
        }
    }

    public override void Reset()
    {
        base.Reset();

        isAttacking = false;
        idleTierShown = -1;
    }

    #endregion
}
