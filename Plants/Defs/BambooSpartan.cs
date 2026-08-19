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
public class BambooSpartanDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public BambooSpartanDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Bamboo Spartan", false);
        SetDescription("A legendary warrior who fights behind his shield until it breaks - then abandons defense entirely for pure offense.", false);
        SetTooltip("Bamboo Spartan jabs zombies ahead of him with his spear. His shield absorbs 1400 damage before breaking; without it, his attacks hit much harder.", false);

        // Real BambooSpartanDefault@PlantProperties: Cost 175, PacketCooldown 15
        // (epic-tier - much slower than our other plants' tier-5, e.g.
        // Bloomerang/Bonkchoy/FirePeashooter all use PacketCooldown 5).
        m_seedCost = 175;
        m_versusCost = 175;
        // TotalHitPoints in the real data is 2000, not the base Hitpoints (600) -
        // that's 600 real health + 1400 shield combined into ONE native health
        // pool. See BambooSpartanBehaviorController's comment for why this
        // matters: reactively "healing back" absorbed damage in PostPlantUpdate
        // races against the native death check (both happen inside the same
        // Plant.Update() call our hook only observes AFTER it returns), so a
        // single lethal hit could kill him before we ever got a chance to
        // intervene. Giving him the full 2000 up front sidesteps that entirely -
        // the native system just handles damage/death normally, and we merely
        // watch for health crossing the 600 threshold to react (shield-break
        // animation, damage bump), never trying to prevent anything.
        m_health = 2000;

        // PacketCooldown 5 -> our other plants' m_refreshTime 500 (~100
        // ticks/sec), so PacketCooldown 15 -> ~1500. Lands in the "Medium"
        // recharge-speed almanac bucket (CustomPlantDefinition.LoadFrom's
        // m_refreshTime > 1000 threshold), which fits an epic-rarity plant.
        m_refreshTime = 1500;
        m_versusBaseRefreshTime = 1500;
        m_versusSuddenDeathRefreshTime = 375;

        // Real jab cooldown is 0.5s; Bloomerang's real 2.85-3s maps to
        // m_launchRate 300 (~100 ticks/sec), so ~50 here.
        m_launchRate = 50;

        SetSeedPacketImage(CompleteCollectionMod.BambooSpartanBundleId, "assets/plantrip/bamboospartan/spr_seedpacket_bamboospartan.png");
        SetPreviewImage(CompleteCollectionMod.BambooSpartanBundleId, "assets/plantrip/bamboospartan/spr_seedpacket_bamboospartan.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "BambooSpartan_Default",
            AssetBundleId = CompleteCollectionMod.BambooSpartanBundleId,
            SkinPrefabId = "assets/plantrip/bamboospartan/models/default/plant/bamboospartan.prefab",
            // No real shipped asset to calibrate against - starting at 1.0
            // (matching FirePeashooter's post-correction value) rather than
            // blindly copying Bloomerang's 1.35 again. Needs visual tuning once
            // built.
            ScaleOverride = new Vector3(1.0f, 1.0f, 1)
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<BambooSpartanBehaviorController>();
    }
}

[RegisterTypeInIl2Cpp]
public class BambooSpartanBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // PvZReCoreLib doesn't expose a generic "Shield" power system, so instead of
    // trying to intercept/cancel damage (which raced against the native death
    // check - see m_health's comment in the Definition above), the plant just
    // has its full real 2000 combined health from the start, and this only
    // reacts once it crosses the 600 threshold (i.e. the 1400 "shield" portion
    // is exhausted): trigger shield-break once, then fight unprotected at
    // higher damage for the remainder.
    private const int ShieldBrokenHealthThreshold = 600;
    private bool hasShield = true;

    #endregion

    #region Constructors

    public BambooSpartanBehaviorController(IntPtr pointer) : base(pointer)
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
        // Real trigger rect is forward-only (mX 10 to 160, positive = toward
        // zombies) - Bamboo Spartan only ever jabs ahead of him, never behind,
        // unlike Bonkchoy's front-and-back punches. Gate is "< 0" not "< 10":
        // the real rect's "mX: 10" is just the trigger zone's own local offset,
        // not a minimum-distance gate - a zombie standing right on the same tile
        // (difference near 0) still overlaps that zone (zombies have their own
        // width) and must stay a valid target, or he can never fight back
        // against one that starts adjacent/on top of him. Only strictly-behind
        // zombies (negative difference) are excluded, keeping the attack
        // forward-only.
        zombies.RemoveAll(z => z.mItem.mPosX - Plant.mX < 0f || z.mItem.mPosX - Plant.mX > 160f);

        if (!zombies.Any())
        {
            return;
        }

        zombies.Sort((a, b) => a.mItem.mPosX.CompareTo(b.mItem.mPosX));
        var target = zombies.First().mItem;

        // Real base damage is 35 (shielded "spear jab"); real
        // BattleTranceDamageMultiplier 300 means 3x (105) once the shield is
        // broken and he's fighting unprotected.
        int damage = hasShield ? 35 : 105;
        DamageZombie(target, damage, 0);

        // battle_trance_attack's bulk-generated transition currently returns to
        // the default "idle" (shielded) state instead of "battle_trance_idle1" -
        // needs rewiring in the Animator window once this plant's bundle is
        // built. Doesn't affect the logic here since hasShield is tracked in C#,
        // not read back from the animator's current state - only the visual
        // return-to-idle pose is affected until that's fixed.
        PlayAnimation(hasShield ? "attack" : "battle_trance_attack");
    }

    public override void PostPlantUpdate()
    {
        base.PostPlantUpdate();

        if (hasShield && Plant.mPlantHealth <= ShieldBrokenHealthThreshold)
        {
            hasShield = false;
            // Same manual-rewiring note as battle_trance_attack above - this
            // should land in "battle_trance_idle1", not the bulk default.
            PlayAnimation("battle_trance_shield_break");
        }
    }

    public override void Reset()
    {
        base.Reset();

        hasShield = true;
    }

    #endregion
}
