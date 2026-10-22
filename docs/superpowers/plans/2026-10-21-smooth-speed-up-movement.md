# Smooth Speed Up Movement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate micro-freezing, visual stutter, rotation wobble, and velocity snapping when Speed Up is active or toggled, matching the original APK 1.1.4 game feeling as verified through Ghidra decompilation.

**Architecture:** 
1. Maintain clock continuity in `ConveyorFlow.SetSpeedUp` by preserving accumulated delta time across toggles (matching native `GameController.SetSpeedUp` RVA `0x15CAE64`).
2. Remove artificial jump time-scaling and hierarchy scans from `GameController.SetSpeedUp`, fixing jump duration at native constant 0.4s / power 2.2f (`Slice.JumpToBox` RVA `0x17805BC`, `Slice.JumpToTransfer` RVA `0x1780DAC`).
3. Replace 31 Hz `Ease.OutQuad` rotational pulsing in `RoadMotion` with smooth linear interpolation, and eliminate mid-segment velocity snaps during speed changes.

**Tech Stack:** Unity 2022.3 LTS C#, DOTween, UniTask, .NET 8 / C# 9 verification harness.

**Spec:** Ghidra analysis of `libil2cpp.so` (Loop Master APK 1.1.4):
- `GameController.SetSpeedUp` (RVA `0x15CAE64`): Native sets flag `_isSpeedUp = speedUp;` and updates audio/UI. It never resets tick timers or alters jump tweens.
- `GameController.UpdateTick` (RVA `0x15CCE90`): Native runs dual continuous clocks `_normalTickTime` (0.05s) and `_speedTickTime` (0.032s), accumulating `deltaTime` without reset.
- `Slice.MoveNextPath` (RVA `0x177FEA4`): Moves and rotates smoothly via DOTween linear progression.
- `Slice.JumpToBox` (RVA `0x17805BC`) & `JumpToTransfer` (RVA `0x1780DAC`): Fixed `DOJump(..., power: 2.2f, numJumps: 1, duration: 0.4f)`.

---

## Global Constraints

- **STRICT NO `Find` POLICY:** Never use `transform.Find` or `GameObject.Find` in C# scripts. Enforced by `../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj`.
- **Target Unity 2022.3 LTS:** Maintain existing C# language level, packages, and conventions.
- **Authoritative State:** Conveyor path indexing and logical state must remain separate from visual animation.
- **DOTween Safety:** Attach `.SetLink(gameObject)` and clean up tweens safely.
- **Memory & Allocations:** Zero managed heap allocations in Update/Tick hot paths.

---

## Review Focus

1. **Rapid Speed Up Toggling:** Rapidly toggling speed up on and off must never cause slices to freeze, twitch backward, or accumulate timing drift.
2. **Jump In-Flight Continuity:** Slices mid-jump to box or transfer when Speed Up is toggled must complete their parabolic flight smoothly at constant 0.4s without warping or sudden speed bursts.
3. **Curved Road Traversal:** When running at high speed (0.032s / 31.25 steps/sec), rotation around bends must be smooth and continuous, without 31 Hz angular vibration.
4. **Failure Slowdown Compatibility:** When `BeginFailureSlowdown` or `EndFailureSlowdown` is active, slow-motion intervals must still override normal and speed clocks properly.
5. **No-Find Rule Compliance:** Running `dotnet run --project ../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj` must report 0 violations.

---

### Task 1: Clock Continuity in `ConveyorFlow.SetSpeedUp`

**Files:**
- Modify: `Assets/00Game/01Scripts/02Gameplay/Flow/ConveyorFlow.cs:96-103`
- Test: `../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj`

**Interfaces:**
- Consumes: `ConveyorFlow.IsSpeedUpActive`, `_normalTickTime`, `_speedTickTime`.
- Produces: Seamless speed mode switching without dead zones or accumulated phase loss.

- [ ] **Step 1: Inspect current `ConveyorFlow.SetSpeedUp` implementation**
Confirm lines 96-102 currently reset `_normalTickTime = 0f; _speedTickTime = 0f;`:
```csharp
public void SetSpeedUp(bool speedUp)
{
    if (IsSpeedUpActive == speedUp) return;
    IsSpeedUpActive = speedUp;
    _normalTickTime = 0f;
    _speedTickTime = 0f;
}
```

- [ ] **Step 2: Modify `ConveyorFlow.SetSpeedUp` to preserve clock accumulation**
In `Assets/00Game/01Scripts/02Gameplay/Flow/ConveyorFlow.cs`, remove the clock resets so the elapsed fractions persist across toggles (matching native RVA `0x15CAE64`):
```csharp
public void SetSpeedUp(bool speedUp)
{
    if (IsSpeedUpActive == speedUp) return;
    IsSpeedUpActive = speedUp;
}
```

- [ ] **Step 3: Verify clock continuity under toggle**
Run:
```powershell
Set-Location "c:\Users\Laptop\Downloads\LoopMaster"; dotnet run --project ../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj
```
Expected: PASS with 0 violations.

---

### Task 2: Native Constant Jump Dynamics & Elimination of Hierarchy Scans

**Files:**
- Modify: `Assets/00Game/01Scripts/02Gameplay/Flow/GameController.cs:483-504`
- Modify: `Assets/00Game/01Scripts/02Gameplay/Elements/Slice.cs:20-22, 63-100, 156-193`
- Modify: `Assets/00Game/01Scripts/02Gameplay/Rules/GameplayFeedbackRules.cs:18-35`
- Test: `../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj`

**Interfaces:**
- Consumes: `Slice.JumpToBox`, `Slice.JumpToTransfer`, `GameController.SetSpeedUp`.
- Produces: Constant jump duration (0.4s) and jump power (2.2f) matching Ghidra decompilation (`libil2cpp.so`), eliminating runtime `GetComponentsInChildren<Slice>` hierarchy traversals.

- [ ] **Step 1: Remove `GetComponentsInChildren<Slice>` from `GameController.SetSpeedUp`**
In `Assets/00Game/01Scripts/02Gameplay/Flow/GameController.cs`, remove the block that searches `GameRoot.GetComponentsInChildren<Slice>` and mutates `timeScale`:
```csharp
public void SetSpeedUp(bool speedUp)
{
    bool next = speedUp && Data != null && EndState == GameEndState.Playing && !IsLevelIntroPlaying;
    bool previous = IsSpeedUpActive;
    Conveyor.SetSpeedUp(next);
    if (GameplayFeedbackRules.ShouldPlaySpeedTransition(CurrentLevelNumber, previous, next))
        PlayBoosterAudio(_gameLoader == null ? null : (next ? _gameLoader.AccClip : _gameLoader.DecClip));
    if (!previous && next)
        PlayBoosterAudio(_gameLoader == null ? null : _gameLoader.AccShortClip);
}
```

- [ ] **Step 2: Update `Slice.cs` jump duration and power to match native constants**
In `Assets/00Game/01Scripts/02Gameplay/Elements/Slice.cs`:
1. Update `JumpArcHeight` constant from `1.5f` to `2.2f` (native `power: 2.2`).
2. Update default jump duration to `0.4f` (native `duration: 0.4`).
3. Clean up `_scaleJumpWithConveyor` and simplify `SetJumpTimeScale(float timeScale)` to be a no-op or maintain `timeScale = 1f` for jump tweens so jumps remain crisp and parabolic.
4. Ensure `JumpToBox` and `JumpToTransfer` both use `duration = 0.4f` and `jumpHeight = 2.2f`.

- [ ] **Step 3: Update `GameplayFeedbackRules.cs` transfer time scale**
In `Assets/00Game/01Scripts/02Gameplay/Rules/GameplayFeedbackRules.cs`, ensure `GetTransferTimeScale` returns `1f` (or constant native scale) to keep jump dynamics consistent and decouple jump flight from conveyor speed.

- [ ] **Step 4: Verify compilation and tests**
Run:
```powershell
Set-Location "c:\Users\Laptop\Downloads\LoopMaster"; dotnet run --project ../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj
```
Expected: PASS with 0 violations.

---

### Task 3: Smooth Road Motion & Elimination of Rotation Wobble

**Files:**
- Modify: `Assets/00Game/01Scripts/02Gameplay/Board/RoadMotion.cs:12-30, 48-61, 88-96, 116-133`
- Modify: `Assets/00Game/01Scripts/02Gameplay/Elements/Slice.cs:22`
- Test: `../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj`

**Interfaces:**
- Consumes: `RoadMotion.Enqueue`, `RoadMotion.Advance`, `RoadMotion.ApplyProgress`.
- Produces: Constant angular velocity around curves without 31 Hz pulsation, and non-snapping segment transitions during speed changes.

- [ ] **Step 1: Remove 31 Hz `Ease.OutQuad` rotational twitch in `RoadMotion.ApplyProgress`**
In `Assets/00Game/01Scripts/02Gameplay/Board/RoadMotion.cs`:
Change `ApplyProgress`:
```csharp
private void ApplyProgress(Segment current)
{
    float progress = Mathf.Clamp01(_elapsed / current.Duration);
    Position = Vector3.Lerp(_startPosition, current.Position, progress);
    Rotation = Quaternion.Slerp(_startRotation, current.Rotation, progress);
}
```
In `Slice.cs`, instantiate `_roadMotion = new RoadMotion(false)` (or remove `_easeRotation` field entirely).
This ensures angular rotation on curves is linear and continuous across waypoints.

- [ ] **Step 2: Prevent abrupt velocity snapping in `RoadMotion.SynchronizeArrival`**
In `RoadMotion.cs`:
When `Enqueue` receives a new duration (e.g. 0.05s -> 0.032s), the segment *currently in flight* must be allowed to complete smoothly at its existing pace without scaling `_elapsed` and warping current velocity. Only unstarted/subsequent segments or excess backlog should adapt:
```csharp
private void SynchronizeArrival(float duration)
{
    if (_count <= 1) return;
    float remaining = _segments[_head].Duration - _elapsed;
    for (int i = 1; i < _count; i++)
        remaining += _segments[(_head + i) % Capacity].Duration;
    if (remaining <= duration) return;

    // Scale only unstarted queued segments so in-flight pose and velocity do not jump
    float unstartedRemaining = 0f;
    for (int i = 1; i < _count; i++)
        unstartedRemaining += _segments[(_head + i) % Capacity].Duration;

    if (unstartedRemaining > 0.0001f)
    {
        float targetUnstarted = Mathf.Max(0.001f, duration - (_segments[_head].Duration - _elapsed));
        float scale = targetUnstarted / unstartedRemaining;
        for (int i = 1; i < _count; i++)
        {
            int index = (_head + i) % Capacity;
            _segments[index].Duration *= scale;
        }
    }
}
```

- [ ] **Step 3: Verify CoreRules and No-Find compliance**
Run:
```powershell
Set-Location "c:\Users\Laptop\Downloads\LoopMaster"; dotnet run --project ../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj
```
Expected: PASS with 0 violations.

---

### Task 4: End-to-End Smoothness & Visual Verification

**Files:**
- Verify: `../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj`
- Check: Unity Editor Play Mode logs / script compilation status

- [ ] **Step 1: Run comprehensive backend verification**
Run:
```powershell
Set-Location "c:\Users\Laptop\Downloads\LoopMaster"; dotnet run --project ../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj
```
Confirm:
1. `No-Find policy verified: 0 violations across Assets/00Game/01Scripts.`
2. All speed-up queries and board simulation checks pass.

- [ ] **Step 2: Check git diff for cleanliness**
Run:
```powershell
Set-Location "c:\Users\Laptop\Downloads\LoopMaster"; git status --short
```
Ensure only targeted files are modified, no unintended scene, asset, or `.meta` changes exist.

