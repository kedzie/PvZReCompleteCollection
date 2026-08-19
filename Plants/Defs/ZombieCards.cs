using Il2CppReloaded.Gameplay;
using PvZReCoreLib.Content.Plants.ZombieCards;

namespace PvZCompleteCollection2.Plants.Defs;

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
        // The Flag Zombie leads a horde: ten hypnotized zombies (70% Normal, 20% Conehead, 10% Buckethead, see
        // CoreLib ZombieCardUtils.PickEscortType) follow it and the huge wave banner shows.
        ZombieCardRegistry.Register(ZombieType.Flag, sunCost: 300, rechargeTime: 6000, placement: ZombieCardPlacement.LandOrWater,
            description: "A hypnotized Flag Zombie that rallies a horde: ten hypnotized zombies, some with cones and buckets, follow him into the lawn.",
            tooltip: "Conjures a hypnotized Flag Zombie leading ten hypnotized zombies, some with cones and buckets.",
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
        // The red-eyed Gargantuar: 6000 HP instead of 3000 (set by the game itself for his zombie type).
        // The game has no packet or almanac entry for him, so the card reuses the Gargantuar's art.
        ZombieCardRegistry.Register(ZombieType.RedeyeGargantuar, sunCost: 500, rechargeTime: 12000,
            description: "A hypnotized Giga Gargantuar, the red-eyed one with twice the health. He smashes enemy zombies and throws his Imp forwards.",
            tooltip: "Conjures a hypnotized red-eyed Gargantuar with 6000 health.");

        // Vehicles. Planted on an empty tile like any card, but only that tile's LANE counts: the vehicle
        // starts at the left edge of the lane and drives right over everything in it (never over water:
        // refused in a lane with a pool tile). It is harmless to our plants, and no hypnotized zombie sets
        // off a lawn mower. The Zomboni (the game's own packet and cost) runs over the enemy zombies in
        // its way and leaves an ice trail behind it that stops planting for 30 s; it stops
        // in front of a Gargantuar, who smashes it (it blows up and hurts him), and blows up with an
        // enemy Zomboni. The Bobsled Team is the sled plus three hypnotized riders
        // sliding right: slowly on bare lawn, fast over a Zomboni's ice (the sled makes no ice and needs
        // none). It breaks on the first enemy zombie it hits (that zombie takes no damage,
        // Gargantuars included), or at the end of the lane, and the riders walk on. No native packet for the Bobsled,
        // so its icon is the almanac illustration. Both untested in game.
        ZombieCardRegistry.Register(ZombieType.Zamboni, rechargeTime: 7000,
            description: "A hypnotized Zomboni. Plant it on an empty square: it starts at the left edge of that lane and drives right, running over every enemy zombie in its way (never your plants) and leaving a trail of ice behind it that nothing can be planted on until it melts. It stops in front of a Gargantuar, who smashes it - it blows up in his face - and it blows up together with an enemy Zomboni.",
            tooltip: "Conjures a hypnotized Zomboni that drives along the lane from the left, running over enemy zombies.");
        ZombieCardRegistry.Register(ZombieType.Bobsled, sunCost: 250, rechargeTime: 9000,
            description: "A hypnotized Zombie Bobsled Team. Plant it on an empty square: four zombies on a sled start at the left edge of that lane and slide right - slowly on grass, fast over a Zomboni's ice. The sled breaks up on the first enemy zombie it hits, and the riders walk on, hypnotized.",
            tooltip: "Conjures a hypnotized Bobsled Team that slides along the lane from the left, and breaks up on the first enemy zombie it meets.");

        // Pool zombies (water only, almanac art; sun cost and column limits are in CoreLib's
        // ZombieCardCatalog: Snorkel 100, Dolphin Rider 125).
        ZombieCardRegistry.Register(ZombieType.Snorkel, rechargeTime: 3500);
        ZombieCardRegistry.Register(ZombieType.DolphinRider, rechargeTime: 4000);
        ZombieCardRegistry.Register(ZombieType.Yeti, rechargeTime: 5000);
    }
}
