using Il2CppReloaded.Gameplay;

namespace PvZCompleteCollection2.Plants;

// Dr. Zomboss' rolling fire ball and ice ball are not projectiles: the boss Zombie holds the active one
// (mBossFireball reanimation, mFireballRow, mIsFireBall = false for the ice variant) and destroys it through
// BossDestroyFireball / BossDestroyIceballInRow. The game's own plants do that for one element: the Ice-shroom
// (Plant.IceZombies) puts out a fire ball, the Jalapeno (Plant.BurnRow) melts an ice ball. Our ice and fire
// plants do the same, for their own lane and the two next to it (what they chill / burn).
public static class BossBalls
{
    // A cold blast (Chilly Pepper) puts out Dr. Zomboss' fire ball if it is in the plant's lane or a neighbour.
    public static void PutOutFireBall(Board board, int plantRow)
    {
        var boss = board?.GetBossZombie();
        if (boss == null || boss.mBossFireball == null || !boss.mIsFireBall)
        {
            return;
        }

        if (Math.Abs(boss.mFireballRow - plantRow) <= 1)
        {
            boss.BossDestroyFireball();
        }
    }

    // A flame (Snapdragon) melts Dr. Zomboss' ice ball in the plant's lane or a neighbour.
    public static void MeltIceBall(Board board, int plantRow)
    {
        var boss = board?.GetBossZombie();
        if (boss == null || boss.mBossFireball == null || boss.mIsFireBall)
        {
            return;
        }

        if (Math.Abs(boss.mFireballRow - plantRow) <= 1)
        {
            boss.BossDestroyIceballInRow(boss.mFireballRow);
        }
    }
}
