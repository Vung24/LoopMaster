LoopMaster Coding & Agent Instructions
Hierarchy Resolution Rule: NEVER USE Find
- Never write transform.Find(...) or GameObject.Find(...) in C# scripts.
- Use serialized fields ([SerializeField]) or GetComponent / GetComponentInChildren.
- When dynamic name-based lookup is needed, use HierarchyReferenceCache (Child, Descendant, Path, ChildComponent, DescendantComponent).
- Backend verification Rules/Verification/CoreRulesVerification.csproj enforces VerifyNoFindPolicy().
Unity Version & Project Consistency
- Target Unity 2022.3 LTS and the project's existing C# language level and installed packages.
- Prefer existing project utilities and patterns over introducing new libraries, managers, or architectural frameworks.
- Do not add or upgrade Unity packages, change ProjectSettings, or modify assembly definitions unless explicitly requested.
- Do not modify generated folders such as Library/, Temp/, Obj/, or Build/.
Component References & Initialization
- Prefer [SerializeField] private for stable scene/prefab references; prefer one-time component lookup for components on dynamically instantiated objects.
- Cache repeatedly used component references in Awake, Start, or explicit initialization; avoid repeated hierarchy/component searches in hot paths.
- Validate required references before use. Log actionable errors for broken setup instead of silently ignoring required gameplay dependencies.
- Do not assume the Awake or OnEnable execution order between different GameObjects. Make cross-object initialization dependencies explicit.
- Avoid changing serialized field names or types without checking prefab and scene compatibility; use [FormerlySerializedAs] when applicable.
Gameplay Architecture & State Consistency
- Read the relevant gameplay flow and callers before modifying core systems such as BoardController, GameController, road/path building, slice movement, input, and win/lose handling.
- Treat existing path/index mappings and board state as gameplay contracts. Check index bounds, path validity, and mapping invariants before changing their logic.
- Keep authoritative gameplay state separate from visual animation. A tween completing or a Transform moving must not be the sole source of truth for board/path state.
- Avoid adding duplicate state (for example, parallel path collections that can drift out of sync) unless synchronization is explicitly designed and tested.
- Keep controller responsibilities consistent with existing code. Do not bypass existing events, transitions, or validation checks to implement a shortcut.
- If changing path or slice behavior, inspect at least these scenarios: initial spawn, normal move, interrupted/blocked move, restart or level reload, and win/lose transition where applicable.
Runtime Performance & Allocations
- Avoid unnecessary managed allocations in frequently called code (Update, FixedUpdate, repeated per-item loops, and large path traversals).
- Avoid repeated LINQ queries, string concatenation, temporary collections, and component lookups in hot paths when a simple cached or reusable alternative exists.
- Reuse collections where practical; prefer the project's existing pooling strategy for high-frequency spawn/despawn operations.
- Do not optimize speculatively at the expense of correctness. Explain performance-driven changes and use the Unity Profiler to validate meaningful claims.
- Keep per-frame logging out of production gameplay paths unless it is explicitly guarded as diagnostic behavior.
Lifecycle, Events, DOTween & Async
- Register event handlers in the appropriate lifecycle stage and unregister symmetrically; avoid duplicate subscriptions after repeated enable/disable cycles.
- Treat object disable, destruction, scene unload, and level reset as potential interruptions to tweens and async operations.
- Track and clean up owned DOTween sequences/tweens as appropriate. Do not create uncontrolled overlapping tweens for the same gameplay action.
- Cancel and dispose owned CancellationTokenSource instances when their lifetime ends. When using UniTask, propagate cancellation tokens through operations that can outlive their caller.
- Do not use async void for general-purpose async methods; reserve it for required event-style entry points and explicitly handle exceptions there.
- Do not access UnityEngine scene objects from background threads (for example, inside Task.Run).
- Preserve the project's existing Coroutine/Task/UniTask conventions instead of converting between them without a concrete reason.
Unity Assets, Prefabs & Serialization
- Preserve .meta files and GUIDs. When adding, moving, renaming, or removing assets, keep their .meta files consistent.
- Do not edit prefabs, scenes, ScriptableObjects, or materials unless the requested change requires it.
- Before changing a serialized field, inspect its references and potential effects on scenes, prefabs, and assets.
- Avoid modifying third-party plugin sources and generated code unless explicitly requested.
- Preserve serialized data and public APIs when refactoring; call out unavoidable breaking changes.
Agent Change Scope & Communication
- Inspect relevant files and call sites before making edits; do not infer architecture from class names alone.
- Implement the smallest change that satisfies the request. Do not mix unrelated refactors, renames, formatting, or dependency upgrades into the same change.
- Reuse existing interfaces, naming conventions, folders, and abstractions. Introduce a new abstraction only when it removes concrete duplication or clearly improves maintainability.
- Do not silently change gameplay rules, level data, visual behavior, or timing constants. Explain the rationale for any intentional behavior change.
- If a requested fix requires assumptions about unclear gameplay behavior, state those assumptions in the final report.
- After editing, summarize changed files, gameplay impact, tests run, and any unverified risks. Never claim a test or Unity Editor validation ran unless it actually did.
Verification Checklist
- Confirm the original Find prohibition still holds and run VerifyNoFindPolicy() if its verification project is available.
- Run an available compile/build/test check appropriate to the changed files; distinguish an actual check from a suggested manual check.
- For gameplay state changes, verify representative happy paths and edge cases, including restart/reload and object disable/destruction where relevant.
- Review the diff for accidental scene/prefab/.meta changes, unexpected package edits, and newly introduced null-sensitive references.
- If Unity Editor is unavailable, report that limitation and provide specific in-Editor checks instead of reporting success.
## Readable Control Flow: Minimize Lambdas & Complex If/Else

- Prefer named methods over lambda expressions (=>)
  for gameplay logic and multi-step operations.

- Use lambdas only for short, simple callbacks
  when they clearly improve readability.

- Avoid lambdas that capture local variables
  inside Update, FixedUpdate, or hot paths.

- Prefer early return and guard clauses
  over deeply nested if/else blocks.

- Avoid nested ternary operators (? :)
  and overly compact conditional expressions.

- Use explicit if/else for non-trivial logic.

- Preserve existing behavior when simplifying code.

## Method Naming: Avoid Unnecessary Try Prefixes

- Do not prefix method names with Try unless failure is an expected, recoverable outcome that the caller needs to handle.
- Use Try only when the method reports success or failure through its return value (normally bool), with out parameters when a result is needed.
- Prefer direct, descriptive action names such as Initialize, Move, Build, or Get when Try semantics are unnecessary. Do not add Try wrappers merely for defensive null checks or to silently skip broken required setup.
- Preserve existing public APIs and framework methods such as TryGetComponent; this rule does not require unrelated renames.
