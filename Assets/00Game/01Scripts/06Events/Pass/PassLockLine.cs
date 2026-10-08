using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace Snui
{
	public class PassLockLine : MonoBehaviour
	{
        public AudioClip LockSound, UnlockSound;
        public Image Left, Right;
        public Transform LockBg;
        public Spine.Unity.SkeletonGraphic LockSpine;
        private float _displayStage = -1;
        private Vector2 _leftOrigin, _rightOrigin;
        private bool _moving;
        private void Awake()
        {
            if (Left == null) Left = PassUI.Node<Image>(transform, "Left");
            if (Right == null) Right = PassUI.Node<Image>(transform, "Right");
            if (LockBg == null) LockBg = HierarchyReferenceCache.Descendant(transform, "LockBg");
            if (LockSpine == null) LockSpine = PassUI.Node<Spine.Unity.SkeletonGraphic>(transform, "Lock");
            if (Left != null) _leftOrigin = Left.rectTransform.anchoredPosition;
            if (Right != null) _rightOrigin = Right.rectTransform.anchoredPosition;
        }
        public async UniTask PlayUnlockAnim()
        {
            _moving = true; PassUI.Sound(UnlockSound);
            PassUI.Spine(LockSpine, "unlock", false);
            if (LockBg != null) LockBg.DOScale(0, .4f).SetUpdate(true).SetLink(gameObject);
            await MoveSides(true);
        }
        public async UniTask PlayLockAnim()
        {
            PassUI.Sound(LockSound);
            await MoveSides(false);
            _moving = false;
            if (LockBg != null) LockBg.localScale = Vector3.one;
            Refresh();
        }
        private async UniTask MoveSides(bool open)
        {
            var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            if (Left != null) sequence.Join(Left.rectTransform.DOAnchorPosX(_leftOrigin.x - (open ? Left.rectTransform.rect.width : 0), .45f));
            if (Right != null) sequence.Join(Right.rectTransform.DOAnchorPosX(_rightOrigin.x + (open ? Right.rectTransform.rect.width : 0), .45f));
            await sequence.ToUniTask().AttachExternalCancellation(this.GetCancellationTokenOnDestroy());
        }
        public void SetDisplayStage(float stage) { _displayStage = stage; Refresh(); }
        public void Refresh()
        {
            bool locked = Managers.Pass != null && !Managers.Pass.IsVip();
            var manager = Managers.Pass;
            if (manager == null) return;
            float stage = _displayStage < 0 ? manager.GetCurrentStage() : _displayStage;
            gameObject.SetActive(stage < manager.GetNormalStageCount());
            transform.SetAsLastSibling();
            var rect = transform as RectTransform;
            if (rect != null) rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -(PassTicketCell.Height + Mathf.Max(1, stage) * PassRewardsCell.Height));
            PassUI.Active(transform, "LockBg", locked);
            PassUI.Active(transform, "Lock", locked);
            if (locked && !_moving) PassUI.Spine(LockSpine, "idle", true);
        }
        private void Start() => Refresh();
	}
}
