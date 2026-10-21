# LoopMaster Development & Coding Guidelines

## 1. Hierarchy & Transform Resolution Policy: STRICTLY NO `Find`
- **DO NOT** use `transform.Find(...)`, `GameObject.Find(...)`, or string-based search in runtime game code (`Assets/00Game/01Scripts`).
- **Hierarchy Resolution Priority:**
  1. **Inspector Serialized Fields:** Always prefer `[SerializeField] private ComponentType _field;` wired in the prefab or scene.
  2. **Direct Component Lookup:** Use `GetComponent<T>()` or `GetComponentInChildren<T>(true)` when resolving components on the current hierarchy node.
  3. **HierarchyReferenceCache:** When resolving named hierarchy nodes dynamically, **always** use `HierarchyReferenceCache` methods:
     - `HierarchyReferenceCache.Child(transform, "NodeName")` for direct children.
     - `HierarchyReferenceCache.Descendant(transform, "NodeName")` for deep recursive search.
     - `HierarchyReferenceCache.Path(transform, "Node1", "Node2")` for explicit paths.
     - `HierarchyReferenceCache.ChildComponent<T>(transform, "NodeName")` for direct child components.
     - `HierarchyReferenceCache.DescendantComponent<T>(transform, "NodeName")` for descendant components.
     - `HierarchyReferenceCache.Component<T>(node)` for null-safe component access.

## 2. Backend & Rule Verification
- The project enforces this rule programmatically via `VerifyNoFindPolicy()` in `Rules/Verification/Program.cs`.
- Running `dotnet run --project Rules/Verification/CoreRulesVerification.csproj` will automatically scan all runtime scripts under `Assets/00Game/01Scripts` and fail if any `transform.Find` or `GameObject.Find` is detected.

## 3. General Architecture & Standards
- **Async Operations:** Use `Cysharp.Threading.Tasks` (`UniTask`), avoid raw `Task` and avoid unhandled `async void` (use `async UniTaskVoid` with `.Forget()` only at fire-and-forget entry points).
- **DOTween Animations:** Always attach `.SetLink(gameObject)` to tweens and kill running tweens in `OnDestroy` or before playing new tweens to prevent memory leaks or missing target exceptions.
- **Audio & Progress:** Use `AudioManager.Current` / `GameAudioPlayer.Current` and `GameProgressStore` for game states.

## 4. Unity Agent Coding Guidelines & Skills
- The full guidelines (§1–63) are available in `UNITY_AGENT_CODING_GUIDELINES_EN.md` (and Russian mirror `UNITY_AGENT_CODING_GUIDELINES_RU.md`).
- Multi-file and large refactoring: use `/unity-refactor` skill (`.agents/skills/unity-refactor/SKILL.md`).
- Guideline verification: use `/guideline-reviewer` skill (`.agents/skills/guideline-reviewer/SKILL.md`) with scopes `code`, `architecture`, `safety`, or `all`.

