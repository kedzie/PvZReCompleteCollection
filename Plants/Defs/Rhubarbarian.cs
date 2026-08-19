using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Behavior;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Content.Plants.Mint;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection2.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class RhubarbarianDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public RhubarbarianDefinition(IntPtr pointer) : base(pointer)
    {
        // Behaviour entirely from the controller (reach test, swing, push), like Spring Bean.
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Rhubarbarian", false);
        SetDescription($"Rhubarbarian swings his rhubarb leaves at the first zombie that comes within reach and sends it flying back. He can do it {RhubarbarianBehaviorController.PushCount} times, and every swing leaves him more battered, until he is spent.", false);
        SetTooltip($"Shoves a zombie back with each swing. Spent after {RhubarbarianBehaviorController.PushCount} pushes (he looks more worn after each one).", false);

        // PvZ2 RhubarbarianDefault: Cost 100, PacketCooldown 20 (StartingCooldown 15). Its Hitpoints of 10 are only
        // there because PvZ2's version is a short-lived instant plant; this one stays on the lawn for its three
        // swings, so it gets an ordinary plant's 300 like Spring Bean's.
        // PacketCooldown 20 -> m_refreshTime 2000 (~100 ticks/sec, same mapping as every other port here).
        m_seedCost = 100;
        m_versusCost = 100;
        m_health = 300;

        m_refreshTime = 2000;
        m_versusBaseRefreshTime = 2000;
        m_versusSuddenDeathRefreshTime = 500;

        // PvZ2 family: Enforce-mint.
        m_mintFamily = MintFamily.EnforceMint;

        SetSeedPacketImage(CompleteCollectionMod.RhubarbarianBundleId, "assets/plantrip/rhubarbarian/spr_seedpacket_rhubarbarian.png");
        SetPreviewImage(CompleteCollectionMod.RhubarbarianBundleId, "assets/plantrip/rhubarbarian/spr_seedpacket_rhubarbarian.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Rhubarbarian_Default",
            AssetBundleId = CompleteCollectionMod.RhubarbarianBundleId,
            SkinPrefabId = "assets/plantrip/rhubarbarian/models/default/plant/rhubarbarian.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the spawn, swing and end sounds are PlaySound Animation Events
        // baked into the "plant", "attack*" and "attack_end*" clips from the PAM (PvZRipTools plant_sounds.py).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<RhubarbarianBehaviorController>();
    }
}

// Clips (30 fps). The PAM has no damage art for him; PvZRipTools make_damage_variants.py derives it (a colour grade
// and some worn patches, wear levels 0 (as ripped) .. 3 (spent)):
//   "plant" (23 frames, the spawn; the Animator default) -> "idle" (loops, wear 0)
//   a swing at wear level N is the clip "attack" (N = 0) / "attack_damage1" / "attack_damage2": 28 frames with two
//   use_action markers (+14 a first smack, +21 the hit that shoves). The clip's art changes to wear N + 1 on the
//   frame after the shove (so the damage shows the moment he has hit), then it plays out into
//   "attack_end_damageN+1" (16 frames) -> "idle_damageN+1" (loops). After the third shove (wear 3) it is
//   "attack_end_damage3" -> "end_damage3" (20 frames, his PvZ2 exit: he spins away in a vortex, with PvZ2's sound), and
//   the plant is removed by the OwnerDie event baked near the end of that clip.
//
// PvZ2's Rhubarbarian is an instant "Pummeling Strikes" plant with a trigger rect x -25..+85 around the plant; the
// swing starts when a zombie is within that reach of his tile. Here each swing is: a light smack on the first marker,
// the shove on the second. Everything is counted in game ticks, so it follows the game speed.
[RegisterTypeInIl2Cpp]
public class RhubarbarianBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // The number of shoves he has in him (the mod author's design, like Spring Bean's charges).
    public const int PushCount = 3;

    // Reach: a zombie whose body overlaps [plant.x - ReachLeft, plant.x + plant width + ReachRight] (PvZ2's trigger
    // rect is -25..+85 around the plant's origin).
    private const float ReachLeft = 10f;
    private const float ReachRight = 45f;

    // The smack and the shove (a token amount of damage, the point is the shove; Spring Bean's hit is 1 too).
    private const int SmackDamage = 5;
    private const int ShoveDamage = 5;

    // How far back the shove sends a zombie (one tile and a bit), how long the slide takes, and the hop.
    private const float ShoveDistance = 110f;
    private const int ShoveTicks = 45;
    private const float ShoveHop = 40f;

    // "attack" is 28 frames at 30 fps (93 ticks), "attack_end*" 16 more (53): the next swing may start after both,
    // plus a short breather. The reach test only needs a few checks a second.
    private const int SwingTicks = 93 + 53 + 20;
    private const int CheckInterval = 5;

    private int pushesDone;
    private int swingTicks = SwingTicks;
    private int markersThisSwing;

    #endregion

    #region Constructors

    public RhubarbarianBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override void PostPlantUpdate()
    {
        base.PostPlantUpdate();

        if (!IsSkinApplied || pushesDone >= PushCount)
        {
            return;
        }

        // A swing in progress (or just over): wait it out.
        if (swingTicks < SwingTicks)
        {
            swingTicks++;
            return;
        }

        swingTicks++;
        if (swingTicks % CheckInterval != 0)
        {
            return;
        }

        if (FindZombies().Count > 0)
        {
            swingTicks = 0;
            markersThisSwing = 0;
            PlayAnimation(pushesDone == 0 ? "attack" : $"attack_damage{pushesDone}");
        }
    }

    // ExecuteAttack events on the two use_action markers of the swing clip.
    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        // Only while a swing is running (a re-bake or a stray event must never push).
        if (swingTicks >= SwingTicks || pushesDone >= PushCount)
        {
            return;
        }

        markersThisSwing++;
        bool shove = markersThisSwing >= 2;

        foreach (var zombie in FindZombies())
        {
            zombie.TakeDamage(shove ? ShoveDamage : SmackDamage, (DamageFlags)0);
            if (shove)
            {
                ZombiePushBack.Push(zombie, Board, ShoveDistance, ShoveTicks, ShoveHop);
            }
        }

        // The shove is what uses him up, whether or not a zombie was still there to shove (the clip's art changes
        // wear level on that very frame either way, so the count has to follow it).
        if (shove)
        {
            pushesDone++;
        }
    }

    // Live, shoveable zombies of his own lane within reach.
    private List<Zombie> FindZombies()
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
                || !CanBeShoved(zombie.mZombieType)
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

    // Heavy, buried, flying and scenery zombies are neither pushed nor do they start a swing (PvZ2's own exclude list
    // has the Gargantuar, Catapult and the other big ones).
    private static bool CanBeShoved(ZombieType type)
    {
        switch (type)
        {
            case ZombieType.Gargantuar:
            case ZombieType.RedeyeGargantuar:
            case ZombieType.Zamboni:
            case ZombieType.Bobsled:
            case ZombieType.Boss:
            case ZombieType.Gravestone:
            case ZombieType.Catapult:
            case ZombieType.Digger:
            case ZombieType.Bungee:
            case ZombieType.Balloon:
                return false;
            default:
                return true;
        }
    }

    public override void Reset()
    {
        base.Reset();

        pushesDone = 0;
        swingTicks = SwingTicks;
        markersThisSwing = 0;
    }

    #endregion
}
