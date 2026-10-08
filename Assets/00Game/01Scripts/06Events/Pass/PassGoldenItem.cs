using UnityEngine;

namespace Snui
{
	public class PassGoldenItem : PassNormalItem
	{
        public AudioClip UnlockSound;
        protected override bool VipTrack => true;
        public Cysharp.Threading.Tasks.UniTask PlayUnlockAnim()
        {
            Refresh(); PassUI.Sound(UnlockSound); PassUI.Animate(transform);
            var particles = GetComponentsInChildren<ParticleSystem>();
            foreach (var particle in particles) particle.Play();
            return Cysharp.Threading.Tasks.UniTask.CompletedTask;
        }
	}
}
