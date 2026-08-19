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
public class NightshadeDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public NightshadeDefinition(IntPtr pointer) : base(pointer)
    {
        // Behaviour entirely from the controller (reach test, whack), like Spring Bean.
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Nightshade", false);
        SetDescription($"Nightshade lashes out with its petals at zombies right in front of it - a heavy blow that hurts everything in reach. Each whack takes something out of it: after {NightshadeBehaviorController.WhackCount} whacks it is spent.", false);
        SetTooltip($"Whacks the zombies in front of it for {NightshadeBehaviorController.WhackDamage} damage. Spent after {NightshadeBehaviorController.WhackCount} whacks (it looks more worn after each one).", false);

        // PvZ2 NightshadeDefault: Cost 75, Hitpoints 300, PacketCooldown 5, MeleeDamage 200 (its "special" action:
        // a rect trigger x -25..+95 in front of the plant).
        // PacketCooldown 5 -> m_refreshTime 500 (~100 ticks/sec, same mapping as every other port here).
        m_seedCost = 75;
        m_versusCost = 75;
        m_health = 300;

        m_refreshTime = 500;
        m_versusBaseRefreshTime = 500;
        m_versusSuddenDeathRefreshTime = 125;

        // PvZ2 family: Conceal-mint.
        m_mintFamily = MintFamily.ConcealMint;

        SetSeedPacketImage(CompleteCollectionMod.NightshadeBundleId, "assets/plantrip/nightshade/spr_seedpacket_nightshade.png");
        SetPreviewImage(CompleteCollectionMod.NightshadeBundleId, "assets/plantrip/nightshade/spr_seedpacket_nightshade.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Nightshade_Default",
            AssetBundleId = CompleteCollectionMod.NightshadeBundleId,
            SkinPrefabId = "assets/plantrip/nightshade/models/default/plant/nightshade.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the whack sounds are PlaySound Animation Events baked into the
        // "attack*" clips from the PAM (PvZRipTools plant_sounds.py).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<NightshadeBehaviorController>();
    }
}

// Clips (30 fps). The PAM has no damage art for it; PvZRipTools make_damage_variants.py derives it (a colour grade
// and some worn patches, wear levels 0 (as ripped) .. 3 (spent)):
//   "idle" (loops, wear 0; the Animator default)
//   a whack at wear level N is the clip "attack" (N = 0) / "attack_damage1" / "attack_damage2": 95 frames with one
//   use_action marker (+40, the petals landing). The clip's art changes to wear N + 1 on the frame after that hit, and
//   it ends in "idle_damageN+1" (loops). After the third whack (wear 3) there is no idle: the plant is removed by the
//   OwnerDie event baked near the end of "attack_damage2".
//
// PvZ2 data: MeleeDamage 200, trigger rect x -25..+95 (in front of the plant). The whack starts when a zombie is in
// that reach and hits everything in it when the marker's frame comes. Counted in game ticks, so it follows the game
// speed.
[RegisterTypeInIl2Cpp]
public class NightshadeBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    public const int WhackCount = 3;
    public const int WhackDamage = 200;

    // Reach: -25..+95 of the plant's origin in PvZ2 -> a zombie whose body overlaps this strip of his own lane.
    private const float ReachLeft = 10f;
    private const float ReachRight = 55f;

    // "attack" is 95 frames at 30 fps = 317 ticks; the next whack may start after that.
    private const int WhackClipTicks = 317 + 10;
    private const int CheckInterval = 5;

    private int whacksDone;
    private int ticksSinceStart = WhackClipTicks;

    #endregion

    #region Constructors

    public NightshadeBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override void PostPlantUpdate()
    {
        base.PostPlantUpdate();

        if (!IsSkinApplied || whacksDone >= WhackCount)
        {
            return;
        }

        ticksSinceStart++;
        if (ticksSinceStart < WhackClipTicks || ticksSinceStart % CheckInterval != 0)
        {
            return;
        }

        if (AnyZombieInReach())
        {
            ticksSinceStart = 0;
            PlayAnimation(whacksDone == 0 ? "attack" : $"attack_damage{whacksDone}");
        }
    }

    // ExecuteAttack event on the clip's use_action frame.
    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        if (ticksSinceStart >= WhackClipTicks || whacksDone >= WhackCount)
        {
            return;
        }

        foreach (var zombie in ZombiesInReach())
        {
            zombie.TakeDamage(WhackDamage, DamageFlags.HitsShieldAndBody);
        }

        // The whack is used up even when nothing was left to hit: the clip's art changes wear level on this frame.
        whacksDone++;
    }

    private bool AnyZombieInReach()
    {
        return ZombiesInReach().Count > 0;
    }

    private List<Zombie> ZombiesInReach()
    {
        var result = new List<Zombie>();
        if (Board?.m_zombies?.m_list == null)
        {
            return result;
        }

        var plantRect = Plant.GetPlantRect();
        float left = plantRect.x - ReachLeft;
        float right = plantRect.x + plantRect.width + ReachRight;

        foreach (var entry in Board.m_zombies.m_list.ToList())
        {
            var zombie = entry.mItem;
            if (zombie == null
                || zombie.IsDeadOrDying()
                || zombie.mMindControlled
                || zombie.mRow != Plant.mRow
                || zombie.mZombieType == ZombieType.Gravestone
                || !zombie.EffectedByDamage(DamageRangeFlags.Ground))
            {
                continue;
            }

            var zombieRect = zombie.GetZombieRect();
            if (zombieRect.x < right && zombieRect.x + zombieRect.width > left)
            {
                result.Add(zombie);
            }
        }

        return result;
    }

    public override void Reset()
    {
        base.Reset();

        whacksDone = 0;
        ticksSinceStart = WhackClipTicks;
    }

    #endregion
}
