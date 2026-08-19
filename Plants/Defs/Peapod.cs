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
public class PeapodDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public PeapodDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        // Shoots or rolls straight along the lane, which the roof's slope stops: not for a roof level's conveyor belt (see m_notOnRoof).
        m_notOnRoof = true;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Peapod", false);
        SetDescription("Peapod is a pea shooter with a sense of humour. He has studied quantum spatial mechanics in clown college, and it shows.", false);
        SetTooltip("Peapod shoots peas down his lane. Plant him on himself again to grow another head, up to five.", false);

        // PvZ2 PeapodDefault: Cost 125, Hitpoints 300, PacketCooldown 5. PvZ2's Peashooter is 100,
        // so 125 is the same 1.25x step; Hitpoints 300 equal Replanted's Peashooter.
        // PacketCooldown 5 -> m_refreshTime 500 (~100 ticks/sec), like FirePeashooter.
        m_seedCost = 125;
        m_versusCost = 125;
        m_health = 300;

        m_refreshTime = 500;
        m_versusBaseRefreshTime = 500;
        m_versusSuddenDeathRefreshTime = 125;

        // PvZ2's one real projectile Action (PeaDefault) cools down 1.35-1.5 s - the same as
        // FirePeashooter's, which uses 140. (The data's second Action is Plant Food's giant pea;
        // this mod has no Plant Food.)
        m_launchRate = 140;

        // PvZ2 family: Appease-mint.
        m_mintFamily = MintFamily.AppeaseMint;

        // PvZ2 "CanPlantAgainToUpgrade": plant Peapod on an existing Peapod to add a head, up to
        // five heads. Each planting costs the full price and recharges the packet like any other;
        // the extra plant merges into the existing one (CoreLib PlantStacking), health stays as
        // it is (PvZ2's data has one Hitpoints value for every head count).
        m_maxStack = 5;

        SetSeedPacketImage(CompleteCollectionMod.PeapodBundleId, "assets/plantrip/peapod/spr_seedpacket_peapod.png");
        SetPreviewImage(CompleteCollectionMod.PeapodBundleId, "assets/plantrip/peapod/spr_seedpacket_peapod.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Peapod_Default",
            AssetBundleId = CompleteCollectionMod.PeapodBundleId,
            SkinPrefabId = "assets/plantrip/peapod/models/default/plant/peapod.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: Peapod's attack sounds are PlaySound
        // Animation Events baked into its clips from the PAM (see PvZRipTools plant_sounds.py).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<PeapodBehaviorController>();
    }
}

// Stackable (CustomPlantDefinition.m_maxStack = 5): one plant, 1..5 heads. The PvZ2 art has a clip
// pair per head count - idle / idle2..idle5 and attack / "attack 2".."attack 5" (clip and Animator
// state names contain a space) - and the controller plays the pair that matches StackCount; the
// attack clips return to the matching idle (PvZBundleBuilder.ApplyPlantAnimatorFixes).
// Every attack clip carries one ExecuteAttack Animation Event per PAM use_actionN marker (one per
// head, on that head's own frame) and the Play_Throw sound once, on the clip's first marker (all
// baked by the pipeline), so the pea and the sound follow the game's animation speed. Each event
// here spawns ONE native pea.
[RegisterTypeInIl2Cpp]
public class PeapodBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    private static readonly string[] IdleClips = { "idle", "idle2", "idle3", "idle4", "idle5" };
    private static readonly string[] AttackClips = { "attack", "attack 2", "attack 3", "attack 4", "attack 5" };

    // Same nudge FirePeashooter uses (identical PvZ2 spawn offset, 35/-32). Tune by eye in game.
    private const float MouthHeightOffset = 23f;

    // Each later pea of a volley leaves this much lower than the one before it (this engine's Y
    // grows downward, so + is lower), so the peas of a multi-head volley are visibly separate.
    // Tune by eye in game.
    private const float HeadDropPerPea = 10f;

    // ExecuteAttack events seen since the current volley started (the Nth one is the Nth pea).
    private int eventsThisAttack;

    private int tickCounter;
    private int shownHeads = -1;

    #endregion

    #region Constructors

    public PeapodBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    private int Heads => Math.Clamp(StackCount, 1, IdleClips.Length);

    // Visual-only: show the idle clip that matches the head count (also right after a save/load,
    // where the count comes back with the plant). Waits for the skin so the request isn't dropped,
    // and never cuts a volley short - the attack clip hands over to the old idle, and the new one
    // is picked up the next time this runs.
    public override void PostPlantUpdate()
    {
        base.PostPlantUpdate();

        tickCounter++;
        if (tickCounter % 5 != 0)
        {
            return;
        }

        int heads = Heads;
        if (heads == shownHeads || !IsSkinApplied || IsAttackClipActive())
        {
            return;
        }

        shownHeads = heads;
        PlayAnimation(IdleClips[heads - 1]);
    }

    private bool IsAttackClipActive()
    {
        foreach (var clip in AttackClips)
        {
            if (IsCustomAnimationActive(clip))
            {
                return true;
            }
        }

        return false;
    }

    public override void OnLaunchCounterTriggered()
    {
        base.OnLaunchCounterTriggered();

        if (FindTarget() == null)
        {
            return;
        }

        eventsThisAttack = 0;
        PlayAnimation(AttackClips[Heads - 1]);
    }

    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        var target = FindTarget();
        if (target == null)
        {
            return;
        }

        // A real native pea, like FirePeashooter's (see the comments there for why the mouth
        // offset and mDamageRangeFlags are needed with SpawnProjectile).
        var projectile = SpawnProjectile(ProjectileType.PeashooterPea);
        projectile.mPosY += MouthHeightOffset + eventsThisAttack * HeadDropPerPea;
        projectile.mTargetZombieID = target.mRelatedZombieID;
        projectile.mDamageRangeFlags = DamageRangeFlags.Ground;
        eventsThisAttack++;
    }

    // Nearest living zombie ahead of the plant in its lane.
    private Zombie FindTarget()
    {
        return ZombieSnapshot.Items(Board)
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
        shownHeads = -1;
        eventsThisAttack = 0;
    }

    #endregion
}
