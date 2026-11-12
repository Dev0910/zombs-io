# Zombs.io Offline

A single-player Unity 2D recreation of [Zombs.io](https://zombs.io/).

## Play on Windows

Run `Builds/Windows/Zombs.io Offline.exe` with its accompanying files and data folder. The game uses local files for settings and best scores and can run without a network connection.

## Open or build the project

Open this folder with Unity `6000.4.9f1`. Open `Assets/Game/Survival.unity` and press Play. Edit `Assets/Game/Balance.asset` to tune the economy, buildings, items, enemies, and waves. The sprite drawings are in `Assets/Game/Runtime/SpriteArt.cs`.

Use **Zombs → Build Windows x64** to rebuild. Run the Edit Mode suite in the Unity Test Runner. The development build also accepts `-zombs-playthrough <output-folder>` to exercise the normal-speed survival loop. Add `-zombs-fast` for accelerated testing or `-zombs-first-boss` to accelerate preparation and return to normal speed before the first boss.

## Controls

| Input | Action |
| --- | --- |
| WASD or arrow keys | Move |
| Mouse and left click | Aim, harvest, attack, build, or select a structure |
| Space | Toggle auto-attack |
| G or the Gold Stash icon | Place the first Gold Stash |
| 1–0 | Wall, Door, Slow Trap, Arrow, Cannon, Melee, Bomb, Mage, Gold Mine, Harvester |
| R | Rotate a Melee Tower preview |
| E / Shift or Alt + E | Upgrade a building / upgrade matching buildings at that tier |
| T | Sell the selected or hovered building |
| B / Q / F / H | Shop / cycle weapons / use health potion / call pet |
| Esc / right click | Cancel the current tool. Esc also opens the pause menu. |

## Reference and differences

- The browser playthrough covered gathering, placement, upgrades, purchases, a full day-night cycle, death, and stash loss. The second run survived wave 8 and showed 100 tokens at the wave 9 loss. The first boss is on wave 9, after eight regular waves.
- The reference and [official changelog](https://zombs.io/changelog) informed early costs, upgrade gates, pickaxe yields, mine income, and harvester capacities. Later wave composition, boss health and damage, some upgrade costs and rates, and pet progression use editable estimates.
- The map, sprites, and sound effects are original recreations. The offline menus use local scores. Horns and pets are available from the shop without external accounts. Local pause freezes the simulation, and a fresh run clears the current balance and base.

## Verification

The Edit Mode suite covers harvesting, placement and payment, mine income, bulk upgrades through tier VIII, tower attacks, spells, boss timing, late waves, and the two respawn paths. The packaged playthrough used normal speed for the wave 9 boss fight and the following loss and restart. The Windows release was also played in an AppContainer with zero network capabilities.

Playthrough logs and screenshots are in `Builds/Verification/Evidence`.
