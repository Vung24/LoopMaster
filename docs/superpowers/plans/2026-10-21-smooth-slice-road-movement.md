# Smooth Slice Road Movement Implementation Plan

**Status:** Revised after review; implementation has not started.

**Goal:** Reduce movement stalls, visible snapping, and unintended corner cutting while preserving authoritative path/index updates, conveyor cadence, launch timing, and box arrival behavior.

**Scope:** `Slice.cs`, `ConveyorFlow.cs`, `PathArrow.cs`, and verification. Modify `GameController.cs` only if required to establish an explicit road visual update phase. Rotation easing is a separate follow-up.

**Tech stack:** Unity 2022.3 LTS, existing C# language level and packages, UniTask, DOTween.

## Constraints

- Follow repository `AGENTS.md`. Do not use `transform.Find` or `GameObject.Find`.
- Keep logical state authoritative: path indices, movement eligibility, box admission, and win/lose checks must not depend on visual completion.
- Preserve public APIs, serialized data, path wrapping, section transitions, overlap rules, group selection, launch timing, and event ordering.
- Retain the existing multi-tick scheduler, normal/speed due-time ordering, tolerance, accumulator cap, and failure slowdown behavior. Do not replace `while` with single-tick `if` in this work.
- Do not add packages, change assembly definitions or ProjectSettings, or edit scenes/prefabs/materials. Preserve GUIDs and `.meta` files for any new verification assets.
- Introduce no per-frame/per-tick managed allocations. Measure steady-state allocations separately from initialization and collection warm-up; do not claim the whole existing flow is allocation-free without evidence.
- Preserve owned tween cleanup and `.SetLink(gameObject)`. Road interpolation must not interfere with jump callbacks or scale animations.
- Prefer named methods and guard clauses. Avoid unrelated refactors and automatic commits.

## Evidence and verification limits

Inspect current source and callers alongside:

- `Rules/RoadSpeedData/decompiled.c`
- `Rules/SliceCatchUpData/Methods/GameController__UpdateTick.c` and movement/group-selection methods in that directory
- `Rules/SliceCatchUpData/UnityVerification/Assets/Editor/SliceCatchUpValidation.cs`
- `Rules/ProgressData/FullProjectVerification.csproj`
- `Rules/Verification/CoreRulesVerification.csproj`

Native `UpdateTick` uses at most one normal and one speed tick per call. This is historical evidence, not sufficient justification to change the current scheduler during a visual fix. Current intervals can be shorter than a frame: with normal interval `0.05s` and spacing scale `0.4`, the fast interval is `0.0128s`. A one-tick-per-frame limit would reduce throughput at 60 FPS.

`CoreRulesVerification.csproj` does not currently compile `Slice`, `PathArrow`, or `ConveyorFlow`. Passing it verifies linked gameplay rules and No-Find policy; runtime compilation and Unity movement verification remain separate requirements.

## Timing contract

Establish one explicit source and phase for road visual time. Conveyor ticks originate from `GameController.Update` using `Time.deltaTime`. Road visuals must not count a frame twice or rely on relative `Update` order between GameObjects.

Distinguish these quantities:

1. **Scheduler backlog:** Time retained by conveyor accumulators. It may exceed an interval while ticks remain due and is subject to the existing `0.5s` cap.
2. **Continuous movement overshoot:** Time past a completed visual segment eligible for an already scheduled contiguous successor. Consume it once; it may cover several known segments during catch-up.
3. **Idle time:** Time after reaching a destination with no eligible successor. Discard it; it must not accelerate movement after a blockage clears.

Required behavior:

- First road movement starts from the actual initialized/landed transform with no stale residual.
- Contiguous movement follows waypoint geometry. Derive direction from the intended segment rather than a diagonal from an intermediate position across a corner.
- Do not anchor unconditionally to the previous target just because `_isRoadMoving` is true. An early successor must preserve the unfinished route without snapping or erasing the time deficit with `Max(0, elapsed - duration)`.
- Several move requests in one frame must retain route order without overwriting intermediate segments or advancing logical state a second time.
- At the final stop waypoint, stop accumulating visual time immediately. Do not use a `duration + 0.1s` timeout to retain continuity.
- Preserve intentional discontinuities at non-circular section boundaries and reset continuity there.
- Keep current rotation easing initially; evaluate angular behavior separately after timing is correct.

**Implementation decision gate:** Before Task 2, document the selected shared timing phase, early-successor behavior, and reset rules here. Use the smallest visual state that satisfies the contract. If pending segments are necessary, use reusable bounded storage with an explicit overflow policy and lifecycle cleanup. Store only visual segment data, not a second authoritative path/index collection. Do not conceal overload with teleportation. Validate any storage bound against catch-up and speed changes before implementation proceeds.

## Task 1: Capture baseline and reproduce failures

**Files:** Read movement source/callers; extend focused Unity verification under `Rules/` as needed.

- [ ] Read `GameController.Update`, interval calculation, speed switches, failure slowdown, conveyor group selection, transfer landing/box admission, and slice/arrow initialization and cleanup.
- [ ] Capture baseline tick order/counts, logical indices, launch count, box admission events, and end-state transitions for deterministic fixtures.
- [ ] Reproduce early ticks, late ticks, multiple ticks in one frame, and blocked-to-resumed movement using actual runtime classes.
- [ ] Cover normal/speed membership changes within a frame; do not assume one move request per slice per rendered frame.
- [ ] Inspect the fixture time driver. `DOTween.ManualUpdate` alone does not advance custom road interpolation. Avoid advancing both manually and automatically for the same elapsed time.
- [ ] Use straight, right-angle, closed-loop, and disconnected-section fixtures. Define geometric expectations independently of the implementation formula.
- [ ] Complete the timing decision gate above before editing movement code.

## Task 2: Coordinate conveyor and visual time

**Files:** `Assets/00Game/01Scripts/02Gameplay/Flow/ConveyorFlow.cs`; `Flow/GameController.cs` only if required; focused verification.

- [ ] Keep scheduler semantics, including chronological normal/speed ordering and recomputation of failure intervals after ticks.
- [ ] Supply timing/order information needed to distinguish continuous successors from idle gaps and consume catch-up requests in order.
- [ ] Give one owner responsibility for advancing road visual time exactly once per elapsed interval. If controller orchestration is selected, remove independent advancement of the same motion from component `Update` methods.
- [ ] Preserve existing behavior when ticking becomes disallowed. Check shop/booster gating, `Time.timeScale == 0`, and failure slowdown; do not introduce a new pause policy silently.
- [ ] Compare logical traces against Task 1, including speed toggles and both tick types due in the same frame.

## Task 3: Correct Slice continuity and cleanup

**File:** `Assets/00Game/01Scripts/02Gameplay/Elements/Slice.cs`; focused verification.

- [ ] Preserve `MoveNextPath(float interval)` and its logical update responsibilities. Add internal timing plumbing only where needed.
- [ ] Implement the selected timing model, preserving unfinished route segments and ordered multi-tick catch-up.
- [ ] Separate road-state cleanup from tween cleanup where necessary so starting a contiguous segment does not erase valid pending visual movement.
- [ ] Retain positive-duration and direction-magnitude guards; validate required setup without adding null-sensitive accesses.
- [ ] Preserve non-circular section transition position/rotation and reset old visual continuity.
- [ ] Reset timing and pending visual data through `StopMotion`, initialization, pool reset, disable, jump entry, undo dismissal, and section discontinuities as applicable.
- [ ] Verify road movement starts from the landed pose after `JumpToTransfer`; stale road updates cannot overwrite `JumpToBox` or dismissal.
- [ ] Verify blocked resume has zero idle-time debt and final segment completion stops visual time accumulation immediately.

## Task 4: Apply the timing contract to PathArrow

**File:** `Assets/00Game/01Scripts/02Gameplay/Board/PathArrow.cs`; focused verification.

- [ ] Preserve arrow cadence: every other launch tick, using duration `interval * 2f`.
- [ ] Handle early/late successors and multi-tick frames without `CompleteMove()` snapping an unfinished segment or discarding eligible overshoot.
- [ ] Keep exact endpoint assignment safe and idempotent. Removing the snap on tick arrival must not remove correct final completion.
- [ ] Preserve initialization, height offset, wrapping, section transitions, and existing rotation easing.
- [ ] Clear visual continuity on disable and reinitialization; verify reuse and disable/re-enable.

## Task 5: Verify and report

### Automated checks

- [ ] Run `dotnet run --project Rules/Verification/CoreRulesVerification.csproj` for linked rules and `VerifyNoFindPolicy()`.
- [ ] Run `dotnet build Rules/ProgressData/FullProjectVerification.csproj --no-restore` to compile runtime classes. Report environmental reference failures separately from code failures.
- [ ] Add and run focused Unity checks against actual changed runtime classes. Reuse existing fixture patterns; inspect launcher scripts before use because they may prepare a separate project or copy settings. Keep the user's main project settings untouched.
- [ ] Compare equal simulated elapsed time at 30/60/90/120 FPS, including minimum spacing and speed intervals shorter than a frame. Declare numeric tolerances explicitly.
- [ ] Cover delta `0`, ordinary deltas, `0.05s`, `0.2s`, and a spike beyond the existing accumulator cap. Preserve baseline cap behavior; do not claim discarded time is retained.
- [ ] Cover a `0.05s` segment followed by `0.04s` of actual idle time: the next move receives no carried idle time.
- [ ] Cover early successors and several successors in one frame: assert waypoint order, no corner shortcut, and no handoff positional discontinuity beyond tolerance.
- [ ] Cover blocked/full roads, closed groups, unblocking, catch-up membership changes, speed toggles, failure slowdown, box arrival, win/lose, restart/reload, jump interruption, pooling, and disable/destruction.
- [ ] Only assert scheduler residual is below an interval after due ticks are drained under stable intervals, allowing float tolerance. Do not impose this bound on all timing state at all times.

### In-Editor visual and performance checks

- [ ] Observe straight paths, sharp corners, wrapping, and section transitions under normal/speed movement and spikes. A long frame cannot display every intermediate waypoint; verify route processing separately from rendered samples.
- [ ] Compare displacement over time on equal-length segments. Do not claim constant world-space velocity over unequal-length segments without distance-based timing.
- [ ] Use Unity Profiler after warm-up to check introduced road-motion GC allocations. Capture evidence before claiming performance improvements.
- [ ] Evaluate residual angular jerk after timing passes. Make easing changes a separate follow-up only when observations justify them, documenting the visual difference.

### Acceptance and completion report

- [ ] Logical movement/event traces preserve baseline behavior for identical inputs.
- [ ] Continuous movement preserves route order without handoff snaps, repeated time consumption, or idle-time debt.
- [ ] Jumps, stops, section transitions, pooled objects, disabled objects, and restarts invalidate old visual state correctly.
- [ ] Review changes for accidental scene/prefab/`.meta`, dependency, settings, and unrelated edits.
- [ ] Report changed files, selected timing model, gameplay impact, actual checks/results, observations, and remaining risks.
- [ ] If Unity validation cannot run, mark movement acceptance incomplete and list specific pending checks. Backend success alone is not movement validation.
