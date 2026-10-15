using Spine.Unity;
using UnityEngine;

public static class GameplayEffects
{
    private const int FirstComboSortingOrder = 2;
    private static int _nextComboSortingOrder = FirstComboSortingOrder;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetComboSortingOrder() => _nextComboSortingOrder = FirstComboSortingOrder;

    public static void BringComboToFront(Renderer renderer)
    {
        if (renderer == null) return;
        // Ribbons use order 1. New labels must also render above older combos.
        renderer.sortingOrder = _nextComboSortingOrder++;
        if (_nextComboSortingOrder > short.MaxValue) _nextComboSortingOrder = FirstComboSortingOrder;
    }

	public static void PlaySlicePopIn(GameObject effect, Color color)
	{
		if (effect == null) return;
		PooledParticleEffect pooled = effect.GetComponent<PooledParticleEffect>();
		if (pooled != null)
		{
			pooled.Play(effect.transform.position, effect.transform.rotation, color);
			return;
		}
		foreach (ParticleSystem particle in effect.GetComponentsInChildren<ParticleSystem>(true))
			particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
		PlayParticles(effect, color);
	}

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
            if (particle.gameObject.activeInHierarchy) particle.Play(false);
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
            // Spine uses an XY plane. Align the rendered child to the camera,
            // overriding the prefab tilt and the box's rotation together.
            Camera view = GameController.Current != null ? GameController.Current.MainCamera : Camera.main;
            if (view != null) child.rotation = view.transform.rotation;
            SkeletonAnimation skeleton = child.GetComponent<SkeletonAnimation>();
            if (skeleton == null) continue;
            skeleton.Initialize(false);
            if (skeleton.Skeleton == null || skeleton.AnimationState == null) continue;
            var animation = GameUtils.GetSkeletonAnimation(skeleton.Skeleton.Data, "animation");
            if (animation == null) continue;
            // Awake starts the prefab animation. Discard it instead of mixing into
            // another copy of the same clip, and apply the initial pose immediately.
            skeleton.AnimationState.ClearTracks();
            skeleton.Skeleton.SetToSetupPose();
            var entry = skeleton.AnimationState.SetAnimation(0, animation, false);
            entry.MixDuration = 0f;
            BringComboToFront(child.GetComponent<MeshRenderer>());
            skeleton.Update(0f);
            skeleton.LateUpdate();
            lifetime = Mathf.Max(lifetime, entry.Animation.Duration);
        }
        Object.Destroy(effect, lifetime + 0.1f);
    }
}
