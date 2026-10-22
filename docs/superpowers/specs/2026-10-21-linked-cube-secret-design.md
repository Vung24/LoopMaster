# Linked Cube Secret Mechanic Design Specification

**Date:** 2026-10-21  
**Status:** Approved  
**Author:** AI Pair Programming (LoopMaster Team)

---

## 1. Intent & Overview

The **Linked Cube Secret** mechanic combines the existing **Linked Cube** system (where two cubes across different boxes are tethered by a cable and must launch together) with the **Secret Cube** system (where cubes are hidden under a mystery material/sprite until they reach the top of their box).

This introduces layered puzzle complexity:
- Players can see that two cubes are linked by the connecting cable.
- However, one or both of the cubes may have their color concealed behind a mystery ("?") state.
- Players cannot launch the pair until both cubes are uncovered and ready at the tops of their respective boxes.

---

## 2. Goals, Non-Goals, and Anti-Goals

### Goals
- **Pair Asymmetry & Symmetry:** Support linked pairs where:
  - 1 member is Secret and 1 member is Normal (Ordinary color).
  - Both members are Secret.
- **Top-of-Box Reveal:** When a secret linked cube reaches the top of its box (or starts at the top), it automatically reveals its true color via the standard `CheckHiddenSecret()` flow, accompanied by the secret particle animation.
- **Zero-Information Leak Cable:** While a cube remains secret, its half of the connecting cable MUST be styled using the Secret material / secret tint, preventing players from guessing or inspecting the secret color in advance.
- **Dynamic Cable Color Update:** Once a secret cube is revealed at the top of its box, its half of the cable updates seamlessly to match the newly revealed true color.
- **Strict Launch Gate:** The pair can ONLY be launched when BOTH cubes are at the top of their boxes and fully revealed (`!FirstCube.IsSecret && !SecondCube.IsSecret`).
- **Standard Tapping Feedback:** Tapping either box while one or both members are still secret or buried will trigger the blocked cable pulse (`BlockedPartner`) and prevent launch.
- **Full Undo Integrity:**
  - Undoing a linked launch returns both revealed cubes to their box tops.
  - Undoing any move that re-buries a revealed linked cube under another slice will cleanly revert that cube (and its cable half) to the secret state.

### Non-Goals
- Secret slices moving along the road: cubes MUST be revealed before launching; secret slices never travel on the road.
- Changing normal secret cube mechanics or normal linked cube rules.
- Allowing more than two cubes per linked group.

### Anti-Goals
- **Information Leaks:** Never expose the secret cube's true color in the cable tint, shader properties, or accessibility labels before it is revealed.
- **Silent Failures:** Never allow tapping to silently do nothing; tapping an unready linked box must always provide responsive visual feedback (cable blocked pulse / shake).

---

## 3. Gameplay Invariants & Mechanics

### 3.1 Setup & Configuration
- Configured in level data via `CubeData.LinkedPairId > 0` and `CubeData.IsSecret == 1`.
- A linked pair consists of exactly two cubes in separate boxes with the same `LinkedPairId`.
- Member colors can be identical or different (standard linked cube rule).
- Supported combinations:
  1. `Cube A (Normal, Color X)` <---> `Cube B (Secret, Color Y)`
  2. `Cube A (Secret, Color X)` <---> `Cube B (Secret, Color Y)`

### 3.2 Reveal Lifecycle
1. While buried under other cubes:
   - Cube displays `GameAssetsConfig.GetSecretMat()` and `GameAssetsConfig.GetCubeSecretSprite()`.
   - The corresponding cable half displays the secret tint / secret material.
2. When reaching the top of the box:
   - `Box.CheckHiddenSecret()` triggers `cube.HiddenSecret()`.
   - Secret reveal particle plays (`Cube.PlaySecretParticle`).
   - `CubeInfo.IsSecret` becomes `false`.
   - Cube material and sprite switch to `GameAssetsConfig.GetMatByType(Type)` and `GetCubeSpriteByType(Type)`.
   - In `LinkedCubeCable.LateUpdate()`, `UpdateTint()` detects the reveal and smoothly updates the cable half to the true color.

### 3.3 Launch Validation
In `LinkCubeLaunch.CanBegin(data, tappedBox, out failure)`:
- Both `pair.FirstBox.PeekTopCube()` and `pair.SecondBox.PeekTopCube()` must match the pair's members.
- Both members must satisfy `IsFullOrdinaryMember`:
  - `cube.Size == GameData.SliceCountPerGroup`
  - `cube.Type >= 1 && cube.Type <= 14`
  - `!cube.IsSecret` (both cubes MUST be revealed)
  - `cube.NumLock == 0 && cube.GroupKeyId == 0`
- If either cube is still secret, launch is refused and failure is reported as `LinkedCubeLaunchFailure.BlockedPartner`.

### 3.4 Undo Behavior
- **Linked Move Undo:** When undoing the launch of a linked cube pair, both cubes were already revealed prior to launch. They return to the tops of their boxes as revealed cubes, and the cable re-anchors with their true colors.
- **Burial Undo:** When undoing a standard move that places a slice back on top of a revealed linked cube, the snapshot in `LinkedCubeUndoRecord` (or `UndoData`) restores `CubeInfo.IsSecret = true`. The cube and cable return to their mystery appearance.

---

## 4. Technical Architecture

### 4.1 Touch Points & Responsibilities
1. **`LinkedCubePair.cs`**:
   - `Validate(Puzzle puzzle)`: Ensure level validation permits `cube.IsSecret > 0` on linked cubes while preserving restrictions on `GroupKeyId`, `NumLock`, and `IsFixed`.
2. **`LinkCubeLaunch.cs`**:
   - Verification of `!cube.IsSecret` in `IsFullOrdinaryMember` is preserved; returns `LinkedCubeLaunchFailure.BlockedPartner` if a member is at the top but still secret.
3. **`LinkedCubeCable.cs`**:
   - `UpdateTint()`:
     - Check `member.IsSecret`.
     - When secret: apply `GameAssetsConfig.GetSecretMat()` or secret tint color.
     - When revealed: apply `GameAssetsConfig.GetCubeColor(member.Type)`.
     - Cache previous tint state to minimize `MaterialPropertyBlock` churn.
4. **`Box.cs`**:
   - Ensure `CheckHiddenSecret()` is triggered upon initial layout and after slices pop, so if a secret linked cube is at the top at level start or after slice departure, it reveals immediately.
5. **Undo System (`LinkedCubeUndoRecord.cs` & `BoxSnapshot`)**:
   - Verify `BoxSnapshot.Matches` and `BoxSnapshot.Restore` correctly capture and restore `IsSecret` and visual state.

---

## 5. Constraints & Compliance
- **Hierarchy Resolution:** STRICTLY NO `transform.Find` or `GameObject.Find`. Use `HierarchyReferenceCache` or serialized fields.
- **Verification Rule:** Must compile cleanly and pass `dotnet run --project ../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj`.
- **DOTween Lifecycles:** All tweens must have `.SetLink(gameObject)` and clean cleanup.
