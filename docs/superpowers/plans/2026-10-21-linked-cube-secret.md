# Linked Cube Secret Mechanic Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the "Linked Cube Secret" mechanic allowing one or both cubes in a linked pair to be secret, concealing their true colors behind secret visuals (including on the connecting cable) until revealed at the top of their box, and gating paired launch until both members are at the top and revealed.

**Architecture:** 
1. **Validation & State Gate (`LinkedCubePair.cs`, `LinkCubeLaunch.cs`):** Update pair validation to support `cube.IsSecret > 0` and ensure launch rules cleanly reject unrevealed secret cubes with `LinkedCubeLaunchFailure.BlockedPartner`.
2. **Zero-Leak Dynamic Cable Styling (`LinkedCubeCable.cs`):** Enhance cable tinting to inspect `member.IsSecret`, applying the secret material tint while a cube is concealed and updating seamlessly to the revealed color once uncovered.
3. **Reveal & Lifecycle Orchestration (`Box.cs`, `Cube.cs`):** Coordinate top-of-box secret reveal via `CheckHiddenSecret()` with cable visual updates and initial level layout handling.
4. **Snapshot & Re-Burial Undo Integrity (`LinkedCubeUndoRecord.cs`, `BoxSnapshot`):** Ensure undoing paired launches preserves revealed state on top, while undoing moves that re-bury a secret linked cube cleanly restores mystery state on both cube and cable.

**Tech Stack:** Unity 2022.3 LTS, C# (.NET Standard 2.1), UniTask, DOTween, `CoreRulesVerification.csproj`.

**Spec:** `docs/superpowers/specs/2026-10-21-linked-cube-secret-design.md`

## Global Constraints

- **STRICTLY NO `Find`:** Never use `transform.Find(...)` or `GameObject.Find(...)`. Use `HierarchyReferenceCache`, `GetComponent`, or serialized fields.
- **Readable Control Flow:** Early returns, guard clauses, no nested ternaries, and no complex closures capturing variables in Update or hot loops.
- **Method Naming:** Avoid unnecessary `Try` prefixes; use descriptive action names (`UpdateTint`, `CanBegin`, `RevealSecretAsync`).
- **DOTween Lifecycles:** Always attach `.SetLink(gameObject)` and cleanly kill active sequences before playing new ones or during `OnDestroy` / `Cancel`.
- **Async Operations:** Use `UniTask` (`Cysharp.Threading.Tasks`) with proper `CancellationToken` propagation; avoid unhandled `async void`.
- **Verification Rule:** Must pass `dotnet run --project Rules/Verification/CoreRulesVerification.csproj`.

## Review Focus

1. **Zero Information Leak via Cable:** While a cube is `IsSecret`, its cable half must never reveal the underlying `Type` color, neither in shader properties nor in `MaterialPropertyBlock`.
2. **Asymmetric Pair Launch Gate:** If Box A's cube is revealed and at top, but Box B's cube is still buried or still secret, tapping Box A must trigger blocked shake (`BlockedPartner`) and not launch.
3. **Double Secret Pair Support:** Both cubes in a pair can be secret; each reveals independently when reaching the top of its respective box.
4. **Re-Burial Undo Integrity:** If an undo action pushes another slice back on top of a revealed linked cube, that cube and its cable half must return to the mystery secret state.
5. **Initial Top Cube Reveal:** If a level starts with a secret linked cube already at the top of a box, it must properly trigger its reveal flow without blocking or hanging.

---

### Task 1: Level Validation & Launch Gate Support

**Files:**
- Modify: `Assets/00Game/01Scripts/02Gameplay/State/LinkedCubePair.cs`
- Modify: `Assets/00Game/01Scripts/02Gameplay/State/LinkCubeLaunch.cs`

**Steps:**
- [ ] Inspect `LinkedCubePair.Validate()` to confirm `cube.IsSecret > 0` is valid for linked pair members. Add explicit doc comments and rule tests ensuring `IsSecret` is permitted while `GroupKeyId != 0`, `NumLock != 0`, and `IsFixed != 0` remain rejected.
- [ ] In `LinkCubeLaunch.CanBegin()`, verify the check `!IsFullOrdinaryMember(pair.FirstCube) || !IsFullOrdinaryMember(pair.SecondCube)`. Ensure that when either cube is top-of-box but still `IsSecret`, `failure` is reported as `LinkedCubeLaunchFailure.BlockedPartner` so tapping provides the standard shake feedback.
- [ ] Run `dotnet run --project Rules/Verification/CoreRulesVerification.csproj` to confirm compliance.

---

### Task 2: Zero-Information-Leak Cable Tinting

**Files:**
- Modify: `Assets/00Game/01Scripts/02Gameplay/Elements/LinkedCubeCable.cs`
- Modify: `Assets/00Game/01Scripts/02Gameplay/Level/Data/GameAssetsConfig.cs` (if helper needed for secret cable color)

**Steps:**
- [ ] Define secret cable tint color resolution:
  - If `member.IsSecret`, resolve the secret color from `GameAssetsConfig.GetSecretMat()` (or a dedicated mystery color constant matching secret block visual style, e.g. `#4A4A5A`).
  - If `!member.IsSecret`, resolve `GameAssetsConfig.GetCubeColor(member.Type)`.
- [ ] In `LinkedCubeCable.UpdateTint()`:
  - Track both the color and secret status of both members:
    ```csharp
    bool firstSecret = _pair.FirstCube != null && _pair.FirstCube.IsSecret;
    bool secondSecret = _pair.SecondCube != null && _pair.SecondCube.IsSecret;
    Color firstColor = firstSecret ? GetSecretCableColor() : GameAssetsConfig.GetCubeColor(_pair.FirstCube.Type);
    Color secondColor = secondSecret ? GetSecretCableColor() : GameAssetsConfig.GetCubeColor(_pair.SecondCube.Type);
    ```
  - Cache the previous colors and secret flags to avoid unnecessary `MaterialPropertyBlock` allocations.
  - Apply `ApplyTint(_firstHalfRenderer, firstColor)` and `ApplyTint(_secondHalfRenderer, secondColor)`.
- [ ] Ensure that when `_pair.FirstCube.IsSecret` flips from `true` to `false`, `UpdateTint()` immediately updates the cable half to the true color.
- [ ] Run verification check.

---

### Task 3: Top-of-Box Reveal Flow & Initial Layout Sync

**Files:**
- Modify: `Assets/00Game/01Scripts/02Gameplay/Elements/Box.cs`
- Modify: `Assets/00Game/01Scripts/02Gameplay/Elements/Cube.cs`

**Steps:**
- [ ] Review `Box.CheckHiddenSecret()`:
  - When the top cube in `_info.PeekTopCube()` has `IsSecret == true`, it reveals via `cubeInfo.CubeSc.HiddenSecret().Forget()`.
  - Verify that when the reveal animation completes and `_info.IsSecret = false`, `Cube.RefreshMaterial()` and `RefreshSquareSprite()` are applied cleanly.
- [ ] In `Box.Init()`, ensure `CheckHiddenSecret()` is evaluated if the initial top cube is secret, allowing it to reveal smoothly on board entrance.
- [ ] Verify that `LinkedCubeCable.LateUpdate()` picks up the newly updated `IsSecret` status in the same or next frame without visual stutter.
- [ ] Run verification check.

---

### Task 4: Undo Integrity (Linked Undo & Re-Burial Undo)

**Files:**
- Modify: `Assets/00Game/01Scripts/02Gameplay/State/LinkedCubeUndoRecord.cs`
- Modify: `Assets/00Game/01Scripts/02Gameplay/Elements/Box.cs`

**Steps:**
- [ ] Inspect `LinkedCubeUndoRecord.BoxSnapshot`:
  - Verify that `BoxSnapshot` copies `a.IsSecret != b.IsSecret` for all cubes in `Matches()` and `Clone()`.
  - In `Restore()`, verify `box.IsLastUnlockSecret` and `box.CubeInfos` are restored.
- [ ] In `Box.RefreshAfterUndo()`, ensure that when an undo replaces or restores cubes:
  - Any restored cube with `info.IsSecret == true` re-applies `GameAssetsConfig.GetSecretMat()` and `GameAssetsConfig.GetCubeSecretSprite()`.
  - If a secret linked cube was re-buried under an incoming slice, its visual state returns to secret.
- [ ] Verify that `LinkedCubeCable.LateUpdate()` automatically reverts the corresponding cable half to the secret tint upon undo re-burial.
- [ ] Run verification check.

---

### Task 5: Core Rules Verification & Integration Tests

**Files:**
- Test / Verify: `Rules/Verification/CoreRulesVerification.csproj`
- Test: EditMode or simulation tests in `Assets/00Game/01Scripts/Editor` or `Rules/Verification`

**Steps:**
- [ ] Create automated verification test cases:
  - Test Case A: Linked pair with 1 Normal + 1 Secret cube. Secret cube starts buried. Verify launch is blocked until buried cube reaches top and reveals.
  - Test Case B: Linked pair with 2 Secret cubes. Verify both must reach top and reveal before launch is permitted.
  - Test Case C: Verify cable half tints: secret tint while secret, true color after reveal.
  - Test Case D: Undo test: linked pair launch undo returns revealed cubes to top; burial undo returns cube to secret state.
- [ ] Run `dotnet run --project Rules/Verification/CoreRulesVerification.csproj`.
- [ ] Ensure 0 compilation warnings and 0 rule violations.
