using Cysharp.Threading.Tasks;
using Spine.Unity;
using UnityEngine;

namespace Snui
{
    public class Finger : MonoBehaviour
    {
        [SerializeField] private SkeletonGraphic _fingerSkeleton;
        private Vector3 _originScale;

        public static UniTask<Finger> Show(Transform parent)
        {
            PrefabLibrary library = PrefabLibrary.Current;
            GameObject instance = Instantiate(library.FingerPrefab, parent, false);
            Finger finger = instance.GetComponent<Finger>();
            return UniTask.FromResult(finger);
        }

        private void Awake()
        {
            _originScale = transform.localScale;
            if (_fingerSkeleton == null) _fingerSkeleton = GetComponentInChildren<SkeletonGraphic>(true);
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group != null) { group.interactable = false; group.blocksRaycasts = false; }
        }

        private void Start() { PlayAnimation(); }

        public void ShowFinger(Vector2 anchoredPosition, bool flipX = false)
        {
            gameObject.SetActive(true);
            RectTransform rect = transform as RectTransform;
            if (rect != null) rect.anchoredPosition = anchoredPosition;
            transform.localScale = new Vector3(_originScale.x * (flipX ? -1f : 1f), _originScale.y, _originScale.z);
            PlayAnimation();
        }

        private void PlayAnimation()
        {
            if (_fingerSkeleton == null || _fingerSkeleton.skeletonDataAsset == null) return;
            _fingerSkeleton.raycastTarget = false;
            _fingerSkeleton.UnscaledTime = true;
            _fingerSkeleton.Initialize(false);
            var data = _fingerSkeleton.skeletonDataAsset.GetSkeletonData(false);
            if (data == null || data.Animations.Count == 0 || _fingerSkeleton.AnimationState == null) return;
            var animation = GameUtils.GetSkeletonAnimation(data, "finger_click") ?? data.Animations.Items[0];
            _fingerSkeleton.AnimationState.SetAnimation(0, animation, true);
        }

        public void HideFinger() { gameObject.SetActive(false); }
    }
}
