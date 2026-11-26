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

namespace PvZCompleteCollection.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class ChardGuardDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public ChardGuardDefinition(IntPtr pointer) : base(pointer)
    {
        // Behaviour entirely from the controller (charge, wait, spring), like Spring Bean.
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Chard Guard", false);
        SetDescription($"Chard Guard lies down and waits. When a zombie comes up to him he springs back up and sends it flying - and every spring costs him one of his {ChardGuardBehaviorController.LeafCount} leaves. Without leaves he can't spring any more, but he is still a very tough wall.", false);
        SetTooltip($"Lies in wait, then shoves a zombie back with a hard spring. {ChardGuardBehaviorController.LeafCount} springs (one leaf each), then he is a plain wall.", false);

        // PvZ2 ChardGuardDefault: Cost 75, Hitpoints 1500, PacketCooldown 15 (StartingCooldown 15).
        // PacketCooldown 15 -> m_refreshTime 1500 (~100 ticks/sec, same mapping as every other port here).
        m_seedCost = 75;
        m_versusCost = 75;
        m_health = 1500;

        m_refreshTime = 1500;
        m_versusBaseRefreshTime = 1500;
        m_versusSuddenDeathRefreshTime = 375;

        // PvZ2 family: Reinforce-mint (the walls).
        m_mintFamily = MintFamily.ReinforceMint;

        SetSeedPacketImage(CompleteCollectionMod.ChardGuardBundleId, "assets/plantrip/chardguard/spr_seedpacket_chardguard.png");
        SetPreviewImage(CompleteCollectionMod.ChardGuardBundleId, "assets/plantrip/chardguard/spr_seedpacket_chardguard.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "ChardGuard_Default",
            AssetBundleId = CompleteCollectionMod.ChardGuardBundleId,
            SkinPrefabId = "assets/plantrip/chardguard/models/default/plant/chardguard.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the lay-down and spring sounds are PlaySound Animation Events baked
        // into the "attack_leaves*" and "attack_flip_leaves*" clips from the PAM (PvZRipTools plant_sounds.py).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<ChardGuardBehaviorController>();
    }
}

// The PAM's own clips, unchanged: one set per leaf level N (3, 2, 1, then 0 = a plain wall, only idle_leaves0):
//   "idle_leavesN" (stands, loops; the Animator default is idle_leaves3)
//   -> [a zombie gets close] "attack_leavesN" (33 frames: he lies down; his sounds are baked on its first frame)
//   -> "attack_idle_leavesN" (100 frames, loops: lying in wait, armed)
//   -> [a zombie is in reach] "attack_flip_leavesN" (43 frames, 54 at level 1: he springs up; the PAM's use_action marker at +6
//      is the shove, the bundle build bakes an ExecuteAttack event there, and the clip flings one leaf away)
//   -> "idle_leaves(N-1)" (one leaf less).
// The PAM names the three leaf levels' art; the controller only has to keep count and play the right level. Everything is
// counted in game ticks (~100 a second), so it follows the game speed.
//
// PvZ2 ChardGuardDefault: PushesPerLeaf 1, KnockbackTime 0.5 s, KnockbackHeight 130, KnockbackOffset 200 (how far back the
// zombie goes); RectZombieProximity {mX -25, mY -50, mWidth 98, mHeight 60} = how close a zombie gets before he reacts; the
// first "special" action's rect {-25, -30, 85, 30} is the trigger, the second's {-40, -125, 132, 220} with Damage 60 is the
// area the spring hits. The rects are PvZ2 board px around the plant's origin (its centre); used here around the plant's
// centre, in his own lane (the trigger) and the lanes either side (the area the spring hits).
[RegisterTypeInIl2Cpp]
public class ChardGuardBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // PushesPerLeaf 1 and three leaves with an attack set (leaves3, leaves2, leaves1): three springs.
    public const int LeafCount = 3;

    // States as plain ints (the injected Il2Cpp class keeps to simple field types).
    private const int Idle = 0;
    private const int Charging = 1;
    private const int Armed = 2;
    private const int Springing = 3;

    // RectZombieProximity: a zombie whose body overlaps [centre - 25, centre + 73] in his lane makes him lie down.
    private const float ArmLeft = 25f;
    private const float ArmRight = 73f;

    // The first special's rect: [centre - 25, centre + 60] springs him.
    private const float TriggerLeft = 25f;
    private const float TriggerRight = 60f;

    // The spring hits [centre - 40, centre + 92] (the second special's rect) in his lane and the lanes either side.
    private const float ShoveLeft = 40f;
    private const float ShoveRight = 92f;
    private const int ShoveDamage = 60;

    // KnockbackOffset 200, KnockbackTime 0.5 s, KnockbackHeight 130.
    private const float KnockbackDistance = 200f;
    private const int KnockbackTicks = 50;
    private const float KnockbackHop = 130f;

    // "attack_leavesN" is 33 frames at 30 fps = 110 ticks; "attack_flip_leavesN" 43 frames = 143 ticks (54 frames = 180 ticks at level 1).
    private const int ChargeTicks = 110;
    private const int FlipTicks = 143;
    private const int FlipTicksLevel1 = 180;

    private const int CheckInterval = 5;

    private int state = Idle;
    private int stateTicks;
    private int leaves = LeafCount;
    private bool shoved;
    private int springLevel;

    #endregion

    #region Constructors

    public ChardGuardBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override void PostPlantUpdate()
    {
        base.PostPlantUpdate();

        // No leaves left: a plain wall (idle_leaves0 is what the last spring lands in).
        if (!IsSkinApplied || leaves <= 0)
        {
            return;
        }

        stateTicks++;

        switch (state)
        {
            case Idle:
                if (stateTicks % CheckInterval == 0 && FindZombies(ArmLeft, ArmRight, 0).Count > 0)
                {
                    SetState(Charging);
                    PlayAnimation($"attack_leaves{leaves}");
                }

                break;

            case Charging:
                // The clip's exit transition lands in "attack_idle_leavesN" (lying, loops) by itself.
                if (stateTicks >= ChargeTicks)
                {
                    SetState(Armed);
                }

                break;

            case Armed:
                if (stateTicks % CheckInterval == 0 && FindZombies(TriggerLeft, TriggerRight, 0).Count > 0)
                {
                    SetState(Springing);
                    shoved = false;
                    springLevel = leaves;
                    PlayAnimation($"attack_flip_leaves{leaves}");
                }

                break;

            case Springing:
                // The shove (and the leaf it costs) is the clip's ExecuteAttack event (OnAnimationAttackEvent); the clip then
                // lands in the idle of the next leaf level by itself. Back to waiting for the next zombie once it has played out.
                if (stateTicks >= (springLevel == 1 ? FlipTicksLevel1 : FlipTicks))
                {
                    SetState(Idle);
                }

                break;
        }
    }

    private void SetState(int next)
    {
        state = next;
        stateTicks = 0;
    }

    // ExecuteAttack event on the use_action marker of the flip clip: the zombies in the spring's area are hit and flung back.
    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        if (state != Springing || shoved)
        {
            return;
        }

        shoved = true;

        foreach (var zombie in FindZombies(ShoveLeft, ShoveRight, 1))
        {
            zombie.TakeDamage(ShoveDamage, (DamageFlags)0);
            ZombiePushBack.Push(zombie, Board, KnockbackDistance, KnockbackTicks, KnockbackHop);
        }

        // One leaf per spring (PushesPerLeaf 1), whether or not a zombie was still there to shove: the clip has flung the
        // leaf. The Animator lands in the idle of the next level when the clip is over; at 0 leaves that is idle_leaves0, the
        // plain wall (PostPlantUpdate does nothing any more).
        leaves--;
    }

    // Live, shoveable zombies whose body overlaps [centre - left, centre + right] of the plant, in his own lane
    // (laneRange 0) or the lanes either side (1).
    private List<Zombie> FindZombies(float left, float right, int laneRange)
    {
        var result = new List<Zombie>();
        if (Board?.m_zombies?.m_list == null)
        {
            return result;
        }

        var plantRect = Plant.GetPlantRect();
        float centerX = plantRect.x + plantRect.width / 2f;
        float zoneLeft = centerX - left;
        float zoneRight = centerX + right;

        foreach (var entry in Board.m_zombies.m_list.ToList())
        {
            var zombie = entry.mItem;
            if (zombie == null
                || zombie.IsDeadOrDying()
                || zombie.mMindControlled
                || Math.Abs(zombie.mRow - Plant.mRow) > laneRange
                || !CanBeShoved(zombie.mZombieType)
                || !zombie.EffectedByDamage(DamageRangeFlags.Ground))
            {
                continue;
            }

            var zombieRect = zombie.GetZombieRect();
            if (zombieRect.x < zoneRight && zombieRect.x + zombieRect.width > zoneLeft)
            {
                result.Add(zombie);
            }
        }

        return result;
    }

    // Heavy, buried, flying and scenery zombies are neither pushed nor do they arm or trigger him (the same list as Spring Bean's).
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

        state = Idle;
        stateTicks = 0;
        leaves = LeafCount;
        shoved = false;
        springLevel = LeafCount;
    }

    #endregion
}
