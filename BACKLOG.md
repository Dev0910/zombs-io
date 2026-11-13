# Zombs.io Offline backlog

| Priority | Status | Work | Done when |
| --- | --- | --- | --- |
| 1 | Done | Save and resume a survival run | A player can leave mid-run, relaunch, and continue from the title screen with the same map, inventory, base, and live wave. Starting over requires confirmation when a checkpoint exists. |
| 2 | Simulation passed | Play from a fresh map through the first boss | Seeds 1234, −19, and 5238 completed wave 9 with gathered materials. Human pacing and less efficient routes remain open. |
| 3 | Automated checks passed | Validate the opening across map seeds | Starter resource proximity checked on 132 seeds. Three fresh boss routes included first-night survival. Six seeds passed empty-tank recovery on harvested wood followed by a coal trip and refuel. |
| 4 | Implemented and reviewed at 800 × 600 | Improve machine and terrain readability | Reviewed title, day gallery, and a normal-speed dense wave. Desktop comparison remains open. |
| 5 | External sessions open | Observe first-time players | [Playtest protocol](PLAYTEST.md) and internal compact checks are ready. Recruit new players and complete the keyboard focus audit. |
| 6 | Implemented | Unlock machine-to-machine transfer | Tier III receivers pull from compatible machines within 6 world units. Save restore and manual Tier I assembly are covered in Edit Mode. |

The [art review](ART-REVIEW.md) records the remaining presentation issues. The [playthrough notes](README.md#open-or-build-the-project) describe the existing opening and boss checks.

Save and resume passed 15 Edit Mode tests and a Windows relaunch during wave 1. The Windows playthrough also passed two nights, ore processing, the Miner unlock, and recovery from an empty oxygen tank.

The fresh boss route built four Arrow Towers and two Bomb Towers for 40 wood and 40 stone. It burned 180 wood per run and reached first dawn with 90 seconds of fuel. The lowest fuel was 58.8–59.3 seconds. Smelting began at 128.8–129.3 seconds and Tier II was reached at 183.8–184.4 seconds. The first boss night ended at 1079.9 seconds on all three seeds. The scripted route stayed near the generator for refueling and relied on renewable wood. No economy changes were required by those runs. See `Builds/Verification/fresh-tests.xml`.

All 25 Edit Mode tests passed. See `Builds/Verification/verified-tests.xml`.
