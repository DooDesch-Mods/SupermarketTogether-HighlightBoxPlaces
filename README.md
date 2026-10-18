# HighlightBoxPlaces

> 🛟 **Need help or found a bug?** Get support at [support.doodesch.de/highlightboxplaces](https://support.doodesch.de/highlightboxplaces).

Find matching shelf rows and storage locations for the product box in your hands in Supermarket Together.
Downloads: [Thunderstore](https://thunderstore.io/c/supermarket-together/p/DooDesch/HighlightBoxPlaces/)
or [GitHub releases](https://github.com/DooDesch-Mods/SupermarketTogether-HighlightBoxPlaces/releases).

## Playing

Hold a normal product box. Matching assigned shelf rows and their shelves receive colored outlines:

| Color | Meaning |
| --- | --- |
| Red | Assigned location with no stock |
| Yellow | Matching location with room for more stock |
| Green | Matching location is full |
| Blue | Matching storage box or reserved storage slot |

Each row uses the game's product spacing and stacking rules. A shelf stays yellow when any matching
row has room, even if another row is full. Container class and restricted product volume are respected.
Unassigned shelf rows are not highlighted.

Changing products updates the marks. Putting the box away, opening a blocking menu, disabling the mod
or changing scenes removes them. Each player sees their own held product; the host and friends do
not need this mod. It reads local stock data and changes no inventory or network state.

## Install and settings

Requires BepInEx 5.4.21 or compatible BepInEx 5. Put `HighlightBoxPlaces.dll` in
`BepInEx/plugins/HighlightBoxPlaces`. Keep only one copy. Configuration is generated at
`BepInEx/config/HighlightBoxPlaces.cfg`.

| Section / key | Default | Meaning |
| --- | --- | --- |
| General / Enabled | true | Show matching locations |
| Display / Storage | true | Include storage boxes and reservations |
| Display / Through Walls | true | Draw outlines through other objects |
| Display / Refresh Interval | 0.25 | Stock refresh in seconds, 0.1 to 2 |
| Colors / Empty | Red | Assigned rows without stock |
| Colors / Needs Stock | Yellow | Rows with free capacity |
| Colors / Full | Green | Full rows |
| Colors / Storage | Blue | Matching storage locations |

The mod owns its effects and leaves existing game hover effects and other mods' profiles unchanged.
It supports normal product boxes; manufacturing containers use a separate game system.

## Development

Build from the workspace root with
`rtk proxy python -X utf8 Workspace/tools/workspace.py build --project HighlightBoxPlaces`.
The current game uses `ProductListing.ProductData` and `PlayerNetwork.OnChangeEquipment`; this
implementation follows local held state directly instead of patching the removed `ChangeEquipment`.
For a standalone checkout, point `SmtGameLibsDirectory` at `Supermarket Together_Data/Managed` and run
`dotnet build -c Release -p:SmtGameLibsDirectory="<path>"`. Game assemblies stay outside the public
repository. GitHub CI uses a pinned private reference snapshot.

Shelf colors, mixed rows, separate player views, product switching, storage signs and cleanup were
checked in the current game build. Licensed under the MIT License; see [LICENSE.md](LICENSE.md).
