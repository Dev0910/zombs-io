# First-time Windows playtest

Use a fresh profile and a recorded seed. Recruit players who have never played this build. Start at 800 × 600 for one player and 1280 × 800 for another. Give each player the four tasks one at a time and let them use the on-screen guidance. Record time to first attempt and completion, wrong actions, requests for help, and whether any prompt had to be supplied.

| Task prompt | Observe |
| --- | --- |
| Start a run and make the area breathable. | Finds Deploy, selects and places the generator, and understands the allowed area and blocked preview. Ask the player to try Enter and G and note where mouse input is required. |
| Keep the air running through the first night. | Finds wood and stone, reads the fuel meter, selects the generator, loads fuel, and places a defense. Record fuel at dusk and dawn. |
| Make and collect one part. | Finds iron, returns to air, builds the Smelter, collects plates, places the Assembler, queues a part, and collects it. Note hidden actions, clipped text, and any difficulty returning to the machines with keyboard movement. |
| Restore air after the tank empties. | Recognizes the outage, finds an emergency wood source, adds fuel, and notices the breath reserve recover. If the suit dies, observe whether the player can resume the run. |

Check Tab, Shift+Tab, Enter, and Escape on the title, selected-machine panel, and recovery screen. Record visible focus and whether each action can be completed. Give a hint only after the player asks or spends two minutes without progress. Mark the task assisted and record the hint verbatim.

## Internal evidence

The updated Windows development player passed the scripted 800 × 600 opening and recovery sequence on seed 5238. It placed the generator, refueled, produced parts, recovered from an empty tank, and exercised both loss screens and restart. See `Builds/Verification/Remaining-Compact-Visible/playthrough.log`. The prior keyboard smoke check covered Enter, G, B, and Escape. Placement uses a pointer and machine recipe actions use on-screen buttons. External sessions and a full keyboard focus audit are open.
