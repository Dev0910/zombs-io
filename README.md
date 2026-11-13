# Zombs.io Offline

A single-player Unity 2D survival game. Gather and build during the day, then defend the Oxygen Generator against stronger zombie waves each night.

## Play on Windows

Run [Zombs.io Offline.exe](Builds/Windows/Zombs.io%20Offline.exe) with its accompanying files and data folder. The game runs offline and saves local settings and best scores. The sci-fi visual direction and verification results are in the [art and UI review](ART-REVIEW.md). Leave **Map seed** blank for a new layout or enter a whole number to replay a layout. Find the current seed in the pause menu.

The game keeps one local survival run. Choose **Save & Main Menu** from pause, or close the game normally to save. It also autosaves every 30 seconds during play. Choose **Continue** from the title screen to return to the same map and wave. **New Run** asks before replacing an existing checkpoint.

See the [project backlog](BACKLOG.md) for current development priorities.

## First run

1. Press **G** and place the Oxygen Generator in the marked starting area. Placement starts the first day and gives you 90 seconds of fuel.
2. Gather wood and stone with the pickaxe. Build a few Arrow Towers and refuel before dark. Press **G** near the generator or click it to add wood or coal. Wood adds 6 seconds per unit and coal adds 20. Each click loads up to five units that fit.
3. Take short trips beyond the blue circle. You have five seconds of breath before poison damages your health. The reserve refills in clean air. The circle goes offline when the tank empties and returns as soon as you refuel.
4. Build a **Smelter (9)**. Queue two iron ore per plate and collect the result. Use two plates to build an **Assembler (M)**. Queue one plate and one wood per part.
5. Spend parts on weapon, defense, and generator upgrades. Generator upgrades expand the air radius and tank and raise the building tier limit. Tier II unlocks the **Miner (0)**. Place it next to a deposit, fuel it, and collect what it extracts.

At Tier III, a Smelter pulls iron directly from a nearby iron Miner, and an Assembler pulls plates directly from a nearby Smelter. Upgrade each receiving machine to Tier III and keep the machines within 6 world units center to center. The Assembler draws wood from your inventory for each automatic job. Miner fuel and finished-part collection stay manual.

Ore and stone occur in deposits that deplete. Starter materials are close to the placement area. The tree beside the starting point provides renewable emergency fuel.

## Controls

| Input | Action |
| --- | --- |
| WASD or arrow keys | Move |
| Mouse and left click | Aim, harvest, attack, place, or select |
| Space | Toggle auto-attack |
| G | Place or open the Oxygen Generator |
| 1–8 | Wall, Door, Slow Trap, Arrow, Cannon, Melee, Bomb, Mage |
| 9 / M / 0 | Smelter / Assembler / Miner |
| R | Rotate a Melee Tower preview |
| E / Shift or Alt + E | Upgrade a building / upgrade matching buildings at that tier |
| T | Sell a building |
| B / Q / F / H | Shop / cycle weapons / use health potion / call pet |
| Esc / right click | Cancel the current tool. Esc opens the pause menu when no tool is active. |

## Open or build the project

Open this folder in Unity **6000.4.9f1**, open [Survival.unity](Assets/Game/Survival.unity), and press Play. Tune timings, fuel, recipes, buildings, equipment, and waves in [Balance.asset](Assets/Game/Balance.asset). The default values used by **Zombs → Restore Default Balance** are in [GameCatalog.cs](Assets/Game/Runtime/GameCatalog.cs).

Use **Zombs → Build Windows x64** to rebuild. Run the Edit Mode suite in Unity Test Runner. The development build accepts **-zombs-playthrough <output-folder>** to run the opening through two nights, an iron trip, production, the Miner unlock, and recovery from an empty tank. Add **-zombs-fast** for an accelerated check. **-zombs-first-boss** also runs a separate defense scenario with supplied build materials and returns to normal speed before the first boss.

Add **-zombs-art-review** to capture title settings, valid and blocked placement, production, oxygen recovery, all shop tabs, every building type, a dense-wave fixture, and both loss screens. **-zombs-art-motion** also captures short normal-speed harvesting and combat sequences. The Sound setting controls both action cues and ambience.
