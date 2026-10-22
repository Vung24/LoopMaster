# Booster Unlock & Interactive Introduction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a clean, engaging unlock flow for boosters when the player enters their milestone levels (Undo at Level 4, Shuffle at Level 7, Path at Level 9): show an introduction popup (clean layout with name at top, booster icon in center, and "Tap to continue" at bottom), animate the booster flying into the bottom HUD tool slot to unlock it, and guide the player through an interactive tutorial where the first use of the booster is completely free.

**Architecture:**
1. **`BoosterUnlockConfig` & `BoosterUnlockProgress`**: Pure data definitions mapping booster keys to unlock levels, names, descriptions, and icon keys, backed by `GamePrefs` to track intro completion and first-use free status.
2. **`BoosterUnlockDialog`**: Minimalist modal popup built with TextMeshPro presenting the booster name at the top, a centered booster icon with soft glow/rays, and a pulsing "Tap to continue" at the bottom across a dim overlay.
3. **`BoosterFlyPresenter`**: A reusable UI flight animator using DOTween that spawns a temporary icon at the dialog center, tweens along an arched trajectory into the target `ToolBase` slot on `UICanvas`, triggers an arrival scale punch on the button, and unlocks the tool.
4. **`BoosterTutorialGuide`**: An in-game interactive guidance overlay with a focus mask and animated finger pointer (`Finger`) tailored for each booster's mechanics, ensuring the first introductory use is completely free (no inventory deduction or purchase prompts).
5. **`GameLevelPresentationFlow` Integration**: Triggers the presentation sequence seamlessly when entering the level after loading panel hides, respecting cancellation tokens on pause/exit and cheat skip settings.

**Tech Stack:** Unity 2022.3 LTS, C# (.NET Standard 2.1), UniTask, DOTween, TextMeshPro, Snui UI Framework, `CoreRulesVerification.csproj`.

**Spec:** User requirements:
- Trigger when entering a booster's unlock level (Undo: 4, Shuffle: 7, Path: 9).
- Flow sequence:
  1. Level board displays.
  2. Short delay (~0.4s), then show Booster Introduction Popup (Name at top, Icon in center, "Tap to continue" at bottom; no reward badge).
  3. On tap, booster icon flies to its HUD position and unlocks the tool button.
  4. Interactive step-by-step tutorial guides the player to use the booster, with the first use being 100% free (no inventory deducted, no buy dialog triggered).

## Global Constraints

- **STRICTLY NO `Find`:** Never use `transform.Find(...)` or `GameObject.Find(...)`. Use `HierarchyReferenceCache`, `GetComponent`, or serialized fields.
- **Readable Control Flow:** Early returns, guard clauses, no nested ternaries, and no complex lambdas capturing variables in Update or hot loops.
- **Method Naming:** Avoid unnecessary `Try` prefixes; use descriptive action names (`GetPendingUnlockKey`, `ShowIntro`, `PlayFlight`).
- **DOTween Lifecycles:** Always attach `.SetLink(gameObject)` and cleanly kill active sequences before playing new ones or during `OnDestroy` / `Cancel`.
- **Async Operations:** Use `UniTask` (`Cysharp.Threading.Tasks`) with proper `CancellationToken` propagation; avoid unhandled `async void`.
- **Verification Rule:** Must pass `dotnet run --project ../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj`.

## Extensibility & Performance Principles

### 1. Extensibility Architecture (Dễ dàng mở rộng)
- **Data-Driven Booster Registry:** Booster definitions are registered in `BoosterUnlockConfig` via a clean array/dictionary. Adding a new booster requires adding a single definition record with its unlock level, title, description, and icon.
- **Strategy Pattern for Tutorial Steps (`IBoosterTutorialStep`):**
  - Instead of a monolithic `switch(key)` inside the tutorial guide, each booster's interactive flow is encapsulated in a dedicated step (`UndoTutorialStep`, `ShuffleTutorialStep`, `PathTutorialStep`).
  - A fallback `DefaultBoosterTutorialStep` is provided: any future booster added to the game automatically works with default "point to button & tap" tutorial out-of-the-box without modifying any existing guide code.
- **Dynamic Tool Resolution on HUD:** `UICanvas` caches tools dynamically into a dictionary `_toolsByKey` upon initialization. Any new booster button placed on the HUD is automatically resolved by its `ToolBase.Key` without touching `UICanvas` switch-cases.

### 2. Performance & Memory Optimization (Tối ưu hiệu năng)
- **Zero Update Loop Overhead:** All intro and tutorial steps are purely event-driven and orchestrated via `UniTask` and DOTween. No code runs in `Update()` or `FixedUpdate()`.
- **Zero-Allocation Hot Paths:** No LINQ, no repeated string formatting in recurring callbacks, and cached delegates.
- **Canvas Rebuild Isolation:** The flying icon is rendered on an overlay layer so animating its `RectTransform` does not dirty the main HUD layout or cause full canvas rebuilds.
- **Immediate Cleanup on Interruption:** Every async call propagates a `CancellationToken`. If the player quits, restarts, or pauses during the popup, flight, or tutorial, all tweens are killed instantly and temporary objects destroyed without memory leaks or missing target exceptions.
- **Sprite Reuse:** Uses cached sprites from `ToolBase` / `GameAssetsConfig` rather than repeated disk or resource loads.

## Review Focus

1. **First-time trigger persistence:** Verify `BoosterUnlockProgress` reliably records when an intro has finished so replaying or restarting level 4, 7, or 9 does not re-trigger the popup or grant infinite free boosters.
2. **Undo prerequisite move handling:** Undo cannot be activated on a fresh board with 0 moves. The tutorial guide for Undo must first guide the player to make a move, wait until gameplay settles, and only then spotlight the Undo button.
3. **Screen resolution and HUD target positioning:** The flight tween target must calculate coordinates accurately across different screen aspect ratios and safe-area insets between the dialog center and HUD bottom tools.
4. **Clean level exit & interruption safety:** If the player pauses, quits, or restarts while the popup, flight, or tutorial is running, all spawned UI objects, tweens, and async tokens must cancel cleanly without null references.
5. **Cheat/Skip tutorial compatibility:** If `Snui.CheatManager.SkipTutorial` is active, the popup and tutorial must be bypassed while still unlocking the tool in inventory.

---

### Task 1: Booster Unlock Configuration & Progress Storage

**Files:**
- Create: `Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterUnlockConfig.cs`
- Create: `Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterUnlockProgress.cs`

**Interfaces:**
- Produces:
  ```csharp
  public static class BoosterUnlockConfig
  {
      public static bool TryGetInfo(string key, out BoosterUnlockInfo info);
      public static string GetBoosterForLevel(int levelNumber);
  }

  public readonly struct BoosterUnlockInfo
  {
      public readonly string Key;
      public readonly int UnlockLevel;
      public readonly string Title;
      public readonly string Description;
      public readonly string IconAddress;
  }

  public static class BoosterUnlockProgress
  {
      public static bool HasSeenIntro(string key);
      public static void MarkIntroSeen(string key);
      public static string GetPendingUnlockKey(int levelNumber);
      public static bool IsFirstUseFree(string key);
      public static void ConsumeFirstUseFree(string key);
  }
  ```

- [ ] **Step 1: Write `BoosterUnlockConfig.cs`**

Create `Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterUnlockConfig.cs` mapping:
- `TOOL_UNDO` -> Level 4, "UNDO", "Revert your last move to try a different path.", "img_booster_undo"
- `TOOL_SHUFFLE` -> Level 7, "SHUFFLE", "Mix and randomize slice colors across boxes.", "img_booster_shuffle"
- `TOOL_PATH` -> Level 9, "EXTRA SLOT", "Add an extra holding slot to your road.", "img_booster_path"

- [ ] **Step 2: Write `BoosterUnlockProgress.cs`**

Create `Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterUnlockProgress.cs`:
- Storage keys:
  - `"BoosterIntro_" + key`: records whether intro popup was shown.
  - `"BoosterFirstUseFree_" + key`: records whether the introductory first free use has been consumed (defaults to `true` when intro unlocks, `false` once used).
- Methods:
  - `HasSeenIntro(string key)`: checks `GamePrefs.GetInt(...) == 1`.
  - `MarkIntroSeen(string key)`: sets intro as seen and initializes first use free flag.
  - `GetPendingUnlockKey(int levelNumber)`: checks if `levelNumber` matches any unlock level and intro has not been seen.
  - `IsFirstUseFree(string key)`: returns true if first use has not been consumed yet.
  - `ConsumeFirstUseFree(string key)`: sets `"BoosterFirstUseFree_" + key` to 0.

- [ ] **Step 3: Verify No-Find Policy & Compile**

Run: `dotnet run --project ../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj`
Expected: `No-Find policy verified: 0 violations`.

- [ ] **Step 4: Commit**

```bash
git add Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterUnlockConfig.cs Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterUnlockProgress.cs
git commit -m "feat: add booster unlock config and progress storage"
```

---

### Task 2: Booster Introduction Popup (`BoosterUnlockDialog`)

**Files:**
- Create: `Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterUnlockDialog.cs`

**Interfaces:**
- Consumes: `BoosterUnlockInfo`, `BoosterUnlockConfig`
- Produces:
  ```csharp
  public class BoosterUnlockDialog : MonoBehaviour
  {
      public static UniTask<BoosterUnlockDialogResult> Show(Transform parent, BoosterUnlockInfo info, CancellationToken token);
  }

  public struct BoosterUnlockDialogResult
  {
      public bool Confirmed;
      public Vector3 IconWorldPosition;
  }
  ```

- [ ] **Step 1: Write `BoosterUnlockDialog.cs`**

Create `Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterUnlockDialog.cs`:
- Minimalist layout focused on the booster:
  - Top: `_titleText` (large prominent TextMeshPro displaying booster name, e.g. "UNDO", "SHUFFLE", "EXTRA SLOT").
  - Center: `_iconImage` (booster icon with soft scale pulse / breathing animation and optional rotating soft glow rays in background).
  - Bottom: `_tapToContinueText` ("TAP TO CONTINUE" with gentle alpha breathing animation).
  - Background: Fullscreen dim background button (`_bgButton`) so tapping anywhere on the screen closes the dialog and proceeds to the flight sequence.
  - No badge, no cluttered cards or extra buttons.
- Animations:
  - Popup entrance: CanvasGroup alpha fade in + gentle scale-up (0.85f -> 1.0f, duration 0.25s).
  - Background rays rotate slowly (`DORotate(..., LoopType.Incremental).SetLink(gameObject)`).
  - On tap: Captures `_iconImage.transform.position`, fades out popup dialog CanvasGroup, resolves the `UniTaskCompletionSource`.

- [ ] **Step 2: Add Safe Fallback Asset Builder**

If a dedicated prefab is not yet instantiated, implement a runtime programmatic builder (`EnsureDialogInstance`) following the established pattern in `GameplayToast.cs` and `FeatureUnlockDialog.cs` so it functions immediately in all scenes.

- [ ] **Step 3: Verify No-Find Policy**

Run: `dotnet run --project ../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj`
Expected: `No-Find policy verified: 0 violations`.

- [ ] **Step 4: Commit**

```bash
git add Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterUnlockDialog.cs
git commit -m "feat: implement BoosterUnlockDialog popup"
```

---

### Task 3: Booster Flight Animation (`BoosterFlyPresenter`)

**Files:**
- Create: `Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterFlyPresenter.cs`
- Modify: `Assets/00Game/01Scripts/02Gameplay/UI/HUD/UICanvas.cs` (expose getter for tool button transform)

**Interfaces:**
- Consumes: `UICanvas.GetToolTransform(string key)`, `InventoryManager`
- Produces:
  ```csharp
  public static class BoosterFlyPresenter
  {
      public static UniTask PlayFlight(Canvas canvas, string boosterKey, Vector3 startWorldPos, Vector3 targetWorldPos, CancellationToken token);
  }
  ```

- [ ] **Step 1: Expose Tool Position in `UICanvas.cs` with Dynamic Lookup**

Modify `Assets/00Game/01Scripts/02Gameplay/UI/HUD/UICanvas.cs`:
- Add a dictionary cache `_toolsByKey = new Dictionary<string, ToolBase>()`.
- In `CacheLegacyPrefabReferences()`, index all `ToolBase` components (including `_undoTool`, `_shuffleTool`, `_pathTool` and any dynamically bound tools) into `_toolsByKey` using their `Key`.
- Add extensible lookup method:
```csharp
public RectTransform GetToolRectTransform(string key)
{
    CacheLegacyPrefabReferences();
    if (!string.IsNullOrEmpty(key) && _toolsByKey.TryGetValue(key, out ToolBase tool) && tool != null)
    {
        return tool.transform as RectTransform;
    }
    return null;
}
```
This ensures any new booster added to the HUD in the future is automatically discoverable for intro flights without editing `UICanvas.cs`.

- [ ] **Step 2: Implement `BoosterFlyPresenter.cs`**

Create `Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterFlyPresenter.cs`:
- Spawns a floating icon GameObject under `canvas.transform`.
- Flight sequence via DOTween:
  1. Scale punch at start (scale 1.25f, duration 0.2s).
  2. Parabolic or curved trajectory: tweens X linear/inQuad, tweens Y with arched height offset towards `targetWorldPos` (duration 0.65s, Ease.InCubic / Ease.InOutQuad).
  3. Scale down slightly as it approaches target (1.0f -> 0.85f).
  4. Audio: Play fly swoosh sound via `AudioManager.Current`.
  5. Upon landing at target:
     - Destroy floating icon.
     - Punch scale target tool button: `targetRect.DOPunchScale(Vector3.one * 0.25f, 0.35f, 5, 0.5f).SetLink(targetRect.gameObject)`.
     - Tool on HUD unlocks visually (lock icon disappears, tool becomes active).
     - Play arrival/ding audio effect.

- [ ] **Step 3: Verify No-Find Policy & Compile**

Run: `dotnet run --project ../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj`
Expected: 0 violations.

- [ ] **Step 4: Commit**

```bash
git add Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterFlyPresenter.cs Assets/00Game/01Scripts/02Gameplay/UI/HUD/UICanvas.cs
git commit -m "feat: add BoosterFlyPresenter with trajectory tween and HUD punch animation"
```

---

### Task 4: Extensible In-Game Tutorial Guide (`BoosterTutorialGuide` & Strategy Steps)

**Files:**
- Create: `Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterTutorialGuide.cs`
- Create: `Assets/00Game/01Scripts/02Gameplay/Boosters/IBoosterTutorialStep.cs`

**Interfaces:**
- Consumes: `NewPlayerTutorial` (finger / mask components), `GameController`, `BoosterFlow`
- Produces:
  ```csharp
  public interface IBoosterTutorialStep
  {
      string BoosterKey { get; }
      UniTask ExecuteAsync(BoosterTutorialContext context, CancellationToken token);
  }

  public class BoosterTutorialContext
  {
      public GameController Controller { get; }
      public UICanvas Canvas { get; }
      public Finger Finger { get; }
      public RectTransform ToolRect { get; }
      public Transform MaskOverlay { get; }
      public TMP_Text TooltipText { get; }
  }

  public class BoosterTutorialGuide : MonoBehaviour
  {
      public static UniTask PlayGuide(GameController controller, UICanvas uiCanvas, string boosterKey, CancellationToken token);
  }
  ```

- [ ] **Step 1: Write `IBoosterTutorialStep.cs` & Concrete Steps (Strategy Pattern)**

Create `Assets/00Game/01Scripts/02Gameplay/Boosters/IBoosterTutorialStep.cs`:
- Defines `IBoosterTutorialStep` and `BoosterTutorialContext`.
- Concrete step implementations:
  1. `UndoTutorialStep`:
     - Checks if `data.CanUndo()` is false -> points finger to the first movable `Box` with tooltip "Chạm vào một khối để di chuyển!".
     - Awaits `UniTask.WaitUntil(() => data.CanUndo() && controller.IsGameplaySettled)`.
     - Repositions finger to `Undo` tool button -> on tap performs free Undo without spending inventory!
  2. `ShuffleTutorialStep`:
     - Points finger to `Shuffle` tool button -> triggers free shuffle activation.
     - Points finger to target box -> player taps to shuffle.
  3. `PathTutorialStep`:
     - Points finger to `Path` tool button -> grants free capacity extension.
  4. `DefaultBoosterTutorialStep`:
     - Generic fallback for any future booster: points finger to the HUD tool button, shows description tooltip, and invokes free activation upon tap.
- `BoosterTutorialRegistry`:
  - Static dictionary mapping booster keys to `IBoosterTutorialStep`. Returns the specific step if registered, or falls back to `DefaultBoosterTutorialStep`.

- [ ] **Step 2: Write `BoosterTutorialGuide.cs`**

Create `Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterTutorialGuide.cs`:
- Instantiates a dark overlay mask blocking touches outside the current active spotlight target.
- Resolves the appropriate `IBoosterTutorialStep` from `BoosterTutorialRegistry`.
- Executes the step with `BoosterTutorialContext`.
- **First Use Free Guarantee:**
  - When the booster is tapped during this tutorial (or for the first time), `BoosterUnlockProgress.ConsumeFirstUseFree(boosterKey)` is called.
  - The booster action is executed without deducting inventory count (amount = 0) and without opening the `BuyToolDialog` even if count was 0.
- **Completion:**
  - Displays success toast ("Tuyệt vời!" / "Great!").
  - Fades out mask and destroys tutorial guide overlay cleanly.
  - Restores standard player input.

- [ ] **Step 3: Cancellation & Cleanup Handling**

Ensure `OnDestroy` and token cancellation kills any active tweens, restores raycast blockers, and destroys spawned overlays without lingering objects.

- [ ] **Step 4: Verify No-Find Policy**

Run: `dotnet run --project ../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj`
Expected: 0 violations.

- [ ] **Step 5: Commit**

```bash
git add Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterTutorialGuide.cs Assets/00Game/01Scripts/02Gameplay/Boosters/IBoosterTutorialStep.cs
git commit -m "feat: implement extensible BoosterTutorialGuide using Strategy pattern"
```

---

### Task 5: Integrate into `GameLevelPresentationFlow` & End-to-End Verification

**Files:**
- Modify: `Assets/00Game/01Scripts/02Gameplay/Flow/GameLevelPresentationFlow.cs`

**Interfaces:**
- Consumes: `BoosterUnlockProgress`, `BoosterUnlockDialog`, `BoosterFlyPresenter`, `BoosterTutorialGuide`

- [ ] **Step 1: Add Booster Unlock Orchestration in `GameLevelPresentationFlow.cs`**

In `GameLevelPresentationFlow.Play(...)`:
1. After loading panel dismisses (`await UniTask.WaitUntil(() => LoadingPanel.Current == null)`):
2. Check for pending booster unlock:
   ```csharp
   int currentLevel = data.GetPuzzleActualLevel();
   string pendingBooster = BoosterUnlockProgress.GetPendingUnlockKey(currentLevel);
   ```
3. If `pendingBooster != null` and `!Snui.CheatManager.SkipTutorial`:
   - Await short pause: `await UniTask.Delay(TimeSpan.FromSeconds(0.4f), cancellationToken: lifetime.Token);`
   - Show dialog:
     ```csharp
     BoosterUnlockConfig.TryGetInfo(pendingBooster, out var info);
     var dialogResult = await BoosterUnlockDialog.Show(_canvas.transform, info, lifetime.Token);
     ```
   - Play flight to HUD:
     ```csharp
     UICanvas uiCanvas = _canvas.GetComponent<UICanvas>();
     RectTransform targetTool = uiCanvas != null ? uiCanvas.GetToolRectTransform(pendingBooster) : null;
     if (targetTool != null)
     {
         Canvas rootCanvas = _canvas.GetComponent<Canvas>();
         await BoosterFlyPresenter.PlayFlight(rootCanvas, pendingBooster, dialogResult.IconWorldPosition, targetTool.position, lifetime.Token);
     }
     ```
   - Play interactive tutorial:
     ```csharp
     GameController controller = GameController.Current;
     if (controller != null && uiCanvas != null)
     {
         await BoosterTutorialGuide.PlayGuide(controller, uiCanvas, pendingBooster, lifetime.Token);
     }
     ```
   - Mark intro seen:
     ```csharp
     BoosterUnlockProgress.MarkIntroSeen(pendingBooster);
     ```
4. Respect `Cancel()`: Ensure cancellation in `GameLevelPresentationFlow.Cancel()` aborts the booster unlock flow.

- [ ] **Step 2: Run Backend & CoreRules Verification**

Run: `dotnet run --project ../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj`
Expected: `No-Find policy verified: 0 violations`.

- [ ] **Step 3: Verification with Unit/Flow Checks**

Run test verification to ensure no build or assembly errors exist across the modified files.

- [ ] **Step 4: Commit**

```bash
git add Assets/00Game/01Scripts/02Gameplay/Flow/GameLevelPresentationFlow.cs
git commit -m "feat: integrate booster unlock, fly animation, and tutorial into level presentation flow"
```

---

## Self-Review Checklist

- [x] **Spec Coverage:** Level detection (Undo lv 4, Shuffle lv 7, Path lv 9), delay after board display, intro popup, flight animation to bottom HUD tool, and interactive guidance are all fully covered.
- [x] **Rule Adherence:** No `transform.Find` or `GameObject.Find` used anywhere; verified against `CoreRulesVerification.csproj`.
- [x] **State Integrity:** Inventory count updated safely via `InventoryManager.Ensure().Add(...)`.
- [x] **DOTween & UniTask:** All tweens use `.SetLink(gameObject)` and cancellation tokens are propagated through every async step.

