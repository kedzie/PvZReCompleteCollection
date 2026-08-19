using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Content.Plants.Mint;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection2.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class PeanutDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public PeanutDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        // Shoots or rolls straight along the lane, which the roof's slope stops: not for a roof level's conveyor belt (see m_notOnRoof).
        m_notOnRoof = true;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Peanut", false);
        SetDescription("Peanut shoots peas and blocks zombies. He can't help but miss the days when he was the most popular nut around.", false);
        SetTooltip("Peanut is a Wall-nut that fires two peas down his lane.", false);

        // PvZ2 PeanutDefault: Cost 150, Hitpoints 4000, PacketCooldown 15. Hitpoints equal
        // Replanted's own Wall-nut (4000) and the cost equals Wall-nut 50 + Peashooter 100, so
        // all of it carries over 1:1. PacketCooldown 15 -> m_refreshTime 1500 (~100 ticks/sec,
        // same mapping as Bamboo Spartan/Endurian).
        // Nut: enemy Ladder Zombies place their ladder on it like on a Wall-nut (see CustomPlantDefinition.m_countsAsNut).
        m_countsAsNut = true;

        m_seedCost = 150;
        m_versusCost = 150;
        m_health = 4000;

        m_refreshTime = 1500;
        m_versusBaseRefreshTime = 1500;
        m_versusSuddenDeathRefreshTime = 375;

        // PvZ2 has two identical projectile Actions (PeaDefault, cooldown 1.30-1.333 s); the two
        // attack clips ("attack" + "attack2", 19 + 21 frames at 30 fps = 1.33 s) play back to
        // back, one pea each. 135 ticks keeps a new cycle from starting before the chain is over.
        m_launchRate = 135;

        // PvZ2 family: Reinforce-mint.
        m_mintFamily = MintFamily.ReinforceMint;

        SetSeedPacketImage(CompleteCollectionMod.PeanutBundleId, "assets/plantrip/peanut/spr_seedpacket_peanut.png");
        SetPreviewImage(CompleteCollectionMod.PeanutBundleId, "assets/plantrip/peanut/spr_seedpacket_peanut.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Peanut_Default",
            AssetBundleId = CompleteCollectionMod.PeanutBundleId,
            SkinPrefabId = "assets/plantrip/peanut/models/default/plant/peanut.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: Peanut's attack sounds are PlaySound
        // Animation Events baked into its clips from the PAM (see PvZRipTools plant_sounds.py).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<PeanutBehaviorController>();
    }
}

// Clips (30 fps, see PvZBundleBuilder.SampleRateOverrides): four damage tiers of idle and of attack.
// Full health and tier 1 shoot with a two-clip chain ("attack" -> "attack2" -> idle, wired in the
// Animator by PvZBundleBuilder.ApplyPlantAnimatorFixes), each clip carrying one ExecuteAttack event
// on its PAM use_action frame = one pea. Tiers 2 and 3 only have a single attack clip in the PvZ2
// art, so there one event spawns both peas (the second a little behind the first).
[RegisterTypeInIl2Cpp]
public class PeanutBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    private static readonly string[] IdleClips = { "idle", "idle_damage1", "idle_damage2", "idle_damage3" };
    private static readonly string[] AttackClips = { "attack", "attack_damage1", "attack_damage2", "attack_damage3" };

    // Same nudge as FirePeashooter (PvZ2 spawn offset y -32 there; Peanut's is -37, a few px higher).
    // Tune by eye in game.
    private const float MouthHeightOffset = 19f;

    // Second pea of a single-clip tier, behind the first so the two are visibly separate.
    private const float SecondPeaStagger = 20f;

    // The second pea of every volley leaves from lower down than the first (this engine's Y grows
    // downward, so + is lower). Tune by eye in game.
    private const float SecondPeaDrop = 16f;

    // ExecuteAttack events seen since the current attack started: in the two-clip chain
    // ("attack" -> "attack2") the first event is the upper pea and the second the lower one.
    private int eventsThisAttack;

    private int tickCounter;
    private int shownTier = -1;

    #endregion

    #region Constructors

    public PeanutBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    // Four looks: >75 %, >50 %, >25 % and below of max health.
    private int CurrentTier
    {
        get
        {
            int health = Plant.mPlantHealth * 4;
            int max = Plant.mPlantMaxHealth;
            return health > max * 3 ? 0 : health > max * 2 ? 1 : health > max ? 2 : 3;
        }
    }

    // Visual-only check, a few times a second.
    public override void PostPlantUpdate()
    {
        base.PostPlantUpdate();

        tickCounter++;
        if (tickCounter % 10 != 0)
        {
            return;
        }

        int tier = CurrentTier;
        if (tier == shownTier)
        {
            return;
        }

        shownTier = tier;
        PlayAnimation(IdleClips[tier]);
    }

    public override void OnLaunchCounterTriggered()
    {
        base.OnLaunchCounterTriggered();

        if (FindTarget() == null)
        {
            return;
        }

        eventsThisAttack = 0;
        PlayAnimation(AttackClips[CurrentTier]);
    }

    // One ExecuteAttack event per attack clip (baked on the PAM's use_action frame).
    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        var target = FindTarget();
        if (target == null)
        {
            return;
        }

        if (CurrentTier >= 2)
        {
            // Single attack clip: one event, both peas.
            SpawnPea(target, 0f, 0f);
            SpawnPea(target, -SecondPeaStagger, SecondPeaDrop);
        }
        else
        {
            SpawnPea(target, 0f, eventsThisAttack == 0 ? 0f : SecondPeaDrop);
            eventsThisAttack++;
        }
    }

    private void SpawnPea(Zombie target, float xOffset, float yDrop)
    {
        // A real native pea, like FirePeashooter's (see the comments there for why the mouth
        // offset and mDamageRangeFlags are needed with SpawnProjectile).
        var projectile = SpawnProjectile(ProjectileType.PeashooterPea);
        projectile.mPosX += xOffset;
        projectile.mPosY += MouthHeightOffset + yDrop;
        projectile.mTargetZombieID = target.mRelatedZombieID;
        projectile.mDamageRangeFlags = DamageRangeFlags.Ground;
    }

    // Nearest living zombie ahead of the plant in its lane.
    private Zombie FindTarget()
    {
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

        tickCounter = 0;
        shownTier = -1;
        eventsThisAttack = 0;
    }

    #endregion
}
