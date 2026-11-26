using HarmonyLib;
using Il2CppReloaded.Gameplay;
using UnityEngine;

namespace PvZCompleteCollection.Plants;

// Sweet Potato's lure: a zombie walking past in a neighbouring lane is pulled over into his lane.
//
// PvZ2 data (SweetPotatoDefault): NormalAttractRect {x 0, y -110, w 80, h 200} relative to the plant - his own tile
// column and the lane above and below - LaneChangeTime 0.6 s, TimeBetweenLaneChanges 5 s. (PlantfoodAttractRect,
// 240 wide, is not ported: custom plants have no Plant Food power here.)
//
// Moving a zombie to another lane is Zombie.SetRow(row) plus its Y (Zombie.GetPosYBasedOnRow), the same thing
// ZombieWaterLaunch does for Spring Bean. The slide is driven from a postfix on Zombie.Update in game ticks, so it
// follows the game speed; the lane itself (what the zombie targets and chews) switches half-way through.
public static class ZombieLaneLure
{
    // 0.6 s at ~100 ticks/sec.
    public const int SlideTicks = 60;

    private sealed class SlideState
    {
        public float StartY;
        public int TargetRow;
        public int ElapsedTicks;
        public bool RowSwitched;
    }

    private static readonly Dictionary<Zombie, SlideState> Active = new Dictionary<Zombie, SlideState>();

    public static bool IsSliding(Zombie zombie)
    {
        return zombie != null && Active.ContainsKey(zombie);
    }

    // Zombies PvZ2's Sweet Potato cannot move (its MovableZombies exclude list: vehicles, barrel/ballista riders...),
    // matched to this game's types, plus the ones with their own lane logic.
    public static bool CanBeMoved(Zombie zombie)
    {
        if (zombie.IsDeadOrDying()
            || zombie.mMindControlled
            || zombie.mInPool
            || zombie.mIsEating
            || zombie.mBlowingAway
            || zombie.mZombiePhase != ZombiePhase.ZombieNormal
            || zombie.IsFlying()
            || IsSliding(zombie)
            || ZombieWaterLaunch.IsLaunched(zombie))
        {
            return false;
        }

        switch (zombie.mZombieType)
        {
            case ZombieType.Zamboni:
            case ZombieType.Bobsled:
            case ZombieType.Catapult:
            case ZombieType.Boss:
            case ZombieType.Gravestone:
            case ZombieType.Digger:
            case ZombieType.Bungee:
            case ZombieType.Balloon:
            case ZombieType.Snorkel:
            case ZombieType.DolphinRider:
                return false;
            default:
                return true;
        }
    }

    // Would the zombie's body stand on water in 'row' but not in its own lane (or the other way round)? Then it is
    // left alone - a land zombie dropped into a pool lane would have to drown.
    public static bool CrossesWater(Board board, Zombie zombie, int targetRow)
    {
        if (!board.StageHasPool())
        {
            return false;
        }

        var rect = zombie.GetZombieRect();
        foreach (float x in new[] { rect.x, rect.x + rect.width })
        {
            int column = board.PixelToGridXKeepOnBoard(x, zombie.mPosY);
            if (board.IsPoolSquare(column, targetRow) != board.IsPoolSquare(column, zombie.mRow))
            {
                return true;
            }
        }

        return false;
    }

    public static void Start(Zombie zombie, int targetRow)
    {
        Active[zombie] = new SlideState
        {
            StartY = zombie.mPosY,
            TargetRow = targetRow,
        };
    }

    // Called once per zombie per Zombie.Update.
    public static void Advance(Zombie zombie)
    {
        if (zombie == null || !Active.TryGetValue(zombie, out var state))
        {
            return;
        }

        if (zombie.IsDeadOrDying() || zombie.mMindControlled)
        {
            Active.Remove(zombie);
            return;
        }

        state.ElapsedTicks++;
        float t = Mathf.Clamp01((float)state.ElapsedTicks / SlideTicks);
        float eased = t * t * (3f - 2f * t);

        if (!state.RowSwitched && t >= 0.5f)
        {
            state.RowSwitched = true;
            zombie.SetRow(state.TargetRow);
        }

        zombie.mPosY = Mathf.Lerp(state.StartY, zombie.GetPosYBasedOnRow(state.TargetRow), eased);

        if (t >= 1f)
        {
            Active.Remove(zombie);
            zombie.mPosY = zombie.GetPosYBasedOnRow(state.TargetRow);
        }
    }
}

[HarmonyPatch(typeof(Zombie), nameof(Zombie.Update))]
public class Zombie_Update_LaneLure_Patch
{
    public static void Postfix(ref Zombie __instance)
    {
        ZombieLaneLure.Advance(__instance);
    }
}
