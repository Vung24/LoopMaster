using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Snui
{
	public class PassGoldenItem : PassNormalItem
	{
        public AudioClip UnlockSound;
        protected override bool VipTrack => true;
        [SerializeField] protected Transform _particle;
        private bool _unlocking;
        private int _bindingVersion;
        protected override void Awake()
        {
            base.Awake();
            if (_particle == null) _particle = HierarchyReferenceCache.Child(transform, "Particle");
            var effect = _particle;
            if (effect != null)
                foreach (var graphic in effect.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
        }
        public override UniTask Init(int row)
        {
            _bindingVersion++; _unlocking = false;
            return base.Init(row);
        }
        public override void Refresh()
        {
            base.Refresh();
            if (_unlocking)
            {
                if (_lockMark != null) _lockMark.gameObject.SetActive(true);
                else PassUI.Active(transform, "Lock", true);
            }
        }
        public async UniTask PlayUnlockAnim()
        {
            if (_unlocking || !Managers.Pass.IsVip()) return;
            int version = _bindingVersion;
            _unlocking = true; Refresh(); PassUI.Sound(UnlockSound);
            var graphic = _lockGraphic != null ? _lockGraphic : PassUI.Node<Spine.Unity.SkeletonGraphic>(transform, "Lock");
            PassUI.Spine(graphic, "unlock", false);
            float duration = graphic?.SkeletonData?.FindAnimation("unlock")?.Duration ?? .4f;
            await UniTask.Delay(System.TimeSpan.FromSeconds(duration), ignoreTimeScale: true, cancellationToken: this.GetCancellationTokenOnDestroy());
            if (version != _bindingVersion || !isActiveAndEnabled) return;
            _unlocking = false; Refresh();
            var particles = GetComponentsInChildren<ParticleSystem>();
            foreach (var particle in particles) particle.Play();
        }
        private void OnDisable() { _bindingVersion++; _unlocking = false; }
	}
}
