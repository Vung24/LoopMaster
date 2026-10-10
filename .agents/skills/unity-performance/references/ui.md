# uGUI, TMP and resource lifetime

## Refresh only relevant changes

- Inspect HudContainer, UICanvas, Snui_Text and their callers as they exist now. Prefer events or explicit dirty state for discrete values; use cached comparisons when no reliable event exists. Initialize on binding/enable and unsubscribe on disable/destroy as ownership requires.
- Compare source values before formatting text or assigning UI properties. Some UI setters already skip identical assignments, but repeatedly constructing strings still allocates. Preserve smoothly animated values and time-based displays where per-frame updates are intentional.
- Cache component and localization references when stable. Avoid repeated GetComponents arrays, full hierarchy scans, LINQ allocations and closure creation in hot paths. Invalidate caches when the hierarchy or component ownership changes; do not use transform.Find/GameObject.Find.
- TMP font/material setters already dirty appropriate state. Avoid blanket ForceMeshUpdate or layout rebuild after unchanged values. Preserve localization, RTL, fallback fonts and shared-material ownership when optimizing labels.

## Canvas rebuilds versus GPU overdraw

- CPU layout/mesh rebuilds and GPU overdraw are separate costs. Identify the actual bottleneck using profiling/Frame Debugger before selecting a remedy.
- Split frequently changing content from static content into a few meaningful Canvas regions. Too many Canvases can increase draw calls and management overhead; do not add one to every widget.
- Reduce nested layout groups and ContentSizeFitter churn on hot paths. Prefer anchors or on-demand layout when appropriate; retain intentional one-time layout synchronization before viewport measurements.
- Disable raycastTarget on decorative graphics and remove unnecessary GraphicRaycasters. Preserve controls, modal blocking and accessibility/input behavior.
- Reduce transparent full-screen layers and unnecessary effects. When a fully covering UI permits it, suspend covered scene rendering/hidden canvases without changing gameplay simulation inadvertently.

## Hiding and animation

- CanvasGroup.alpha is for fades/visibility and does not stop child scripts or Animators. Set interactable/blocksRaycasts according to intended modal behavior; do not let invisible controls receive input accidentally.
- For an entire Canvas that should stay initialized, disabling Canvas.enabled stops its draw calls while retaining geometry. Child Update/Animator code can remain active and needs its own policy. SetActive(false) is suitable when OnDisable and child suspension are desired; compare reactivation behavior and rebuild cost.
- For short UI transitions, consider existing tween/code animation rather than a permanently active Animator. Animator can dirty UI every frame, even when animated values stay unchanged. Cancel/complete tweens appropriately during pooling, disable and destruction.

## Pooling and asynchronous assets

- Pool repeated UI objects and virtualize long lists when item count warrants it. Before pooling, disable the object then reparent; on reuse, reparent, bind data then enable. Reset listeners, selection, animation and previous data; bound pool size to avoid memory growth.
- Addressables operations need clear handle/instance ownership and matching release behavior. Cancellation of the caller may not cancel the underlying load. Dispose late results and prevent duplicate popup/HUD instances using generation or request ownership checks.
- Verify rapid open/close, disable/re-enable, rebind, scene changes, localization changes and cancellation while loading. Measure allocations, rebuild time and retained assets; do not claim lower overdraw solely from fewer text assignments.

## Official reference

- [Unity UI optimization tips](https://unity.com/how-to/unity-ui-optimization-tips)
