# Rendering and animation

## Choose by pipeline and measured cost

- LoopMaster currently uses Built-in rendering. Recheck quality overrides as well as GraphicsSettings. SRP Batcher guidance applies to compatible URP/HDRP projects, not as a switch for Built-in. Avoid unsolicited pipeline migrations.
- Shader/material complexity, pass count, fill rate, draw submission, geometry and animation evaluation are different bottlenecks. Use GPU/CPU timings and Frame Debugger to choose changes; draw-call count alone is not a success metric.

## Shaders, lighting and visibility

- Prefer simple shaders for numerous objects when they meet visual requirements. Use opaque, cutout or alpha blend according to appearance/depth/sorting requirements; they are not interchangeable. Inspect screen coverage, transparent overlap and effect passes.
- Evaluate baked lighting for static scenes, including lightmap memory, loading and dynamic-object lighting. Limit realtime lights/shadows and adjust shadow distance, resolution and per-object casting/receiving according to visible benefit. Baking is not always cheaper in memory.
- Tighten camera clip planes and culling masks where content permits. Reducing far clip helps only when it excludes actual render work; verify objects and effects do not disappear incorrectly.
- Frustum culling is normally provided by Unity renderers; do not introduce redundant custom visibility loops without evidence. Occlusion culling is conditional on useful occluders, scene layout and bake/runtime costs. Ensure renderer bounds are correct.

## Meshes and batching

- Reduce unnecessary geometry and consider LOD when geometry is the bottleneck. Verify silhouettes, UVs, normals and animation quality. Control unique material instances and use compatible shared materials/atlases where useful.
- Static batching can increase memory; do not mark moving objects static. Dynamic batching transforms vertices on the CPU and may cost more than saved submission work. Compare enabled/disabled on the target device.
- GPU instancing requires compatible repeated meshes/materials/shaders and data. Do not assume it batches uGUI or ordinary skinned characters. SRP Batcher reduces compatible render-state preparation cost; it does not necessarily reduce draw-call count.
- Choose techniques based on actual renderer/pipeline compatibility and batch-break reasons. Avoid treating static batching, dynamic batching, instancing and mesh combining as interchangeable universal options.

## Animation

- Avoid Animators on static objects; for simple temporary motion consider the existing tween or code path. Reduce unnecessary curves/keyframes, layers, transitions, bone count and skin influences where quality allows.
- GPU skinning addresses skin deformation, not the whole CPU Animator state machine. Confirm the setting/backend/device is relevant; it is not an optimization for ordinary TMP labels or sprite-only UI.
- Use Animator culling only when offscreen root motion, events and animation state are not required by gameplay. Check SkinnedMeshRenderer bounds and updateWhenOffscreen behavior, then verify offscreen/re-entry transitions. Do not apply CullCompletely blindly to gameplay-critical actors or UI.
- There is no general-purpose 'batch Animator' checkbox. Shared controllers are maintainable but do not eliminate per-instance evaluation. Specialized crowd techniques require a measured crowd bottleneck and an explicit architecture decision.
- Validate visual quality, animation events, root motion, shadows, sorting and target-device CPU/GPU/memory behavior after changes.

## Official references

- [Dynamic batching 2022.3](https://docs.unity3d.com/2022.3/Documentation/Manual/dynamic-batching.html)
- [Animation performance 2022.3](https://docs.unity3d.com/2022.3/Documentation/Manual/MecanimPeformanceandOptimization.html)
