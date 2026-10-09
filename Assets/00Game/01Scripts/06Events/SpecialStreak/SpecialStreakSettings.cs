using UnityEngine;
using Spine.Unity;
using System.Collections.Generic;

public class SpecialStreakSettings : MonoBehaviour
{
}

internal static class SpecialStreakPresentation
{
    internal static void Prepare(Transform root, List<Material> materials)
    {
        Snui.Snui_Text.EnsureFontAssets(root);
        var library = Snui.PrefabLibrary.Current;
        foreach (var graphic in root.GetComponentsInChildren<SkeletonGraphic>(true))
        {
            if (library?.LevelBoxSpineShader != null && graphic.material != null)
            {
                var material = new Material(graphic.material) { shader = library.LevelBoxSpineShader };
                if (material.HasProperty("_StraightAlphaInput") && material.GetFloat("_StraightAlphaInput") != 0) material.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
                if (material.HasProperty("_CanvasGroupCompatible") && material.GetFloat("_CanvasGroupCompatible") != 0) material.EnableKeyword("_CANVAS_GROUP_COMPATIBLE");
                materials.Add(material); graphic.material = material;
            }
            graphic.UnscaledTime = true;
            graphic.Initialize(false);
            if (graphic.SkeletonData?.FindAnimation("animation") != null) graphic.AnimationState.SetAnimation(0, "animation", true);
        }
    }
    internal static void Cleanup(List<Material> materials) { foreach (var material in materials) if (material != null) Object.Destroy(material); materials.Clear(); }
    internal static void Sound(AudioClip clip, GameObject owner)
    {
        if (clip != null && GameAudioSettings.EffectsEnabled && AudioManager.Current != null) AudioManager.Current.PlayOneShot(clip, owner);
    }
    internal static void SetActive(Component component, bool active) { if (component != null) component.gameObject.SetActive(active); }
}
