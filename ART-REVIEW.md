# Zombs.io Offline art and UI review

## Art direction

The redesign depicts a containment outpost on a basalt mining site. The generator is the largest source of teal light. Armor and machinery use graphite, pale alloy, and restrained highlights. Amber marks heat, activity, and caution. Coral marks hostile or damaging states. A warning, structure, or deposit has a shape, label, or pattern that carries its meaning when color is hard to distinguish.

The flat, brightly outlined baseline was fast to read at small sizes but gave the oxygen system and factory progression little visual context. The redesign takes the modular clarity of a top-down industrial game and adds localized light, a textured mineral ground, and stronger silhouettes. A fully rendered 3D direction would require rebuilding the current 2D art and effects pipeline. The gameplay captures keep the same orthographic camera and use seed 5238 for comparison.

| Reference | Applied to this project |
| --- | --- |
| [Mindustry official gameplay media](https://mindustrygame.github.io/) | Shared footprints and distinct weapon heads help a dense factory and defense line read at the same scale. |
| [The Riftbreaker graphics retrospective](https://www.exorstudios.com/blog/rb-graphics-story) and [minimap changes](https://www.exorstudios.com/blog/rb-community-changes) | Suit geometry separates the operator from the horde. Local lighting establishes activity, and symbols help players navigate around the core. |
| [Factorio on resource art](https://www.factorio.com/blog/post/fff-179), [machine and interface states](https://www.factorio.com/blog/post/fff-363), and [quickbar positions](https://www.factorio.com/blog/post/fff-278) | Deposit geometry distinguishes materials, the quickbar retains slot positions, and machine status appears both on the sprite and in the inspector. |
| [Dome Keeper](https://rawfury.com/games/dome-keeper/) and [Alien Swarm](https://store.steampowered.com/app/630/Alien_Swarm/) | The core anchors the return from a dangerous extraction trip. Pale environmental suits and ragged infected outlines establish the two sides before names or health bars appear. |

The title illustration was made with built-in ImageGen and is packaged as [Outpost.png](Assets/Game/Resources/Presentation/Outpost.png). The brief specified a basalt wasteland, a cyan containment reactor and ivory operator on the right, and a dark, low-detail area on the left for the menu. Gameplay sprites are authored in [SpriteArt.cs](Assets/Game/Runtime/SpriteArt.cs). The Barlow and Barlow Condensed fonts are packaged with their respective licenses.

<details>
<summary>Title illustration prompt</summary>

Create one landscape 16:9 original video game title-screen background illustration, no lettering, no logos, no UI. Sophisticated stylized hand-painted sci-fi industrial survival key art, crisp broad shapes and controlled texture, graphite-blue volcanic wasteland with fractured basalt and soft toxic blue-grey fog. Put the focal subject in the RIGHT half: a compact hexagonal modular oxygen reactor with an unmistakable cyan circular core and glass vents, bolted alloy containment walls, a lone ivory EVA-suited scavenger and tiny warm orange service lamps. Suggest threatening silhouettes in the far-right mist, no gore. Camera oblique from above, readable industrial shapes, subtle depth, grounded utility, restrained cyan and amber highlights. LEFT 43 percent is genuinely dark and quiet empty basalt and fog with little detail and no distinct objects, reserved for white title/menu overlay. Keep the strongest cyan brightness near x=70%. Finished high-end indie game cover illustration, no typography and no borders.

</details>

## Review criteria

Each element scores from 0 to 10 at the actual camera and display scale. A 5 is functional with recurring visible problems. A 7 is coherent during ordinary play with material gaps in a demanding state. A 9 is polished in the verified scenarios with minor limitations. A 10 requires complete evidence with no identified gap. The overall score is the unrounded arithmetic mean. Clipped primary controls, a misleading hazard or footprint, and persistent combat occlusion each prevent a score above 9 until corrected. An untested scenario stays unverified.

The review checks text against its weakest rendered backdrop and looks for a second cue alongside color for critical states. Contrast and motion are based on the Windows captures and the tested display sizes. These checks do not establish platform accessibility certification or represent player survey scores. [Xbox contrast guidelines](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/102) · [Additional cue guidelines](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/103) · [Riot gameplay clarity](https://www.leagueoflegends.com/en-us/news/dev/clarity-in-league/)

## Baseline findings

The original Windows captures are in `Builds/Verification/Oxygen-Final`. The findings below drove the redesign.

| Element | Baseline evidence and player impact |
| --- | --- |
| Title and deployment | The setup form shared the ordinary grass backdrop. The opening gave little sense of the oxygen-driven setting. See `01-title.png`. |
| Terrain and deposits | The persistent grass grid competed with machinery. Similar outlined rock forms relied heavily on hue. See `05-poison-exposure.png` and `06-assembly.png`. |
| Operator, companions, and infected | The operator and boss shared a round body. The boss depended on its crown, weapon, and banner. See `10-first-boss.png`. |
| Machinery and defenses | Brown square frames dominated the base. The machine inspector conveyed working state more clearly than the world sprite did. See `03-generator-controls.png` and `06-assembly.png`. |
| Oxygen and placement | Coverage matched the simulated radius, and invalid placement used an X. The enlarged outline showed jagged edges. See `02-generator-preview.png` and `09-fuel-outage.png`. |
| HUD and navigation | The clock needed hover to expose its meaning, and several empty loadout slots were hard to see. See `04-first-wave.png`. |
| Inspection and equipment | The inspector covered the operator and nearby structures. The refund wrapped into the Sell action. The resource tooltip could overlap inventory. See `06-assembly.png` and `10-first-boss.png`. |
| Combat and effects | The boss announcement covered the threat as it entered. Repeated hits accumulated floating numbers in crowded fights. See `09-fuel-outage.png` and `10-first-boss.png`. |
| Settings, pause, and loss | The baseline capture series did not show these states. The original source used the same generic panel for these screens. Baseline runtime appearance is unverified. |

## Final review

The independent desktop re-review scored **9.10/10** at 1280 × 800. This is the arithmetic mean of the ten areas below. The review used the packaged Windows player and compared the same seed and camera with the original screenshots. The first redesign review scored 8.5/10 across the nine areas it could assess. Its findings drove the second pass. The ten review areas passed in the specified scenes. The deductions record visual limitations that remained after the fixes.

| Element | Final score | Verified change and remaining deduction |
| --- | ---: | --- |
| Title and deployment | 9.3 | The reactor illustration gives the title a focal point and leaves a dark field behind the launch controls. The compact crop trims some scenery at the right edge. |
| Terrain and deposits | 8.8 | Rock, coal, iron, and wood have distinct profiles and matching map symbols. Ground details repeat across large empty areas. |
| Operator, companions, and infected | 9.0 | The operator has a visor and air pack. Both companions read beside the operator. Ragged infected, hostile markers, and the boss’s armor distinguish threats at night. |
| Generator and production | 8.8 | Working and idle states read in the world and inspector. Fuel and recipe buttons describe the amount they will use. Several machines share a similar chassis and rely on their center shapes for recognition. |
| Defenses and equipment art | 9.0 | Each tower head has a different outline, and the same motif appears in its toolbar icon. All building types were placed in the review fixture. |
| Oxygen and placement | 9.5 | Solid active coverage, a dashed outage boundary, a reserve meter, and plain-language warnings distinguish the states. Blocked placement uses the footprint, an X, and a reason. |
| HUD and navigation | 9.2 | Suit, phase, map, materials, and tools have stable positions. The phase panel and guidance leave the central fight clear. |
| Inspector and shop | 9.0 | The inspector leaves the operator visible. The shop shows stock and price, and disabled actions explain unavailable materials, fuel, or output. The final pass also gave companion evolution its level and token requirements. |
| Settings and loss | 9.4 | Pause retains context. Suit death and generator loss have separate recovery explanations, and the loss screen hides live telemetry. |
| Combat and motion | 9.0 | The normal-speed clip shows turret turns and short projectile trails. The dense-wave fixture keeps the operator and reactor readable with 24 live enemies within 14.5 world units. |

Desktop evidence is in `Builds/Verification/Art-Release`. `night-motion.mp4` contains five seconds from the first wave, captured at 12 frames per second. The log recorded a 7.0 ms 95th-percentile frame time for the brief sample without PNG capture on the review machine. It rose to 69.4 ms during PNG capture, so the exported clip is visual evidence and the recording interval is a separate performance measurement. Sampled text contrast was 6.77:1 for Deploy, 7.06:1 for an inactive shop action, and 11.24:1 for loss-screen body text.

In the release player, keyboard and mouse input verified Enter to deploy, G to select the generator, clicking to place it, B to open the shop, selecting the Pets tab, adopting C.A.R.L., and Escape to close the shop and pause. The review did not measure recognition times with external players or certify display sizes beyond those tested.

The 800 × 600 Windows run passed the same opening and review scenarios. It showed a clear boss entrance, completed the first boss night at normal speed with 100 boss tokens, and captured the dense fight, all five shop tabs, and both recovery screens. The Pets tab showed the disabled evolution action with its level and token requirements. See `Builds/Verification/Art-Release-Compact/playthrough.log`. All 12 Unity Edit Mode tests passed on the packaged revision.
