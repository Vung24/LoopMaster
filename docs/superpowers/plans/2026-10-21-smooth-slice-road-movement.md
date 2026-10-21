# Smooth Slice Road Movement Implementation Plan

**Status:** Implemented; focused Unity Play Mode verification passed (65,032 assertions). In-Editor visual/performance acceptance remains pending.

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

Normal and speed ticks use two independent logical clocks to decide when movement is issued and which slices move. Every slice and arrow consumes one shared **Master DeltaTime**, sourced from `Time.deltaTime`; never add normal and speed tick intervals to the visual elapsed time. A slice changing tick membership must not receive a second visual advance for the same elapsed interval.

Selected orchestration: `GameController` supplies Master DeltaTime and `ConveyorFlow` coordinates road visual advancement around the existing chronological tick dispatch. Advance visuals to each tick's logical issue time, dispatch that tick, then advance the remaining frame time once. Remove independent road-time advancement from component `Update` methods. Intervals remain segment durations, not extra elapsed time. Specify how capped backlog maps to issue times before implementation; discarded scheduler time must not generate moves. Preserve existing jump tween ownership and update behavior.

Distinguish these quantities:

1. **Scheduler backlog:** Time retained by conveyor accumulators. It may exceed an interval while ticks remain due and is subject to the existing `0.5s` cap.
2. **Continuous movement overshoot:** Unconsumed time on the shared timeline belonging to contiguous permitted movement. Preserve it across render-frame boundaries and consume it once, including across several known segments during catch-up. An empty queue or `_isRoadMoving == false` alone does not prove that time is idle. Use logical issue times to distinguish continuity from a real wait; never spend time on movement before its permission/issue time.
3. **Idle time:** Time actually waiting because movement is disallowed. A failed `IsSliceCanMove` check breaks continuity for future unapproved movement, but must not cancel an unfinished segment already permitted. Do not carry blocked waiting time into resumed movement. Shop/booster gating and other non-ticking periods require the same explicit distinction while preserving their existing pause policy. Jump, reset, disable, and section discontinuity invalidate old road continuity separately.

Required behavior:

- First road movement starts from the actual initialized/landed transform with no stale residual.
- Contiguous movement follows waypoint geometry. Derive direction from the intended segment rather than a diagonal from an intermediate position across a corner.
- Do not anchor unconditionally to the previous target just because `_isRoadMoving` is true. An early successor must preserve the unfinished route without snapping or erasing the time deficit with `Max(0, elapsed - duration)`.
- Several move requests in one frame must retain route order without overwriting intermediate segments or advancing logical state a second time.
- At a genuine final stop waypoint, stop accumulating movement credit immediately. Preserve unconsumed shared-timeline time across ordinary render boundaries until its relationship to subsequent issue times is resolved. Do not use a `duration + 0.05s` or `duration + 0.1s` timeout to infer continuity.
- For a lag spike greater than `0.2s`, process all due permitted visual segments in order within that frame and render the resulting pose. This is timeline catch-up, not teleportation to the logical index: retain waypoint processing, section rules, and box/jump handoff ordering. Use the same algorithm for smaller frames; `0.2s` is a required verification trigger, not a reason to change scheduler behavior. Respect the existing `0.5s` accumulator cap and never synthesize moves for discarded time.
- Preserve intentional discontinuities at non-circular section boundaries and reset continuity there.
- Keep current rotation easing initially; evaluate angular behavior separately after timing is correct.

**Implementation decision gate:** Before Task 2, document the selected shared timing phase, early-successor behavior, and reset rules here. Use the smallest visual state that satisfies the contract. If pending segments are necessary, use reusable bounded storage with an explicit overflow policy and lifecycle cleanup. Store only visual segment data, not a second authoritative path/index collection. Do not conceal overload with teleportation. Validate any storage bound against catch-up and speed changes before implementation proceeds.

The selected direction above fixes the clock ownership and continuity rules. Before coding, resolve capped-backlog issue times, same-time normal/speed dispatch, initial/landed movement timing, and buffer capacity. Derive the capacity from minimum intervals, retained backlog, early successors, and membership changes. Increasing the current four-slot buffer without a bound argument is insufficient. Never merge targets or overwrite intermediate waypoints on overflow; prefer interleaved consumption at dispatch times to avoid accumulating a whole frame of pending requests.

### Implementation decisions (2026-10-21)

- `GameController.Update` supplies the frame delta to `ConveyorFlow.Tick`. Before each existing tick dispatch, advance slice/arrow visuals to `Clamp(frameDelta - selectedClockLag, visualCursor, frameDelta)`. Normal retains its existing tie priority. After dispatching due ticks, advance only the frame remainder. For a `0.8s` frame capped to `0.5s`, the first `0.05s` tick issues at `0.35s`; discarded time creates no moves.
- `RoadMotion` stores only visual pose segments and consumes them in order; it never writes gameplay indices or raises admission events. Slice retains OutQuad rotation and arrow retains linear rotation. Component road `Update` methods are removed. Already permitted motion continues through non-ticking gating as before; no idle credit is stored after its final endpoint.
- Valid road intervals are preserved down to the supported scheduler minimum `0.00512s`; the previous `0.01s` road clamp would create indefinite backlog. Jump duration clamps are unchanged. Initial/landed road movement starts at the actual transform, and no visual advancement precedes its move issue time.
- A frame greater than `0.2s` uses the same ordered timeline processing without a separate teleport branch. Persistent early successors can also overload visual time, so after enqueueing, older remaining segment time is bounded to `0.2s` by proportional duration shortening. Scale active elapsed by the same factor to preserve the current pose. Exclude the newest segment from this acceleration so its nominal cadence is retained, including slow arrows. This is an intentional visual timing change under overload; no waypoints are removed.
- Storage has 288 slots. Once a segment has a successor, its remaining completion horizon is at most `0.2s` and can only shorten. At most two logical clocks dispatch at the supported minimum interval; allowing `0.5s` retained backlog plus `0.2s` older visual debt gives `2 * ceil(0.7 / 0.00512) + 4 = 278` slots, below capacity. Unsupported direct callers exhausting storage fail explicitly instead of overwriting targets. New jump/section/reset/disable boundaries clear the route.
- Follow-up reproduced in Unity: a follower returning from automatic catch-up to normal membership had logical spacing `1` but persistent visual spacing `1.559999` on a straight fixture. On requested cadence changes, proportionally shorten all remaining route durations and active elapsed so the newest permitted target is reached at the next tick of the new cadence. This preserves the handoff pose and every waypoint while eliminating old-clock phase debt; unchanged-cadence successors retain nominal timing except the documented overload rule. Reset also clears cadence history.
- Verification lives in `Rules/SmoothSliceVerification/`: it builds a fresh runtime source inventory to avoid stale generated project entries, runs actual classes in a separate Unity project, and compares against a frozen pre-change `BaselineConveyorFlow`. Main project assets/settings/packages are untouched. This folder is locally ignored by the repository's existing `.gitignore`.

## Task 1: Capture baseline and reproduce failures

**Files:** Read movement source/callers; extend focused Unity verification under `Rules/` as needed.

- [x] Read `GameController.Update`, interval calculation, speed switches, failure slowdown, conveyor group selection, transfer landing/box admission, and slice/arrow initialization and cleanup.
- [ ] Capture baseline tick order/counts, logical indices, launch count, box admission events, and end-state transitions for deterministic fixtures. Partial: indices, residuals and admission/end-check callback traces are compared; full scene admission/end-state events remain pending.
- [x] Reproduce early ticks, late ticks, multiple ticks in one frame, and blocked-to-resumed movement using actual runtime classes.
- [x] Cover normal/speed membership changes within a frame; do not assume one move request per slice per rendered frame.
- [ ] Record both logical clocks and verify that total visual time advanced per existing active slice equals the shared frame time once, regardless of tick count or membership changes. Newly initialized/landed slices consume only their eligible portion of that timeline.
- [x] Inspect the fixture time driver. `DOTween.ManualUpdate` alone does not advance custom road interpolation. Avoid advancing both manually and automatically for the same elapsed time.
- [x] Use straight, right-angle, closed-loop, and disconnected-section fixtures. Define geometric expectations independently of the implementation formula.
- [x] Complete the timing decision gate above before editing movement code.

## Task 2: Coordinate conveyor and visual time

**Files:** `Assets/00Game/01Scripts/02Gameplay/Flow/ConveyorFlow.cs`; `Flow/GameController.cs` only if required; focused verification.

- [x] Keep scheduler semantics, including chronological normal/speed ordering and recomputation of failure intervals after ticks.
- [x] Supply timing/order information needed to distinguish continuous successors from idle gaps and consume catch-up requests in order.
- [x] Implement the selected shared phase: advance to logical tick issue times, dispatch in the existing order, then consume the frame remainder. Remove independent road advancement from `Slice.Update` and `PathArrow.Update`. Do not add either tick interval to visual elapsed time.
- [x] Process lag spikes greater than `0.2s` in the same frame through ordered segment consumption. Do not snap to logical indices, skip waypoint geometry, or alter logical traces; verify box admission and jump entry after preceding visual advancement.
- [x] Establish and verify reusable pending-storage capacity and overflow behavior before replacing the current four-slot queue. Never overwrite or merge intermediate targets when full.
- [x] Preserve existing behavior when ticking becomes disallowed. Check shop/booster gating, `Time.timeScale == 0`, and failure slowdown; do not introduce a new pause policy silently.
- [x] Compare logical traces against Task 1, including speed toggles and both tick types due in the same frame.

## Task 3: Correct Slice continuity and cleanup

**File:** `Assets/00Game/01Scripts/02Gameplay/Elements/Slice.cs`; focused verification.

- [x] Preserve `MoveNextPath(float interval)` and its logical update responsibilities. Add internal timing plumbing only where needed.
- [x] Implement the selected timing model, preserving unfinished route segments and ordered multi-tick catch-up.
- [x] Separate road-state cleanup from tween cleanup where necessary so starting a contiguous segment does not erase valid pending visual movement.
- [x] Retain positive-duration and direction-magnitude guards; validate required setup without adding null-sensitive accesses.
- [x] Preserve non-circular section transition position/rotation and reset old visual continuity.
- [x] Reset timing and pending visual data through `StopMotion`, initialization, pool reset, disable, jump entry, undo dismissal, and section discontinuities as applicable.
- [x] Verify road movement starts from the landed pose after `JumpToTransfer`; stale road updates cannot overwrite `JumpToBox` or dismissal.
- [x] Preserve valid continuity across render frames, including a temporarily empty queue. Use issue times rather than a grace timeout to classify elapsed time.
- [x] Verify blocked resume has zero idle-time debt, already permitted segments finish normally, and genuine final stops accrue no movement credit. Explicit resets and jump entry still clear obsolete visual data.

## Task 4: Apply the timing contract to PathArrow

**File:** `Assets/00Game/01Scripts/02Gameplay/Board/PathArrow.cs`; focused verification.

- [x] Preserve arrow cadence: every other launch tick, using duration `interval * 2f`.
- [x] Handle early/late successors and multi-tick frames without `CompleteMove()` snapping an unfinished segment or discarding eligible overshoot.
- [x] Consume the same shared timeline as slices. Preserve continuous overshoot across render boundaries and remove the current `duration + 0.05s` grace period; stopped arrow time cannot accelerate a later segment.
- [x] Keep exact endpoint assignment safe and idempotent. Removing the snap on tick arrival must not remove correct final completion.
- [x] Preserve initialization, height offset, wrapping, section transitions, and existing rotation easing.
- [ ] Clear visual continuity on disable and reinitialization; verify reuse and disable/re-enable.

## Task 5: Verify and report

### Automated checks

- [x] Run `dotnet run --project Rules/Verification/CoreRulesVerification.csproj` for linked rules and `VerifyNoFindPolicy()`.
- [ ] Run `dotnet build Rules/ProgressData/FullProjectVerification.csproj --no-restore` to compile runtime classes. Report environmental reference failures separately from code failures.
- [x] Add and run focused Unity checks against actual changed runtime classes. Reuse existing fixture patterns; inspect launcher scripts before use because they may prepare a separate project or copy settings. Keep the user's main project settings untouched.
- [x] Compare equal simulated elapsed time at 30/60/90/120 FPS, including minimum spacing and speed intervals shorter than a frame. Declare numeric tolerances explicitly.
- [x] Cover delta `0`, ordinary deltas, `0.05s`, `0.2s`, `0.201s`, and a spike beyond the existing accumulator cap. Preserve baseline cap behavior; do not claim discarded time is retained. Assert ordered same-frame visual catch-up and no extra logical moves.
- [x] Cover a `0.05s` segment followed by `0.04s` of actual blocked waiting: the next move receives no carried blocked time. Separately test continuous movement split across render boundaries; eligible time must not be erased merely because no successor was queued at a component update boundary.
- [ ] Cover simultaneous normal/speed ticks, membership switches, and new landings: assert no double consumption of Master DeltaTime and no movement credited before its issue time. Clock/membership and isolated landing checks pass; a same-frame linked-landing integration fixture remains pending.
- [x] Reproduce automatic fast-to-normal group joining on a straight route. After the handoff cycle, assert visual spacing equals logical spacing within `0.002` world units and remains aligned; also cover normal-to-fast changes and long-duration slowdown arrows.
- [x] Exceed four pending requests across a right-angle route at the minimum interval; assert every waypoint is processed without target merging, overflow loss, or corner shortcuts. Include box admission and section transitions during catch-up.
- [ ] Cover early successors and several successors in one frame: assert waypoint order, no corner shortcut, and no handoff positional discontinuity beyond tolerance.
- [ ] Cover blocked/full roads, closed groups, unblocking, catch-up membership changes, speed toggles, failure slowdown, box arrival, win/lose, restart/reload, jump interruption, pooling, and disable/destruction.
- [ ] Only assert scheduler residual is below an interval after due ticks are drained under stable intervals, allowing float tolerance. Do not impose this bound on all timing state at all times.

### In-Editor visual and performance checks

- [ ] Observe straight paths, sharp corners, wrapping, and section transitions under normal/speed movement and spikes. A long frame cannot display every intermediate waypoint; verify route processing separately from rendered samples.
- [ ] Compare displacement over time on equal-length segments. Do not claim constant world-space velocity over unequal-length segments without distance-based timing.
- [ ] Use Unity Profiler after warm-up to check introduced road-motion GC allocations. Capture evidence before claiming performance improvements.
- [ ] Evaluate residual angular jerk after timing passes. Make easing changes a separate follow-up only when observations justify them, documenting the visual difference.

### Acceptance and completion report

- [x] Logical movement/event traces preserve baseline behavior for identical inputs.
- [x] Continuous movement preserves route order without handoff snaps, repeated time consumption, or idle-time debt.
- [ ] Jumps, stops, section transitions, pooled objects, disabled objects, and restarts invalidate old visual state correctly.
- [x] Review changes for accidental scene/prefab/`.meta`, dependency, settings, and unrelated edits.
- [x] Report changed files, selected timing model, gameplay impact, actual checks/results, observations, and remaining risks.
- [ ] If Unity validation cannot run, mark movement acceptance incomplete and list specific pending checks. Backend success alone is not movement validation.

## Implementation and verification report

Changed runtime files: new `Board/RoadMotion.cs` and its `.meta`; `Elements/Slice.cs`; `Board/PathArrow.cs`; `Flow/ConveyorFlow.cs`; the road visual update branch in `Flow/GameController.cs`. Existing unrelated working changes were preserved. This task did not edit main-project scenes, prefabs, materials, packages, assembly definitions, or ProjectSettings.

`RoadMotion` replaces the four-slot target-merging queue and arrow's unfinished-segment replacement/grace timeout. The conveyor advances one visual timeline before each chronological tick and once for the frame remainder. Catch-up traverses each queued segment, and cadence changes synchronize arrival with the new group's next tick while preserving the current pose. Gameplay indices, group selection, launch cadence, admission callback ordering, and scheduler cap are unchanged in the deterministic comparisons.

Actual commands/results:

- `dotnet run --project Rules/Verification/CoreRulesVerification.csproj`: `No-Find policy verified: 0 violations across Assets/00Game/01Scripts.` and `Core game verification passed.`
- `powershell -NoProfile -ExecutionPolicy Bypass -File Rules/SmoothSliceVerification/verify-unity.ps1`: `PASS: 65032 checks against actual runtime classes in Unity.` Runs in Play Mode in an isolated project with real transforms, Slice, PathArrow, ConveyorFlow, CoreGameplayEngine, and DOTween. Source DLLs are built from a fresh inventory; the final build reported `2 Warning(s), 0 Error(s)` (existing MSB3277 framework-reference conflicts). The stale original generated verification project is not used to claim a successful build.
- Original generated `Assembly-CSharp.csproj --no-restore` baseline build failed without compiler diagnostics; the separate fresh-inventory build above is the actual successful runtime compile.
- Before the cadence fix, the added group-join regression failed: `logic gap 1, visual gap 1.559999`. After the fix, it passes at tolerance `0.002` world units after the handoff cycle and stays aligned.
- Tests cover eight queued waypoints through right angles, handoff continuity, minimum `0.00512s` intervals at 30/60/90/120 FPS, spikes through `0.8s` respecting the `0.5s` scheduler cap, actual popup-box blocking/resume, gating, pool/disable/reinitialization/section cleanup, slow-to-fast arrows, and transfer/box jump interruption and callbacks. Two thousand deterministic frames compare logical indices, both scheduler residuals, and admission/end-check callback traces against the frozen original scheduler.
- Guideline reviews: `code`, `safety`, and `architecture` passed, including the follow-up cadence synchronization change. Task-scoped `git diff --check` passed.

Local test sources, runner, build logs, pre-fix reproduction, and latest results are under `Rules/SmoothSliceVerification/` (ignored by the existing repository policy). The Unity dependency fixture reports pre-existing Coffee.UIParticle `OnParticleUpdateJobScheduled` import messages; this is not a clean-main-Editor-console claim.

Remaining acceptance: observe real levels with normal/manual-speed catch-up through corners, wrapping and disconnected sections; exercise actual linked launches, box reservation, full-road and win/lose UI transitions; capture warmed-up Unity Profiler `GC.Alloc` and frame timing. The headless fixture validates movement and callbacks, not complete scene content or the subjective rotation/animation feel. No performance improvement or zero-allocation claim is made.
