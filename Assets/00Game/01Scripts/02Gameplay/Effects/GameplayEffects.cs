using Spine.Unity;
using UnityEngine;

/// <summary>Plays authored effects and removes their complete hierarchy after playback.</summary>
public static class GameplayEffects
{
    public static void PlayParticles(GameObject effect, Color? tint = null)
    {
        if (effect == null) return;
        float lifetime = 0.5f;
        foreach (ParticleSystem particle in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particle.main;
            main.stopAction = ParticleSystemStopAction.None;
            main.loop = false;
            if (tint.HasValue) main.startColor = tint.Value;
            lifetime = Mathf.Max(lifetime, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
            if (particle.gameObject.activeInHierarchy) particle.Play();
        }
        foreach (Animation animation in effect.GetComponentsInChildren<Animation>())
        {
            if (animation.clip == null) continue;
            lifetime = Mathf.Max(lifetime, animation.clip.length);
            animation.Play();
        }
        Object.Destroy(effect, lifetime + 0.1f);
    }

    public static void PlayCombo(GameObject effect, int variant)
    {
        if (effect == null) return;
        float lifetime = 1f;
        for (int i = 0; i < effect.transform.childCount; i++)
        {
            Transform child = effect.transform.GetChild(i);
            bool selected = child.name == "Combo" + variant;
            child.gameObject.SetActive(selected);
            if (!selected) continue;
            SkeletonAnimation skeleton = child.GetComponent<SkeletonAnimation>();
            if (skeleton == null) continue;
            skeleton.Initialize(false);
            var animation = GameUtils.GetSkeletonAnimation(skeleton.Skeleton.Data, "animation");
            if (skeleton.AnimationState == null || animation == null) continue;
            var entry = skeleton.AnimationState.SetAnimation(0, animation, false);
            lifetime = Mathf.Max(lifetime, entry.Animation.Duration);
        }
        Object.Destroy(effect, lifetime + 0.1f);
    }
}
