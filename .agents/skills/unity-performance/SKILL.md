---
name: unity-performance
description: Review or improve Unity runtime performance in LoopMaster, including save/load stalls, UI rebuilds and allocations, rendering, and animation. Use for performance audits and optimization changes; use individual Unity tool skills for tool invocation details.
---

# Unity performance

## Establish the problem

- Read project AGENTS.md and relevant code before changing behavior. Never introduce transform.Find or GameObject.Find in C#; use serialized references, GetComponent/GetComponentInChildren, or HierarchyReferenceCache for dynamic name lookup.
- Determine Unity version, render pipeline, target device, scripting backend and frame budget. LoopMaster currently uses Unity 2022.3.62f2 and Built-in rendering; recheck ProjectSettings and Packages before relying on that baseline. Existing packages include UniTask, Newtonsoft.Json and Addressables; prefer existing integrations when appropriate.
- Reproduce a concrete slow operation and capture a baseline on the target device where available. Distinguish CPU, GPU, I/O, allocations and retained memory. Record frame times and spikes, not just average FPS. Use the Unity Profiler, Frame Debugger and memory tooling appropriate to the suspected cost.
- If measurement tools or hardware are unavailable, continue with evidence-backed code review and report candidates as unmeasured. Never invent profiler results or promise FPS improvements.

## Select the relevant guidance

- For persistence, serialization, checkpoints and recovery, read [persistence.md](references/persistence.md).
- For uGUI/TMP, HUDs, hidden screens, pooling and asynchronous assets, read [ui.md](references/ui.md).
- For shaders, lighting, batching, culling and animation, read [rendering-animation.md](references/rendering-animation.md).

## Implement and verify

- Preserve gameplay, visual intent, input and lifecycle semantics. Start with the smallest change addressing the measured cost. Do not switch storage formats, add dependencies or migrate render pipelines solely because they appear on an optimization checklist.
- Use caching, event-driven refresh and batching where their invalidation and ownership are clear. Avoid arbitrary delays or lower refresh rates that make gameplay stale.
- Preserve unrelated working-tree edits. A skill update alone does not authorize runtime-code changes.
- For C# changes run relevant available backend checks, including Rules/Verification/CoreRulesVerification.csproj when applicable to hierarchy changes. Use focused runtime checks for UI/input/lifecycle and persistence recovery; do not claim backend checks prove Unity visual behavior.
- Compare the same device, scene, workload and build settings before/after. Inspect regressions in memory, responsiveness, visuals and save integrity. Report changes, evidence, checks actually run and remaining measurement limits.
