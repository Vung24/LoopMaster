LoopMaster Coding & Agent Instructions
Project Data & Verification Index
- Read `../LoopMaster-ProjectData/DATA_INDEX.md` for current data folders, verification entry points, and naming conventions.
- `../LoopMaster-ProjectData/Verification` contains executable gameplay checks; `../LoopMaster-ProjectData/*Data` and `../LoopMaster-ProjectData/ReferenceData` contain feature reference data and tools.
- Use feature-based data names in new folders and references. Keep technical gameplay meanings such as post-challenge recovery distinct from reference-data provenance.
Hierarchy Resolution Rule: NEVER USE Find
- Never write transform.Find(...) or GameObject.Find(...) in C# scripts.
- Use serialized fields ([SerializeField]) or GetComponent / GetComponentInChildren.
- When dynamic name-based lookup is needed, use HierarchyReferenceCache (Child, Descendant, Path, ChildComponent, DescendantComponent).
- Backend verification ../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj enforces VerifyNoFindPolicy().
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

## Anti-Pattern Rules: Eliminate Delegate Hell & Lambda Overuse

### Explicit Dependencies Instead of Delegate Soup

- Do not pass a collection of `Func<T>` / `Action<T>` callbacks into constructors or initialization methods as a pseudo-decoupling mechanism. Dependencies and state ownership must remain explicit and easy to trace.
- Do not replace ordinary property access with a delegate such as `Func<GameData> getData`. Pass the required owner or data directly. When data can change, make its lifetime explicit: use the authoritative owner for live state, or pass the current value to the operation that needs it.
- More than two dependency or state-query callbacks is a design review signal, not an automatic requirement to introduce a context interface. First check whether the class has too many responsibilities or whether values belong in operation parameters.
- Prefer direct dependencies and action-method parameters. Use a small, cohesive context object/interface only when related operations genuinely share that context and it reduces existing complexity.
- Do not create a broad `IGameplayContext` that exposes unrelated systems, duplicates authoritative state, or acts as a service locator. Do not introduce an interface solely to reduce the constructor parameter count.
- Keep delegates for genuine callbacks, completion notifications, or small interchangeable behaviors. Give retained callbacks a clear owner and lifetime; release references or unsubscribe at the appropriate lifecycle boundary.

### Explicit Initialization and Readable Getters

- Do not use expression-bodied members (`=>`) for lazy initialization, singleton caching, or dependency creation, such as a getter containing `_flow ?? (_flow = new Flow(...))`.
- Initialize dependencies explicitly in `Awake()`, `Start()`, or a named `Initialize(...)` method. Choose the stage based on actual dependency readiness; do not rely on the relative `Awake()` order of different GameObjects.
- Getters and state queries must not initialize systems, mutate gameplay state, or trigger other side effects. Put those operations in intention-revealing methods.
- Do not use expression-bodied members for multi-condition boolean logic spanning multiple lines. Use a standard getter with guard clauses or a named boolean method so each decision can be inspected and debugged.
- Simple, side-effect-free expression-bodied members remain acceptable, for example `IsRunning => state == GameState.Running`.
- Use block-bodied named methods for gameplay commands and multi-step operations. Keep short callback lambdas limited to cases where they clearly improve readability.

### Method Groups and Callback Identity

- Prefer a method group when its signature and behavior match the required delegate: use `OnCompleted` instead of `() => OnCompleted()`, and `ApplyDamage` instead of `amount => ApplyDamage(amount)`.
- Use a lambda only when it performs a necessary adaptation, such as supplying an additional argument or transforming callback data. Extract multi-step logic into a named method.
- For event subscriptions, preserve the handler identity needed for unsubscription. Do not subscribe and unsubscribe with separately created lambda expressions.
- A method group is a readability choice, not proof of zero allocations. Cache a delegate when repeated conversion would otherwise allocate in a measured or clearly high-frequency path, and respect its target object's lifetime.

### Avoid Callback Allocations in Hot Paths

- Do not create capturing lambdas, closures, or recurring delegate allocations in `Update()`, `FixedUpdate()`, ticking loops, per-item loops, or frequent gameplay operations.
- Prefer explicit loops over repeated LINQ queries with predicate lambdas in hot paths. Also avoid associated iterator, temporary collection, and materialization allocations.
- Create stable callbacks once during explicit initialization when needed. Reuse them only while their owner and captured dependencies remain valid; do not cache stale level or scene state.
- Do not assume every lambda allocates or that every non-capturing lambda or method group is allocation-free. Compiler behavior, delegate conversion, and the called API all matter.
- Validate meaningful allocation claims with the Unity Profiler, including the `GC.Alloc` samples for the relevant operation. Do not claim zero allocations without measurement.
- Keep changes local and behavior-preserving. These rules do not require unrelated rewrites of existing callbacks or speculative caching.

## Method Naming: Avoid Unnecessary Try Prefixes

- Do not prefix method names with Try unless failure is an expected, recoverable outcome that the caller needs to handle.
- Use Try only when the method reports success or failure through its return value (normally bool), with out parameters when a result is needed.
- Prefer direct, descriptive action names such as Initialize, Move, Build, or Get when Try semantics are unnecessary. Do not add Try wrappers merely for defensive null checks or to silently skip broken required setup.
- Preserve existing public APIs and framework methods such as TryGetComponent; this rule does not require unrelated renames.

## Unity Agent Coding Guidelines Contract

Goal: clear gameplay code, explicit state, local change, testability, and the smallest architecture that solves the real problem.
The reasoning, examples, and full rule set (§1–63) are in `UNITY_AGENT_CODING_GUIDELINES_EN.md` (Russian mirror: `UNITY_AGENT_CODING_GUIDELINES_RU.md`). Read the relevant section there when a decision is not obvious from this file.

### Hard Rules
- One primary responsibility per class/component. No God Objects / God `MonoBehaviour`s.
- Guard clauses over nested conditions; happy path readable top to bottom; about 2 nesting levels at most.
- One authoritative owner for each piece of runtime state. UI, Animator, VFX, and audio derive from gameplay state, never the reverse.
- Change state through intention-revealing operations (`health.ApplyDamage(amount)`, `inventory.TryAdd(item)`), not public setters driven by an outside manager.
- Mutually exclusive states are an `enum` or state objects, not a pile of booleans.
- Dependencies are explicit. No hidden globals, service locators, or ScriptableObjects used as mutable global runtime state.
- Unity lifecycle methods (`Awake`, `Start`, `Update`, …) only orchestrate. Gameplay rules live in plain C# testable without a scene; test them through observable behavior (Given / When / Then), not internal call chains.
- Config/definition assets (`WeaponDefinition`) stay separate from runtime state (`WeaponState`). Save/network formats map to and from the domain model; they do not shape it.
- No interface, event, layer, pattern, or generic framework for a hypothetical future need. Start with a direct call; add indirection only when it removes complexity that exists now.
- No pass-through layers, global EventBus, `Shared`/`Utils` dumping grounds, clever one-liners, or nested ternaries.
- Optimize only with profiler evidence or a clear scale requirement, and keep optimized code behind a small boundary.

### Size Signals (heuristics, not hard limits)
| Signal | Reconsider when |
|---|---|
| Method | more than 30–40 lines, or mixes levels of abstraction |
| Class | 5+ unrelated dependencies |
| File | more than 300–400 lines, or its name differs from the single type it contains |
| Cognitive Complexity | above 10 simplify; above 15 refactor or justify |

### Naming
- Use game-design vocabulary (`TryAcquireTarget`, `AddThreat`, `ActivateCheckpoint`), not `Manager` / `Helper` / `Process()` / `Handle()` when a precise name exists.
- Full words, not abbreviations (`damageMultiplier`, not `dmgMul`), except domain words (`HP`, `UI`, `id`) and loop indices.
- Names must not lie: `Get*`, properties, `Is*`, `Has*`, `Can*` have no side effects; `Is*` / `Has*` / `Can*` return `bool`; `Try*` returns `bool` plus an `out` result; a plural name holds a collection; events are past facts (`Died`), commands are imperatives (`ApplyDamage`).
- After changing what a method does, re-check its name.
- Comments explain *why*, not *what*.

### Findability for Agents
- One file, one type, matching names.
- A short `/// <summary>` on public feature entry points.
- A 5–10 line `README.md` in large feature folders: what lives here, where the entry points are.
- A small glossary mapping game-design terms to code symbols (`Stagger -> StaggerState`).

### Unity Traps
- Never use `?.`, `??`, `??=`, `is null`, or `is not null` on `UnityEngine.Object`: they bypass Unity's overloaded `==`, so a destroyed object looks alive. Use `== null` / `!= null` (Microsoft.Unity.Analyzers: UNT0007, UNT0008, UNT0029).
- Never delete and recreate a script to move or rename it: move the `.cs` together with its `.meta` (it holds the GUID that scenes and prefabs reference), or move it through Unity.
- Renaming a serialized field requires `[FormerlySerializedAs("oldName")]`.
- A new "Missing script" warning or an unexpected scene/prefab diff is a regression.
- Unsubscribe from events at the matching lifecycle boundary. Give coroutines and async work an owner and a stop path. Make critical initialization order explicit.
- No `Find*`, repeated `GetComponent`, heavy LINQ, or allocations in per-frame hot paths without reason; cache stable references.
- Use `.asmdef` files to enforce feature/layer boundaries; keep Editor-only code in its own asmdef.

### Refactoring Workflow
1. **Pick targets by evidence:** complex *and* frequently changed files first.
   `git log --since=12.months --format= --name-only -- "*.cs" | sort | uniq -c | sort -rn | head -20`
2. **Pin behavior before changing it:** a characterization (EditMode) test, or a written manual check you will repeat.
3. **One kind of refactoring per step** (rename, extract, move, split); each step compiles on its own and never mixes in behavior changes.
4. **Prefer the IDE/Roslyn refactoring engine** for rename, move, and extract over hand-written text edits.
5. **Verify after every step:** clean compile, no new console errors or analyzer warnings, tests pass. Run `dotnet run --project ../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj` and compile verification.
6. **Quote real compiler/test output;** never summarize a run that did not happen.
7. **Stop and ask** before behavior changes, serialized-data migrations, or scene/prefab edits.

### Done Checklist
- [ ] The touched code is simpler, or at least not harder to read, than before.
- [ ] No new God class, boolean state pile, hidden global, pass-through layer, or speculative abstraction.
- [ ] Every touched name still describes what the code does.
- [ ] No touched file grew past ~400 lines; file names match their types.
- [ ] `.meta` files are intact; renamed serialized fields have `[FormerlySerializedAs]`.
- [ ] The project compiles with no new errors or warnings, and tests pass (output quoted).

