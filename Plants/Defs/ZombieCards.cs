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
// HypnotizedGargantuarPatches); untested in game.
//
// Pool zombies: Snorkel and Dolphin Rider have no native seed packet, so their card icon is the
// zombie's ALMANAC illustration; they can only be planted on water (and are left out of the chooser
// on levels without a pool). Normal, Flag, Conehead and Buckethead may also be planted on water,
// where the game itself turns them into swimming Ducky Tube zombies - so there is no separate Ducky
// Tube card. All untested in game.
public static class ZombieCards
{
    public static void RegisterAll()
    {
        // Sun cost is the game's own cost for each zombie (left unset). Recharge is in 1/100 s:
        // 3000 = 30 s for the light zombies, longer for the heavy ones.
        ZombieCardRegistry.Register(ZombieType.Normal, sunCost: 50, rechargeTime: 3000, placement: ZombieCardPlacement.LandOrWater);
        // The Flag Zombie leads a horde: ten hypnotized zombies follow it and the huge wave banner shows.
        ZombieCardRegistry.Register(ZombieType.Flag, sunCost: 300, rechargeTime: 6000, placement: ZombieCardPlacement.LandOrWater,
            description: "A hypnotized Flag Zombie that rallies a horde: ten hypnotized zombies follow him into the lawn.",
            tooltip: "Conjures a hypnotized Flag Zombie leading ten hypnotized zombies.",
            escortCount: 10, announceHorde: true);
        ZombieCardRegistry.Register(ZombieType.Imp, rechargeTime: 3000);
        ZombieCardRegistry.Register(ZombieType.TrafficCone, rechargeTime: 3000, placement: ZombieCardPlacement.LandOrWater);
        ZombieCardRegistry.Register(ZombieType.Pail, rechargeTime: 4000, placement: ZombieCardPlacement.LandOrWater);
        ZombieCardRegistry.Register(ZombieType.Door, rechargeTime: 4000);
        ZombieCardRegistry.Register(ZombieType.Newspaper, rechargeTime: 3000);
        ZombieCardRegistry.Register(ZombieType.Polevaulter, rechargeTime: 3500);
        ZombieCardRegistry.Register(ZombieType.JackInTheBox, rechargeTime: 4000);
        ZombieCardRegistry.Register(ZombieType.Ladder, rechargeTime: 4000);
        ZombieCardRegistry.Register(ZombieType.Pogo, rechargeTime: 4000);
        ZombieCardRegistry.Register(ZombieType.Dancer, rechargeTime: 4500);
        ZombieCardRegistry.Register(ZombieType.Football, rechargeTime: 5000);
        ZombieCardRegistry.Register(ZombieType.Gargantuar, rechargeTime: 8000);

        // Pool zombies (water only, almanac art; sun cost and column limits are in CoreLib's
        // ZombieCardCatalog: Snorkel 100, Dolphin Rider 125).
        ZombieCardRegistry.Register(ZombieType.Snorkel, rechargeTime: 3500);
        ZombieCardRegistry.Register(ZombieType.DolphinRider, rechargeTime: 4000);
    }
}
