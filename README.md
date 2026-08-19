<div align="center">

# Complete Collection 2

**29 new plants and 26 hypnotized zombie cards for *Plants vs. Zombies Replanted*,**
**rebuilt from PvZ2 art, animation, sound and data.**

![MelonLoader](https://img.shields.io/badge/MelonLoader-0.7.3%2B-8a2be2) ![Plants](https://img.shields.io/badge/plants-29-2ea043) ![Zombie cards](https://img.shields.io/badge/zombie%20cards-26-d29922) ![Game](https://img.shields.io/badge/PvZ-Replanted-blue)

</div>

---

## What it is

Complete Collection 2 (CC2) is a [MelonLoader](https://melonwiki.xyz) mod that adds PvZ2 plants to Replanted and wires them into the game's own mechanics: they cost sun, recharge from the seed bank, show up in the almanac, drop from conveyor belts, get chewed by zombies, and respect the Versus rules. Nothing is faked with overlays; the plants are real `Plant` objects with their own behaviour code.

- **Frame-accurate animation.** Every plant is composited from the original PvZ2 PAM animation, at PvZ2's own 30 fps.
- **Real PvZ2 sounds,** baked into the animations as events, so they follow the game's speed setting.
- **PvZ2 numbers** (cost, health, damage, rates) ported wherever Replanted has an equivalent.
- **Hypnotized zombie cards:** plant a zombie that fights for you, from a Basic Zombie to a Giga Gargantuar, Zomboni, Bobsled Team, Yeti and the Zombotany crew.
- **Conveyor-belt support:** on belt levels (the Dr. Zomboss fight included) the belt also drops custom plants and zombie cards.

It runs next to the original *Complete Collection* mod (separate assembly and namespace).

## The plants

| Plant | Sun | What it does |
|---|---:|---|
| **Aloe** | 75 | Heals hurt plants in the 3x3 area around her, every 5 seconds. |
| **Bamboo Spartan** | 175 | Spear jabs; his shield soaks 1400 damage, and without it he hits much harder. |
| **Bombegranate** | 150 | Explodes at once, destroying every zombie in a 3x3 area. |
| **Celery Stalker** | 50 | Hides so zombies walk past, pops up to strike, burrows back down. |
| **Chard Guard** | 75 | Lies in wait, then springs up and shoves a zombie back. Three springs, then a plain wall. |
| **Chilly Pepper** | 125 | Freezes every zombie in its lane, chills the next lanes, hurts the nearby ones. |
| **Cold Snapdragon** | 150 | Frost breath over 2 tiles in 3 lanes: damages and chills. |
| **Dusk Lobber** | 150 | Lobs thorn bulbs that splash the zombies around the target. |
| **Endurian** | 100 | Jabs back at any zombie touching him; the more damage he has taken, the stronger he gets. |
| **Explode-o-nut** | 50 | A tough wall that explodes when it is destroyed. |
| **Fire Peashooter** | 175 | Flaming peas, immune to frost. |
| **Gold Bloom** | 0 | An instant burst of sun. |
| **Iceweed** | 150 | Hurts and slows zombies that walk over it. Cannot be eaten. |
| **Mega Gatling Pea** | 400 | A stream of peas, and a giant pea every few volleys. |
| **Nightshade** | 75 | Whacks the zombies in front of it, three times, then is spent. |
| **Peanut** | 150 | A Wall-nut that fires two peas. |
| **Peapod** | 125 | Shoots peas; plant him on himself again to grow up to five heads. |
| **Pepperpult** | 200 | Lobs peppers that splash the zombies around the target. |
| **Primal Peashooter** | 175 | Slow, hard-hitting rock peas that can knock zombies back. |
| **Primal Potato Mine** | 50 | A fast-arming mine with a large blast radius. |
| **Primal Sunflower** | 75 | More sun than a regular Sunflower. |
| **Primal Wall-nut** | 75 | A cheap wall that shows its wear as it takes damage. |
| **Snapdragon** | 150 | Fire breath over 2 tiles in 3 lanes. |
| **Sourshot** | 150 | Spits two lemons per volley that can knock zombies back. |
| **Spring Bean** | 50 | Bounces a zombie back; throw one into the water and he is spent. |
| **Stallia** | 0 | Stalls every zombie in a 3x3 area for 12 seconds. |
| **Stunion** | 25 | Stalls the zombies in front of him when one gets close, then is spent. |
| **Sweet Potato** | 150 | A tough wall that lures zombies over from the next lanes. |
| **Tumbleweed** | 25 | Launches itself down the lane, bouncing diagonally between zombies. |

The nuts (Primal Wall-nut, Explode-o-nut, Peanut, Endurian) count as nuts for Ladder Zombies, as a Wall-nut does.

## Zombie cards

Plant a hypnotized zombie that walks right and fights for you, exactly like one that ate a Hypno-shroom:
Basic, Flag, Imp, Conehead, Buckethead, Screen Door, Newspaper, Pole Vaulter, Jack-in-the-Box, Ladder, Pogo, Dancing, Football, Gargantuar, Giga Gargantuar, Zomboni, Bobsled Team, Snorkel, Dolphin Rider, Yeti, and the Zombotany set (Peashooter, Wall-nut, Tall-nut, Jalapeno, Squash, Gatling Pea).

With [VersusPlus](https://gamebanana.com/mods/713795) installed, several cards use its card art; without it they borrow the closest native picture.

## Install

1. Install [MelonLoader](https://melonwiki.xyz) **0.7.3 or newer** into *PvZ Replanted*.
2. Download the latest release zip and extract it into the game's `Mods` folder, overwriting when asked. It contains:
   - `CompleteCollection2.dll` and the `CompleteCollection/` asset bundles
   - `PvZReCoreLib.dll`, the library CC2 is built on (**required**, and the matching version is in the zip)
   - `CoreLib/ConveyorBelt.cfg`, the belt settings (overwriting it resets them; skip it when updating if you changed it)
3. Start the game. The plants appear in the seed chooser and the almanac.

The zip is large (about 540 MB) because every plant carries its full animation frames.

**Optional:** BloomEngine (by PalmForest) adds an in-game menu page for the belt settings. Note that VersusPlus *requires* BloomEngine; if you run VersusPlus, install BloomEngine too, or the game will lag badly.

## Branches

| Branch | Purpose |
|---|---|
| `dev` | The standalone **Complete Collection 2** build: the release zips are built from here. It is `master` plus one commit that renames the namespace and assembly so it can run beside the original mod. |
| `master` | The same code in the original *Complete Collection* namespace, kept compatible with the upstream project. |

## Building

Requires the .NET 6 SDK, a MelonLoader-installed copy of the game, and a built [PvZReCoreLib](https://github.com/kedzie/PvZReCoreLib). Adjust the `HintPath`s in `PvZCompleteCollection.csproj` to your install, then:

```
dotnet build -c Release
```

The asset bundles are produced separately from the ripped PvZ2 assets and are not part of this repository.

## Credits

- **Draco9990**: the original *Complete Collection* and *PvZReCoreLib*, which this project is built on.
- **VersusPlus** by CougatDaCube: zombie card pictures (read from the player's own install; nothing is shipped).
- **BloomEngine** by PalmForest: the optional in-game menu.
- Plant art, animation and sound are taken from *Plants vs. Zombies 2*.

*Not affiliated with, or endorsed by, EA or PopCap. Plants vs. Zombies is a trademark of Electronic Arts Inc.*
