# Level rebalance — 2026-10-22

55 of the 70 gameplay JSON files were revised. Existing level identities, asset GUIDs, gameplay rules, packages and project settings are preserved. Changes use the existing linked-secret gameplay support.

## Shorter opening lessons

Reference clicks are successful simulated solution lengths, not minimum moves or measured player completion times.

| Level | Boxes before → after | Reference clicks before → after |
|---|---|---|
| 5 | 6 → 5 | 11 → 7 |
| 6 | 7 → 5 | 15 → 10 |
| 7 | 8 → 6 | 27 → 10 |
| 8 | 7 → 6 | 23 → 12 |
| 9 | 9 → 7 | 18 → 12 |
| 10 | 7 → 6 | 14 → 12 |
| 11 | 8 → 6 | 38 → 11 |
| 12 | 8 → 7 | 15 → 12 |
| 13 | 10 → 7 | 24 → 13 |
| 14 | 9 → 8 | 16 → 15 |

Level 10 keeps its first Hard label and four-slot road, with a compact generated six-box layout. Level 46 becomes an eight-box, six-color recovery lesson with a wide seven-slot road and two buried linked secrets. The two levels now have different layouts, capacities and central mechanics.

## Secret placement and mechanic combinations

Secret selection proceeds by depth across boxes, keeping the initial top cubes visible. Keys, ice counters and special colors stay readable. A normal cube matching the cube directly above it is excluded from secret selection, so a mystery does not fragment an otherwise readable same-color run. Percentage settings apply to these eligible color boundaries rather than all buried cubes. There are 359 secret cubes, compared with 581 before this revision.

| Level | Linked secret lesson |
|---|---|
| 17 | One visible linked member and one buried secret member |
| 33 | One buried linked secret with adjacent locks |
| 46 | Both linked members hidden, under one and two covering cubes |
| 52 | One buried linked secret combined with ice and color locks |
| 59 | One buried linked secret combined with a group door and color lock |

Levels 15–16 retain ordinary links for introduction. Other linked levels retain ordinary pairs to keep the campaign from repeating the same reveal pattern. Each pair still has exactly two members in separate normal boxes; cube colors and counts are preserved when changing the secret/link assignment on an existing board.

The editor generator uses the same placement policy. Failed generation preserves the existing level report. When the archived campaign specification is absent, learning objectives come from the published reports, with explicit objectives for the revised linked-secret lessons.

A one-line compile fix in `SROptions.Cheats.cs` changes the debug level display from the nonexistent `BuiltinLevelCatalog.Count` to the existing `HighestLevel` property. No gameplay behavior depends on this display.

## Verification

- Unity audit: `70 structural/simulation/contracts passed`, including successful final-JSON solution replay, color stock, box footprints, path mappings and linked member constraints.
- Generator geometry smoke checks: `PASS generator geometry catalog smoke checks`.
- Player and level editor compile: `PASS player and level editor compilation`.
- Initial core verification: `No-Find policy verified: 0 violations across Assets/00Game/01Scripts.` and `Core game verification passed.` A later repeat was blocked by Windows Application Control (`0x800711C7`), including outside the sandbox.
- Unity Play Mode: levels 5–14 passed. Full campaign replay and linked-secret lifecycle results are recorded in the per-level reports and the final note below.

The prior reference-click bands are retained as review signals. Levels 45, 60–65 and 68 have shorter valid reference solutions after secret placement changes; they require human balance review. Audit still rejects shallow color layouts and verifies the actual solution instead of padding its length to fit an old target.

Human completion time, restart rate and final visual acceptance remain unverified. In the Editor, review the first-time secret reveal at level 6, blocked linked taps and reveal/Undo at 17, both reveal depths and restart at 46, and interaction with ice/color locks and group doors at 52/59. Inspect cable halves before reveal to confirm they conceal secret colors.
