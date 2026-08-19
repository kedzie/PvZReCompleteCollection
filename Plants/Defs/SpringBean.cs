using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Behavior;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Util;
using UnityEngine;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection2.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class SpringBeanDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public SpringBeanDefinition(IntPtr pointer) : base(pointer)
    {
        // Behaviour entirely from the controller (arming, trigger, spring), like Primal Potato Mine.
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Spring Bean", false);
        SetDescription($"Spring Bean flattens himself as zombies get close. The moment one steps on him he springs up and boings it away. On a level with water that is a big throw into the nearest pool - the zombie drowns, but the effort uses Spring Bean up. Anywhere else it is only a short bounce back: he naps for ten seconds and can do it {SpringBeanBehaviorController.WeakPushCount} times before he is spent.", false);
        SetTooltip($"Bounces a zombie back up to {SpringBeanBehaviorController.WeakPushCount} times (a 10 second nap in between). Throwing a zombie into the water uses him up at once.", false);

        // PvZ2 SpringbeanDefault: Cost 50, Hitpoints 300, PacketCooldown 20. PacketCooldown 20 -> m_refreshTime
        // 2000 (~100 ticks/sec, same mapping as every other port here).
        m_seedCost = 50;
        m_versusCost = 50;
        m_health = 300;

        m_refreshTime = 2000;
        m_versusBaseRefreshTime = 2000;
        m_versusSuddenDeathRefreshTime = 500;

        SetSeedPacketImage(CompleteCollectionMod.SpringBeanBundleId, "assets/plantrip/springbean/spr_seedpacket_springbean.png");
        SetPreviewImage(CompleteCollectionMod.SpringBeanBundleId, "assets/plantrip/springbean/spr_seedpacket_springbean.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "SpringBean_Default",
            AssetBundleId = CompleteCollectionMod.SpringBeanBundleId,
            SkinPrefabId = "assets/plantrip/springbean/models/default/plant/springbean.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the squash and spring sounds are PlaySound Animation
        // Events baked into the "attack" and "attack2" clips (PvZBundleBuilder.HandBakedSounds - the PAM has no
        // sound command, the choice of sound is a guess by length).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<SpringBeanBehaviorController>();
    }
}

// Clips (30 fps): "idle" (standing, default) -> [a zombie gets close] "attack" (30 frames, squashes flat and
// coils) -> "idle3" (flat, loops: armed) -> [a zombie steps on him] "attack2" (65 frames: springs up; the build
// bakes an ExecuteAttack event on frame 9, PvZBundleBuilder.ExtraLaunchFrames, where the stem is fully
// stretched) -> "recover" (41 frames, asleep, loops) -> [recovery over] "recover2" (25 frames, wakes up) -> idle.
// A spent bean (below) plays the whole of "attack2" and is then removed with Plant.Die() instead of sleeping.
//
// PvZ2 SpringbeanDefault: KnockbackTime 0.5 s, KnockbackHeight 130, KnockbackOffset 80 (how far back in X the
// zombie goes), AttackRecoveryTime 10 s, RectZombieProximity x -64..+74 (a zombie that close arms him), trigger
// rect x -30..+17, Damage 1. Everything is counted in game ticks, so it follows the game speed.
//
// Where the zombie goes (the mod author's design: Spring Bean throws zombies into the water):
//  * On a level with water (Board.StageHasPool) a zombie that does not swim is thrown in a big arc into the nearest
//    water lane (ZombieWaterLaunch: the game's own pool handling, a drowned zombie is removed without loot).
//  * A zombie that already swims (Snorkel, Dolphin Rider, ducky tube / in the pool) and every zombie on a level
//    without water gets the weak pushback instead: 80 px back with a hop (CoreLib's ZombiePushBack).
//
// Charges (the mod author's design, from how they remember him): a spring that only pushes zombies back
// (no water on the level, or only swimmers hit) uses one of WeakPushCount charges (default 3); the 10 s nap
// (PvZ2's AttackRecoveryTime) sits between pushes. A spring that throws a zombie into the water uses him up
// at once, whatever charges were left. When he is used up he plays out "attack2" and is removed with
// Plant.Die(), the call every one-shot plant here ends in (Gold Bloom, Chilly Pepper, ...: their baked
// OwnerDie event -> AnimationScripts.OwnerDie -> Plant.Die). It is called from the controller instead of an
// OwnerDie event because the same "attack2" clip is also the spring of the pushes that leave him alive.
// A spring that finds no zombie left to push (it died meanwhile) costs no charge.
[RegisterTypeInIl2Cpp]
public class SpringBeanBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // How many weak pushes (no water on the level, or a swimmer) he can do before he is used up.
    public const int WeakPushCount = 3;

    // States as plain ints (the injected Il2Cpp class keeps to simple field types).
    private const int Idle = 0;
    private const int Arming = 1;
    private const int Armed = 2;
    private const int Springing = 3;

    // KnockbackOffset 80, KnockbackTime 0.5 s, KnockbackHeight 130.
    private const float KnockbackDistance = 80f;
    private const int KnockbackTicks = 50;
    private const float KnockbackHop = 130f;
    private const int HitDamage = 1;

    // The throw into the water: 0.7 s in the air, peaking 160 board px above the straight line (a big arc, the lane
    // can be a few rows away).
    private const int WaterLaunchTicks = 70;
    private const float WaterLaunchHop = 160f;

    // AttackRecoveryTime 10 s.
    private const int RecoveryTicks = 10 * 100;

    // "attack2" is 65 frames at 30 fps = 217 ticks at normal speed; a used-up bean is removed just before the
    // clip's tail would hand over to the sleeping "recover" loop.
    private const int SpringClipTicks = 210;

    // The "attack" clip is 30 frames at 30 fps: one second at normal speed. Counted in ticks so it follows the
    // game speed like the clip does.
    private const int ArmingTicks = 100;

    // RectZombieProximity: a zombie inside plant.x - 64 .. plant.x + 74 arms him.
    private const float ProximityLeft = 64f;
    private const float ProximityRight = 74f;

    // Trigger: the zombie's body reaches the bean's tile (plus a little, so he goes off just before the chew starts).
    private const float TriggerRightPadding = 25f;

    private const int CheckInterval = 5;

    private int state = Idle;
    private int stateTicks;
    private int pushesLeft = WeakPushCount;
    private bool usedUp;

    #endregion

    #region Constructors

    public SpringBeanBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override void PostPlantUpdate()
    {
        base.PostPlantUpdate();

        if (!IsSkinApplied)
        {
            return;
        }

        stateTicks++;

        switch (state)
        {
            case Idle:
                if (stateTicks % CheckInterval == 0 && FindZombie(ProximityLeft, ProximityRight) != null)
                {
                    SetState(Arming);
                    PlayAnimation("attack");
                }

                break;

            case Arming:
                // The clip's exit transition lands in "idle3" (flat) by itself.
                if (stateTicks >= ArmingTicks)
                {
                    SetState(Armed);
                }

                break;

            case Armed:
                if (stateTicks % CheckInterval == 0 && FindZombie(0f, TriggerRightPadding) != null)
                {
                    SetState(Springing);
                    PlayAnimation("attack2");
                }

                break;

            case Springing:
                // A used-up bean ends with the spring (Plant.Die, like the instant plants' OwnerDie).
                if (usedUp)
                {
                    if (stateTicks >= SpringClipTicks)
                    {
                        usedUp = false;
                        Plant.Die();
                    }

                    break;
                }

                // The hit is the clip's ExecuteAttack event (OnAnimationAttackEvent); the clip then lands
                // in "recover" (asleep) by itself, the recovery countdown runs from the moment he sprang.
                if (stateTicks >= RecoveryTicks)
                {
                    SetState(Idle);
                    PlayAnimation("recover2");
                }

                break;
        }
    }

    private void SetState(int next)
    {
        state = next;
        stateTicks = 0;
    }

    // ExecuteAttack event on frame 9 of attack2: the zombies on his tile are sent flying (into the water) or bounced back.
    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        if (state != Springing)
        {
            return;
        }

        bool pushedAny = false;
        bool threwIntoWater = false;

        foreach (var zombie in FindZombies(0f, TriggerRightPadding + 10f))
        {
            pushedAny = true;
            zombie.TakeDamage(HitDamage, (DamageFlags)0);

            // Into the nearest water when there is any and the zombie is not already a swimmer ...
            if (!zombie.IsDeadOrDying()
                && !ZombieWaterLaunch.IsLaunched(zombie)
                && !ZombieWaterLaunch.IsSwimmer(zombie)
                && ZombieWaterLaunch.TryFindWaterTarget(Board, zombie, out int waterRow, out int waterColumn))
            {
                ZombieWaterLaunch.Launch(Board, zombie, waterRow, waterColumn, WaterLaunchTicks, WaterLaunchHop);
                threwIntoWater = true;
                continue;
            }

            // ... otherwise the weak pushback.
            ZombiePushBack.Push(zombie, Board, KnockbackDistance, KnockbackTicks, KnockbackHop);
        }

        if (!pushedAny)
        {
            return;
        }

        // A throw into the water uses him up at once; a weak push (also one that only hit a swimmer) uses one charge.
        if (threwIntoWater || --pushesLeft <= 0)
        {
            usedUp = true;
        }
    }

    private Zombie FindZombie(float leftPadding, float rightPadding)
    {
        var zombies = FindZombies(leftPadding, rightPadding);
        return zombies.Count > 0 ? zombies[0] : null;
    }

    // Live, bounceable zombies of the bean's own lane whose body overlaps [plant.x - leftPadding,
    // plant.x + plant width + rightPadding]. (Gargantuars and other heavy or special zombies are not bounced,
    // so they neither arm nor trigger him.)
    private List<Zombie> FindZombies(float leftPadding, float rightPadding)
    {
        var result = new List<Zombie>();
        if (Board?.m_zombies?.m_list == null)
        {
            return result;
        }

        var plantRect = Plant.GetPlantRect();
        float left = plantRect.x - leftPadding;
        float right = plantRect.x + plantRect.width + rightPadding;

        foreach (var entry in Board.m_zombies.m_list.ToList())
        {
            var zombie = entry.mItem;
            if (zombie == null
                || zombie.IsDeadOrDying()
                || zombie.mMindControlled
                || zombie.mRow != Plant.mRow
                || !CanBeBounced(zombie.mZombieType)
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

    private static bool CanBeBounced(ZombieType type)
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
        pushesLeft = WeakPushCount;
        usedUp = false;
    }

    #endregion
}
