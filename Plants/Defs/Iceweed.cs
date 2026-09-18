using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Util;
using UnityEngine;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class IceweedDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public IceweedDefinition(IntPtr pointer) : base(pointer)
    {
        // CONFIRMED live (zombies walked over Iceweed with zero effect, on the first real test):
        // PlantSubClass.Normal was wrong - Celery Stalker, the only other custom plant using
        // OnLaunchCounterTriggered, uses PlantSubClass.Shooter, and per Plant.Update()'s own call
        // graph, UpdateShooter (the method that actually drives mLaunchCounter/
        // OnLaunchCounterTriggered) is reached through the same per-type dispatch as
        // UpdateProductionPlant, UpdateSpikeweed, etc. - not something PlantSubClass.Normal opts
        // into. Iceweed has no projectile and isn't literally a "shooter," but Shooter is
        // apparently the only PlantSubClass value that reaches this tick at all in this
        // framework, regardless of what the plant's actual attack looks like.
        m_subClass = PlantSubClass.Shooter;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Iceweed", false);
        SetDescription("An ice variant of Spikeweed. Zombies that step on it take a little less damage than they would from Spikeweed, but come away chilled and slowed for a while.", false);
        SetTooltip("Hurts and slows down zombies that walk over it. Immune to being eaten, just like Spikeweed.", false);

        // Native Spikeweed (confirmed live via ReloadedUtils.GetPlantDefinition):
        // seedCost=100, refreshTime=750 ("Fast" recharge tier). Real PvZ2 Iceweed costs 150
        // against Spikeweed's own 100 there too (a 1.5x ratio) - applying that same ratio to
        // Replanted's real Spikeweed cost rather than reusing PvZ2's absolute number, since this
        // is a new plant for a PvZ1-logic game, not a PvZ2 port. Recharge kept identical to
        // Spikeweed - nothing suggests PvZ2 changes that, only cost.
        m_seedCost = 150;
        m_versusCost = 150;
        m_refreshTime = 750;
        m_versusBaseRefreshTime = 750;
        m_versusSuddenDeathRefreshTime = 188; // same ratio as Spikeweed's own sudden-death cut

        // Ground plant, same as Spikeweed/Potato Mine - can't be placed on a lilypad.
        m_requiresGround = true;

        // 0.5s tick cadence, matching Spikeweed's own real attack rate (confirmed consistent
        // across both PvZ1 and PvZ2 sources: 10 damage every ~0.5s) - same conversion used
        // elsewhere in this codebase (Celery Stalker's 0.5s -> m_launchRate 50).
        m_launchRate = 50;

        // CONFIRMED live (via a real crash): PvZRipImporter.ImportSeedPacket copies the seed
        // packet PNG straight to Assets/plantrip/<plant>/SPR_SeedPacket_<plant>.png - no
        // "seedpacket/" subfolder in the built asset structure (that only exists in the raw
        // export directory this gets imported FROM, not the destination). The wrong path here
        // resolved to zero assets in the bundle, which crashed LoadAssetFromAssetBundle's
        // completion callback (allAssets.First() throws on an empty sequence) - silently, since
        // it's an IL2CPP-to-managed trampoline callback, which is why this only showed up as a
        // blank seed packet icon in-game rather than an obvious error.
        SetSeedPacketImage(CompleteCollectionMod.IceweedBundleId, "assets/plantrip/iceweed/spr_seedpacket_iceweed.png");
        SetPreviewImage(CompleteCollectionMod.IceweedBundleId, "assets/plantrip/iceweed/spr_seedpacket_iceweed.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Iceweed_Default",
            AssetBundleId = CompleteCollectionMod.IceweedBundleId,
            SkinPrefabId = "assets/plantrip/iceweed/models/default/plant/iceweed.prefab",
            ScaleOverride = new Vector3(1.0f, 1.0f, 1)
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // All 11 of Iceweed's real ripped sound bites are bite/chomp variants (no dedicated
        // "spike jab" cue exists in the rip) - loaded once here per plant TYPE, not per planted
        // instance: CustomPlantBehaviorController's own RegisterSoundPool/mSoundPools is a
        // per-instance dictionary meant to be populated fresh by each instance's own constructor,
        // which would mean re-loading these from the asset bundle (an async callback) every
        // single time a player plants another Iceweed - wasteful, and racy against the first
        // attack tick firing before the load callback returns. Loading once into this static list
        // and having the behavior controller play directly from it (see
        // IceweedBehaviorController.PlayRandomBiteSound) avoids both problems.
        foreach (var soundAsset in IceweedBehaviorController.BiteSoundAssetPaths)
        {
            RegistryBridge.LoadAssetFromAssetBundle<AudioClip>(CompleteCollectionMod.IceweedBundleId, soundAsset,
                clip => IceweedBehaviorController.BiteSounds.Add(clip));
        }

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<IceweedBehaviorController>();
    }
}

[RegisterTypeInIl2Cpp]
public class IceweedBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // Real PvZ2 Iceweed deals 8 damage per tick against Spikeweed's 10 (a 0.8x ratio) - the
    // lower per-tick number is intentional, not a nerf we're introducing: the chill applied
    // alongside it (see below) keeps a zombie in the damage zone longer, which is how PopCap
    // balanced it back up to roughly Spikeweed's own total output. Scaled off Replanted's own
    // native Spikeweed damage would be preferable, but that constant isn't exposed anywhere
    // reflectable (not a PlantDefinition field, unlike cost/recharge) - 8 is PvZ2's real number,
    // which independent research shows matches original PvZ1's own Spikeweed tick damage (10)
    // almost exactly, so scaling off it here is a reasonable stand-in for Replanted's own value.
    private const int AttackDamage = 8;

    // DamageFlags.Freeze is consumed directly inside native Zombie.TakeBodyDamage/TakeHelmDamage
    // - ApplyChill(false) (partial chill/slow, not the full-freeze "true" path Ice Trap uses) -
    // fires automatically as a side effect of any damage carrying this flag. Spike bypasses
    // whatever native Spikeweed's own damage already bypasses (metal helmet bonus etc.) - no new
    // status-effect system needed, just the two flags together.
    private const DamageFlags IceweedDamageFlags = DamageFlags.Spike | DamageFlags.Freeze;

    // PvZ1-style full-tile passive hazard, not a PvZ2-style directional attack-rect: any zombie
    // whose current grid column matches this plant's own column, on the same row, is standing on
    // it and takes damage - matching how native Spikeweed actually works (Zombie.
    // IsStandingOnSpikeweed() is a tile match, not a rect overlap), not Celery Stalker's PvZ2-
    // sourced asymmetric rect (that plant has no PvZ1 equivalent to imitate, this one does).
    private bool isAttacking = false;

    // Real ripped clip/state names (PvZRipImporter builds one AnimatorController state per
    // export-time folder name, unchanged) - "stage1_idle1"/"stage1_idle2" are a pure visual-
    // variety pair like Celery Stalker's own unused idle2/idle3/idle4 (only stage1_idle1 is
    // actually used; see this plant's Unity-side Fix method for why no attack_loop state exists
    // to target instead - stage1_attack is forced to loop in the Editor build step so this one
    // PlayAnimation call is all that's needed to start it, same as an idle transition).
    private const string IdleState = "stage1_idle1";
    private const string AttackState = "stage1_attack";

    // Loaded once per plant type by IceweedDefinition's constructor - see that field's own
    // comment for why this can't be the per-instance mSoundPools/RegisterSoundPool mechanism.
    public static readonly string[] BiteSoundAssetPaths =
    {
        "assets/plantrip/iceweed/sounds/134858001.ogg",
        "assets/plantrip/iceweed/sounds/135271060.ogg",
        "assets/plantrip/iceweed/sounds/137219562.ogg",
        "assets/plantrip/iceweed/sounds/180532437.ogg",
        "assets/plantrip/iceweed/sounds/383543868.ogg",
        "assets/plantrip/iceweed/sounds/416326568.ogg",
        "assets/plantrip/iceweed/sounds/587642561.ogg",
        "assets/plantrip/iceweed/sounds/611417781.ogg",
        "assets/plantrip/iceweed/sounds/660685521.ogg",
        "assets/plantrip/iceweed/sounds/679122514.ogg",
        "assets/plantrip/iceweed/sounds/776784313.ogg",
    };
    public static readonly List<AudioClip> BiteSounds = new();

    #endregion

    #region Constructors

    public IceweedBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    // Confirmed live: Il2CppInterop logs "has unsupported return type" for ANY method
    // returning a plain C# List<T> on a [RegisterTypeInIl2Cpp] type, private or not - not
    // worth risking whether that's cosmetic-only or actually breaks the call, when filling a
    // caller-owned list instead sidesteps the whole question. Celery Stalker's own
    // FindZombieInRange never hit this because it returns a single Zombie, not a list - this
    // plant's only difference is being a genuine multi-target AoE (matching real Spikeweed
    // hitting every zombie standing on it, not just the nearest one).
    private readonly List<Zombie> zombiesOnTileScratch = new();

    // CONFIRMED live (via NavDiag) that Board.PixelToGridX's column bucketing is much coarser
    // than the plant's real footprint - zombies matched nearly a full tile-width (~80px) before
    // visually reaching the plant. Plant.GetPlantRect()/Zombie.GetZombieRect() (the same
    // rect-overlap technique Celery Stalker's own FindZombieInRange already uses, and what
    // native's own CanTargetPlant/SquishAllInSquare machinery is built on) is the real, precise
    // footprint instead of a guessed tile width - no column math needed at all.
    private void FindZombiesOnTile(List<Zombie> found)
    {
        found.Clear();
        Rect plantRect = Plant.GetPlantRect();

        foreach (var z in Board.m_zombies.m_list)
        {
            var zombie = z.mItem;
            if (zombie == null
                || zombie.mRow != Plant.mRow
                || zombie.IsDeadOrDying()
                || !zombie.EffectedByDamage(DamageRangeFlags.Ground))
            {
                continue;
            }

            if (plantRect.Overlaps(zombie.GetZombieRect()))
            {
                found.Add(zombie);
            }
        }
    }

    // Immune to being eaten, unconditionally - same as native Spikeweed (zombies just walk over
    // it, never stopping to bite). Separate from IsSpiky() below on purpose - see
    // CustomPlantBehaviorController.CanBeTargetedBy's own comment for why the two hooks stay
    // independent. No rise/hide state machine needed here (unlike Celery Stalker) - Iceweed
    // never stops being immune.
    public override bool CanBeTargetedBy(ZombieAttackType attackType)
    {
        return false;
    }

    // Real Spikeweed's other defining trait: a Zomboni (or similar vehicle) driving over it is
    // instantly destroyed instead of squishing it flat - one native-bundled effect (see
    // CustomPlantBehaviorController.IsSpiky's own comment; Zombie.SquishAllInSquare calls both
    // IsSpiky() and Plant.Squish() together), so no extra code is needed here for "the plant also
    // dies" - that's already what native does whenever this returns true.
    public override bool IsSpiky()
    {
        return true;
    }

    public override void OnLaunchCounterTriggered()
    {
        base.OnLaunchCounterTriggered();

        FindZombiesOnTile(zombiesOnTileScratch);
        var targets = zombiesOnTileScratch;

        if (targets.Count > 0)
        {
            if (!isAttacking)
            {
                isAttacking = true;
                // stage1_attack is forced to loop in the Editor build step (see this plant's
                // Fix method in PvZBundleBuilder.cs), so one call here keeps it visually poking
                // for as long as a zombie remains, without needing to re-trigger it every tick.
                PlayAnimation(AttackState);
            }

            foreach (var target in targets)
            {
                DamageZombie(target, AttackDamage, IceweedDamageFlags);
            }

            // Every sound in the rip is a bite/chomp variant with no meaningful distinction
            // between them, so picking one at random each tick (rather than baking specific
            // Animation Events into the clip's timeline) gives the same periodic effect for a
            // fraction of the authoring work.
            if (BiteSounds.Count > 0)
            {
                PlayAudio(BiteSounds[UnityEngine.Random.Range(0, BiteSounds.Count)]);
            }

            return;
        }

        if (isAttacking)
        {
            isAttacking = false;
            PlayAnimation(IdleState);
        }
    }

    public override void Reset()
    {
        base.Reset();

        isAttacking = false;
    }

    #endregion
}
