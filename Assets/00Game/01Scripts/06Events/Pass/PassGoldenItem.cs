using UnityEngine;

namespace Snui
{
	public class PassGoldenItem : PassNormalItem
	{
        public AudioClip UnlockSound;
        protected override bool VipTrack => true;
        protected override void Awake()
        {
            base.Awake();
            var effect = HierarchyReferenceCache.Child(transform, "Particle");
            if (effect != null)
                foreach (var graphic in effect.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
        }
        public Cysharp.Threading.Tasks.UniTask PlayUnlockAnim()
        {
            Refresh(); PassUI.Sound(UnlockSound); PassUI.Animate(transform);
            var particles = GetComponentsInChildren<ParticleSystem>();
            foreach (var particle in particles) particle.Play();
            return Cysharp.Threading.Tasks.UniTask.CompletedTask;
        }
	}
}
