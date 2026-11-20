# SPT Customs Classic

Classic Customs, playable on SPT 4.1 as a variant of Customs (`bigmap`). You pick which one you want at the map
screen; the Customs SPT ships is untouched and one click away.

Unlike a map the game still carries, the classic layout has to be brought in whole: the mod ships it as a map
bundle, and loads it in place of the modern scenes when you pick the Classic tile. The modern map's weather,
airdrops, exits and sound systems stay loaded underneath, moved to where the classic layout needs them.

> **Quests.** Customs quest objectives are placed for the modern map; many cannot be completed on Classic. The map
> screen says so when you pick the tile. Play quest raids on the vanilla tile.

## What you get

- **Picked, not forced.** The map screen offers Customs - Vanilla and Customs - Classic, and every player in that
  raid loads what the host picked.
- **The classic layout** with its own player spawns, exits, loot containers and loose loot positions.
- **Bots that work on it.** The navmesh, cover points and patrol routes are the classic map's own, and bot spawn
  points with no classic ground under them are switched off.
- **No bosses unless you want them.** Classic Customs ships with Reshala, the Goons and the cultists at a 0% chance.
  A config file turns them on, and says where each may appear (below).
- **The bundle borrows from your install.** Most textures the classic map shares with the game are read from your
  own SPT 4.1 client rather than shipped, which keeps the download down.

## Requires

- SPT 4.1
- **MapVariants 1.1.0 or later** (`com.lennoxp90.mapvariants`), required, not optional. It owns the map-screen
  choice for every map that has a second version, this one included. Without it neither half of this mod loads and
  Customs is the map SPT ships.
- Fika is optional. The variant choice travels with the raid the same way it does for FactoryClassic.

## Install

Extract the archive over your SPT install folder, so that `BepInEx` and `SPT_Runtime` land next to the ones already
there. Restart the server.

## Configuration

Server side, in `user/mods/LennoxP90-CustomsClassic/config/bosses.json`:

```json
{
  "bossBully": { "chance": 0, "zones": ["ZoneDormitory", "ZoneGasStation"] },
  "bossKnight": { "chance": 0, "zones": ["ZoneCustoms"] },
  "sectantPriest": { "chance": 0, "zones": ["*"] }
}
```

Change a `chance` to the percent you want per raid and save. The next raid uses it; no restart is needed. For
reference, modern Customs uses 39 for Reshala and 20 for the Goons and the cultists.

- A boss never starts in the area you spawn in, and two bosses never share an area. A boss with no area left sits
  the raid out.
- Cultists only appear in raids that start between 22:00 and 06:00.
- `zones` takes the game's bot zone names, or `"*"` for every area that has bot spawns on Classic.
- The file only affects Classic Customs. Modern Customs keeps the game's own boss settings, and a bot spawn mod
  keeps its PMCs, scavs and every other map.

## Known limits

- Many Customs quests cannot be completed on the classic tile, as above.
- Seasonal event objects are left as the game places them.

## Building

The code builds against an SPT 4.1 install:

```
dotnet build CustomsClassic.Server -c Release
dotnet build CustomsClassic.Client -c Release /p:SptRoot=<your SPT 4.1 folder>
dotnet test CustomsClassic.Tests
```

This repository holds the code and its data files. The map bundle and the generated bot data are not in it; they
ship in the release archive.

## Licence

MIT, see `LICENSE`. The licence covers the code in this repository. The map bundle in the release archive is not
covered by it.
