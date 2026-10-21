using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using DG.Tweening;

namespace Snui
{
	public class PassStarProgressBtn : MonoBehaviour
	{
        public AudioClip CoinSound;
        public ProgressBar Bar;
        public TMP_Text StageText;
        public ParticleSystem SparkEffect, FrameEffect;
        public Transform TrailEffect, TutorialFingerTf;
        private int _displayStage;
        private int _displayCoins = -1;
        private Animation _animation;
        private void Awake()
        {
            if (Bar == null) Bar = GetComponentInChildren<ProgressBar>(true);
            if (StageText == null) StageText = PassUI.Node<TMP_Text>(transform, "Level");
            PassUI.Bind(transform, "ProgressBtn", ShowInfo);
            var button = GetComponent<UnityEngine.UI.Button>();
            if (button != null) button.onClick.AddListener(ShowInfo);
            PassUI.Active(transform, "TutorialDesc", false);
            PassUI.Active(transform, "TutorialFinger", false);
            _animation = GetComponent<Animation>();
        }
        private void Start() { if (GetComponentInParent<PassDetailDialog>() == null) Refresh(); }
        private void ShowInfo() => PassInfoDialog.Show(transform.GetComponentInParent<Canvas>()?.rootCanvas.transform).Forget();
        public void Refresh()
        {
            var manager = Managers.Pass;
            if (manager == null) return;
            SetPresentation(manager.GetTotalPoints());
        }
        public void SetPresentation(int totalPoints)
        {
            var manager = Managers.Pass;
            _displayStage = manager.GetStageAtPoints(totalPoints);
            bool final = _displayStage >= manager.GetNormalStageCount();
            bool finished = _displayStage >= manager.GetTotalStageCount();
            PassUI.Active(transform, "Coin", final);
            PassUI.Active(transform, "Level", !final);
            PassUI.Active(transform, "Finished", finished);
            PassUI.Active(transform, "Value", !finished);
            int goal = manager.GetRequirePoints(_displayStage), count = finished ? goal : manager.GetPointsAtTotal(totalPoints);
            if (Bar != null) Bar.SetProgress(count, goal);
            PassUI.Text(transform, "Value", count + "/" + goal);
            int coins = manager.GetCurrentFinalBundleCoin() > 0 ? Mathf.Clamp((_displayStage - manager.GetNormalStageCount()) * manager.FinalStageCoin, 0, manager.FinalTotalCoinCount) : 0;
            PassUI.Text(transform, "Count", coins.ToString());
            if (_displayCoins >= 0 && coins > _displayCoins) PassUI.Sound(CoinSound);
            _displayCoins = coins;
            if (StageText != null) PassUI.Text(StageText.transform.parent, "Level", _displayStage.ToString());
        }
        public async UniTask AnimateCountTo(int count)
        {
            if (Bar == null) return;
            int goal = Managers.Pass.GetRequirePoints(_displayStage);
            var tween = Bar.IncreaseTo(count, .4f, value => PassUI.Text(transform, "Value", value + "/" + goal));
            await tween.ToUniTask().AttachExternalCancellation(this.GetCancellationTokenOnDestroy());
            await UniTask.NextFrame(cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        public void PlayCollectAnim()
        {
            if (_animation != null && _animation.GetClip("ProgressBtn") != null) _animation.Play("ProgressBtn");
            else PassUI.Animate(transform);
            PlaySpark();
        }
        public void PlaySpark() { if (SparkEffect != null) SparkEffect.Play(); }
        public void PlayProgressEffect() { if (FrameEffect != null) FrameEffect.Play(); }
        public void PlayTrailTo(Vector3 endPosition)
        {
            if (TrailEffect == null) return;
            var parent = GetComponentInParent<Canvas>()?.rootCanvas.transform;
            if (parent == null) return;
            var trail = Instantiate(TrailEffect, parent, false);
            trail.position = TrailEffect.position; trail.gameObject.SetActive(true);
            foreach (var graphic in trail.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
            foreach (var particle in trail.GetComponentsInChildren<ParticleSystem>()) particle.Play();
            trail.DOLocalMove(parent.InverseTransformPoint(endPosition), .5f).SetEase(Ease.InQuad).SetUpdate(true)
                .SetLink(trail.gameObject).OnComplete(() => { if (trail != null) Destroy(trail.gameObject); });
        }
        public void ChangeToFinishedUI() => Refresh();
        public void ChangeToFinalMode() => Refresh();
	}
}
