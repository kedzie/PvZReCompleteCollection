using HarmonyLib;
using Il2CppReloaded.Gameplay;
using UnityEngine;

namespace PvZCompleteCollection.Plants;

// Throws a zombie in a high arc into the nearest water of a pool level (Spring Bean). A few facts about how the game
// treats a zombie in a pool lane, read from the decompile (ISIL of Zombie.CheckForPool / SetRow):
//
//  * Setting the row is just Zombie.SetRow(row): it stores mRow and recomputes the render order, nothing else. The
//    vertical position follows from the row (Zombie.GetPosYBasedOnRow); the lane the zombie is "in" for everything
//    else (peas, plants, chewing) is mRow.
//  * The game's own water entry is Zombie.CheckForPool, which runs from the zombie's update: for the zombie types the
//    game lets swim (Normal, Flag, Conehead, Buckethead, Snorkel, Dolphin Rider, the "head" zombies, ...) it tests
//    two points of the body against Board.IsPoolSquare(column, mRow); when both are water it sets mInPool, the
//    in-to-pool height, starts the walk animation again and calls PoolSplash (the ducky tube appears and the zombie
//    swims on). Every other zombie type is simply ignored by it: nothing in the game drowns a zombie.
//
// So: moving the zombie (SetRow + x) is enough for the swimming types - CheckForPool is called once right after the
// landing instead of waiting for the next tick. For everything that cannot swim the drowning is done here: PoolSplash
// (the game's splash particle and sound) and then Zombie.DieNoLoot (the same removal the game uses for a zombie that
// has to disappear without loot).
//
// Like ZombiePushBack (CoreLib) the arc is driven from a postfix on Zombie.Update, in game ticks, so it follows the
// game speed; the zombie's own walking is simply overwritten each tick while it is in the air.
public static class ZombieWaterLaunch
{
    // true: a zombie that merely CAN swim (Normal, Flag, Conehead, Buckethead...) is drowned as well, so a knock into
    // the water always removes a land zombie. false: those get the game's own water handling instead (they land,
    // are given a ducky tube by CheckForPool and swim on).
    public const bool DrownZombiesThatCouldSwim = false;

    private const float TileWidth = 80f;
    private const int LastColumnIndex = 8;

    private sealed class LaunchState
    {
        public float StartX;
        public float TargetX;
        public float StartY;
        public int TargetRow;
        public float Hop;
        public int ElapsedTicks;
        public int TotalTicks;
    }

    private static readonly Dictionary<Zombie, LaunchState> Active = new Dictionary<Zombie, LaunchState>();

    // Zombies that already swim (or live in the water) are not thrown anywhere: they just get the weak pushback.
    public static bool IsSwimmer(Zombie zombie)
    {
        if (zombie.mInPool)
        {
            return true;
        }

        switch (zombie.mZombieType)
        {
            case ZombieType.Snorkel:
            case ZombieType.DolphinRider:
            case ZombieType.DuckyTube:
                return true;
            default:
                return false;
        }
    }

    // The nearest water tile to the zombie: the water lane closest to its row (ties: the upper one), and in it the
    // water column closest to the zombie, preferring a column whose neighbours are water too (so both of the
    // points CheckForPool tests land in the water). False when the level has no water.
    public static bool TryFindWaterTarget(Board board, Zombie zombie, out int targetRow, out int targetColumn)
    {
        targetRow = -1;
        targetColumn = -1;

        if (board == null || !board.StageHasPool())
        {
            return false;
        }

        int rows = board.GetNumRows();
        int bestDistance = int.MaxValue;
        for (int row = 0; row < rows; row++)
        {
            if (!HasWater(board, row))
            {
                continue;
            }

            int distance = Math.Abs(row - zombie.mRow);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                targetRow = row;
            }
        }

        if (targetRow < 0)
        {
            return false;
        }

        var rect = zombie.GetZombieRect();
        int zombieColumn = board.PixelToGridXKeepOnBoard(rect.x + rect.width / 2f, zombie.mPosY);

        int bestScore = int.MaxValue;
        for (int column = 0; column <= LastColumnIndex; column++)
        {
            if (!board.IsPoolSquare(column, targetRow))
            {
                continue;
            }

            bool interior = (column == 0 || board.IsPoolSquare(column - 1, targetRow))
                            && (column == LastColumnIndex || board.IsPoolSquare(column + 1, targetRow));
            int score = Math.Abs(column - zombieColumn) + (interior ? 0 : 100);
            if (score < bestScore)
            {
                bestScore = score;
                targetColumn = column;
            }
        }

        return targetColumn >= 0;
    }

    private static bool HasWater(Board board, int row)
    {
        for (int column = 0; column <= LastColumnIndex; column++)
        {
            if (board.IsPoolSquare(column, row))
            {
                return true;
            }
        }

        return false;
    }

    // Start the throw: the zombie lands in the middle of (targetColumn, targetRow) after 'ticks' game ticks, the arc
    // peaking 'hop' board pixels above the straight line.
    public static void Launch(Board board, Zombie zombie, int targetRow, int targetColumn, int ticks, float hop)
    {
        if (zombie == null)
        {
            return;
        }

        // Land with the middle of the body in the middle of the tile.
        var rect = zombie.GetZombieRect();
        float bodyCenter = rect.x + rect.width / 2f;
        float tileCenter = board.GridToPixelX(targetColumn, targetRow) + TileWidth / 2f;

        Active[zombie] = new LaunchState
        {
            StartX = zombie.mPosX,
            TargetX = zombie.mPosX + (tileCenter - bodyCenter),
            StartY = zombie.mPosY,
            TargetRow = targetRow,
            Hop = hop,
            ElapsedTicks = 0,
            TotalTicks = Math.Max(1, ticks),
        };
    }

    public static bool IsLaunched(Zombie zombie)
    {
        return zombie != null && Active.ContainsKey(zombie);
    }

    // Called once per zombie per Zombie.Update.
    public static void Advance(Zombie zombie)
    {
        if (zombie == null || !Active.TryGetValue(zombie, out var state))
        {
            return;
        }

        state.ElapsedTicks++;
        float t = Mathf.Clamp01((float)state.ElapsedTicks / state.TotalTicks);

        float landingY = zombie.GetPosYBasedOnRow(state.TargetRow);
        zombie.mPosX = Mathf.Lerp(state.StartX, state.TargetX, t);
        // This engine's Y grows downward, so the hop is a negative offset (same as ZombiePushBack).
        zombie.mPosY = Mathf.Lerp(state.StartY, landingY, t) - 4f * state.Hop * t * (1f - t);

        if (t < 1f)
        {
            return;
        }

        Active.Remove(zombie);
        Land(zombie, state);
    }

    private static void Land(Zombie zombie, LaunchState state)
    {
        zombie.SetRow(state.TargetRow);
        zombie.mPosX = state.TargetX;
        zombie.mPosY = zombie.GetPosYBasedOnRow(state.TargetRow);

        if (zombie.IsDeadOrDying())
        {
            return;
        }

        bool couldSwim = CouldSwim(zombie.mZombieType);
        if (!IsSwimmer(zombie) && (!couldSwim || DrownZombiesThatCouldSwim))
        {
            // The game's own splash (particle + sound), then the zombie is removed without loot.
            zombie.PoolSplash(true);
            zombie.DieNoLoot();
            return;
        }

        // A zombie type the game lets swim: its own water entry (ducky tube, swim animation, splash) runs now
        // that the zombie is in a water lane.
        zombie.CheckForPool();
    }

    // The types Zombie.CheckForPool lets into the water (decompiled bit mask 0x23C014816 and the TallnutHead case):
    // Normal, Flag, Conehead, Buckethead, Snorkel, Dolphin Rider, Balloon and the head zombies.
    private static bool CouldSwim(ZombieType type)
    {
        switch (type)
        {
            case ZombieType.Normal:
            case ZombieType.Flag:
            case ZombieType.TrafficCone:
            case ZombieType.Pail:
            case ZombieType.Snorkel:
            case ZombieType.DolphinRider:
            case ZombieType.Balloon:
                return true;
            default:
                return false;
        }
    }
}

[HarmonyPatch(typeof(Zombie), nameof(Zombie.Update))]
public class Zombie_Update_WaterLaunch_Patch
{
    public static void Postfix(ref Zombie __instance)
    {
        ZombieWaterLaunch.Advance(__instance);
    }
}
