# Booster unlock verification

Quick runtime test: press **F8**, open **05. Booster Unlock Test**, then choose Undo (4), Shuffle (7) or Extra Slot (9).
Each button resets that booster's intro/free-use flags, disables Skip Tutorial and enters the milestone level.
Inventory counts are preserved. Repeat the button to replay the entire introduction; Skip Tutorial stays disabled afterward.

Run `dotnet run --project Tests/BoosterUnlock/BoosterUnlockVerification.csproj` from the repository root.
The harness links the production unlock registry, progress and inventory transaction code.
`TestEnvironment.cs` substitutes Unity hosting, storage and unrelated reward-journal services; `TestBoosterConfig` reads the shipped ScriptableObject YAML into the test host.
It checks milestone boundaries, free use with zero/existing stock, failures, exceptions, reentrancy and replay.
It does not validate Unity persistence, rendering, touch routing or tween timing.

Run `node Tests/BoosterUnlock/verify-prefab.cjs` to check prefab fileIDs, serialized UI references and removal of runtime UI builders.
Edit `Assets/00Game/Resources/BoosterPresentation.prefab`: `Dialog` contains Title/Glow/Icon/TapToContinue;
`FlightIcon` is the flight visual; `Tutorial` contains the four shades, Focus button and Tooltip.
Only enable one presentation child for preview; save all three children inactive for runtime phase selection.

Repeat these checks in Unity 2022.3 with an isolated player profile:

1. Enter levels 4, 7 and 9: board, delay, title/icon/tap popup, flight to the correct locked HUD slot, then unlock.
2. Undo: tap the highlighted movable box, wait for movement to settle, then tap Undo. The move is restored.
3. Shuffle: tap the highlighted tool, then choose any eligible highlighted box, including a box other than the first one. No stock is spent when opening selection. Cancel selection and confirm its free use remains available.
4. Extra Slot: tap the tool and confirm the road capacity increases through the existing path action.
5. With stock 0 and stock 3, confirm first use opens no purchase dialog and leaves stock unchanged; next successful use costs 1.
6. Restart/exit during popup, flight, each guidance step and Shuffle selection. Confirm overlays, locks and tweens clean up.
7. Restart after successful use: no repeated popup or free grant. Restart after flight but before use: guidance resumes.
8. Enable SkipTutorial: no popup or guide, normal milestone unlock remains available.
9. Repeat at narrow/tall/wide aspect ratios and with safe-area insets; confirm icon landing and focus touch areas.
10. At the booster button step, confirm the dim has a circular opening around the button (its four surrounding corners stay dim), the hand is centered on it, and only "First use is free" appears above it.
