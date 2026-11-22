using Il2CppReloaded.Gameplay;
using PvZReCoreLib.Content.Plants.ZombieCards;

namespace PvZCompleteCollection.Plants.Defs;

// "Zombies as plant cards": each card spawns a zombie on the tile you plant it on and immediately
// hypnotizes it (what a Hypno-shroom does), so it walks right and fights the other zombies. The
// whole mechanism lives in CoreLib (PvZReCoreLib.Content.Plants.ZombieCards); this file only
// decides which zombies get a card. Seed packet art, sun cost and almanac art are borrowed from
// the game's own zombie data, so there are no asset bundles to build.
//
// Versus: the plant-side player can pick these cards too; the conjured zombie fights the zombie
// side's zombies. Pass allowInVersus: false to a Register call to keep a card out of Versus.
//
// Adding a zombie is one line. The plain walkers are the ones this was designed around:
// TrafficCone, Pail, Door, Newspaper, Football, Imp. Special-movement zombies
// (Polevaulter, Pogo, Ladder, JackInTheBox, Dancer) and Flag have card art but are untested.
// A hypnotized Gargantuar smashes enemy zombies and throws its Imp forwards (CoreLib
// HypnotizedGargantuarPatches).
public static class ZombieCards
{
    public static void RegisterAll()
    {
        // Sun cost is the game's own cost for each zombie (left unset). Recharge is in 1/100 s:
        // 3000 = 30 s for the light zombies, longer for the heavy ones.
        ZombieCardRegistry.Register(ZombieType.Normal, sunCost: 50, rechargeTime: 3000);
        ZombieCardRegistry.Register(ZombieType.Flag, rechargeTime: 3000);
        ZombieCardRegistry.Register(ZombieType.Imp, rechargeTime: 3000);
        ZombieCardRegistry.Register(ZombieType.TrafficCone, rechargeTime: 3000);
        ZombieCardRegistry.Register(ZombieType.Pail, rechargeTime: 4000);
        ZombieCardRegistry.Register(ZombieType.Door, rechargeTime: 4000);
        ZombieCardRegistry.Register(ZombieType.Newspaper, rechargeTime: 3000);
        ZombieCardRegistry.Register(ZombieType.Polevaulter, rechargeTime: 3500);
        ZombieCardRegistry.Register(ZombieType.JackInTheBox, rechargeTime: 4000);
        ZombieCardRegistry.Register(ZombieType.Ladder, rechargeTime: 4000);
        ZombieCardRegistry.Register(ZombieType.Pogo, rechargeTime: 4000);
        ZombieCardRegistry.Register(ZombieType.Dancer, rechargeTime: 4500);
        ZombieCardRegistry.Register(ZombieType.Football, rechargeTime: 5000);
        ZombieCardRegistry.Register(ZombieType.Gargantuar, rechargeTime: 8000);
    }
}
