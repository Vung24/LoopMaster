using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Snui
{
    public class ScrewController : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private AudioClip _clickAudio;
        public UnityAction OnUsedScrewdriver;
        public UnityAction OnShowUsedFailToast;
        public UnityAction<ScrewController> OnScrewDropped;
        private BlockData _block;
        private int _index;
        private bool _canClick = true;
        private bool _used;
        private bool _dropStarted;
        private KeyHunterBoardContext _context;

        public void Init(BlockData block, int index) => Init(block, index, BoardController.DefaultContext);
        public void Init(BlockData block, int index, KeyHunterBoardContext context)
        {
            _block = block;
            _index = index;
            _context = context;
            _canClick = true;
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            _context?.PlayClickAudio?.Invoke(_clickAudio);
            if (_canClick) Use(true).Forget();
        }
        public async UniTask Use(bool showScrewdriver)
        {
            // Reveal calls Use(false) while normal pointer input is disabled.
            if (_used || _block?.Screws == null || _index < 0 || _index >= _block.Screws.Length || !_block.Screws[_index]) return;
            if (showScrewdriver && _context?.ScrewdriverPrefab == null)
                throw new InvalidOperationException("Missing KeyHunter/Prefab/Screwdriver.prefab.");
            if (_context?.UseScrewdriver == null || !_context.UseScrewdriver(_block, _index))
            {
                if (_context == null || !_context.IsRevealing) OnShowUsedFailToast?.Invoke();
                return;
            }
            _block.Screws[_index] = false;
            _used = true;
            _canClick = false;
            if (showScrewdriver) await ShowScrewdriver();
            else
            {
                OnUsedScrewdriver?.Invoke();
                OnScrewDropped?.Invoke(this);
                PlayDropAnimation().OnComplete(() => Destroy(gameObject));
            }
        }
        private UniTask ShowScrewdriver()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("Key Hunter screw requires a Canvas.");
            SkeletonGraphic screwdriver = Instantiate(_context.ScrewdriverPrefab, canvas.transform);
            screwdriver.transform.localPosition = canvas.transform.InverseTransformPoint(transform.position) + new Vector3(6f, 6f, 0f);
            screwdriver.Initialize(false);
            if (screwdriver.AnimationState == null)
            {
                Destroy(screwdriver.gameObject);
                throw new InvalidOperationException("Key Hunter screwdriver has no Spine animation state.");
            }
            screwdriver.AnimationState.Event += OnAnimationEvent;
            screwdriver.AnimationState.Complete += entry =>
            {
                if (screwdriver != null) Destroy(screwdriver.gameObject);
            };
            OnUsedScrewdriver?.Invoke();
            return UniTask.CompletedTask;
        }
        private void OnAnimationEvent(TrackEntry trackEntry, Spine.Event e)
        {
            if (this == null || e.Data.Name != "rotate" || _dropStarted) return;
            _dropStarted = true;
            OnScrewDropped?.Invoke(this);
            PlayDropAnimation().OnComplete(() => Destroy(gameObject));
        }
        private Tween PlayDropAnimation()
        {
            transform.DOKill();
            if (transform.parent != null) transform.SetParent(transform.parent.parent, true);
            return DOTween.Sequence()
                .Append(transform.DOScale(1.2f, 0.4f))
                .Join(transform.DOLocalRotate(new Vector3(0f, 0f, 360f), 0.4f, DG.Tweening.RotateMode.FastBeyond360))
                .Append(transform.DOBlendableLocalMoveBy(new Vector3(0f, -800f, 0f), 0.43f)).SetLink(gameObject);
        }
        public void DisableClick() => _canClick = false;
        public void EnableClick() { if (!_used) _canClick = true; }
    }
}
