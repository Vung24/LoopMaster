using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Snui
{
	public class PassStarProgressBtn : MonoBehaviour
	{
        public AudioClip CoinSound;
        public ProgressBar Bar;
        public TMP_Text StageText;
        public ParticleSystem SparkEffect, FrameEffect;
        public Transform TrailEffect, TutorialFingerTf;
        private void Awake()
        {
            if (Bar == null) Bar = GetComponentInChildren<ProgressBar>(true);
            if (StageText == null) StageText = PassUI.Node<TMP_Text>(transform, "Level");
            PassUI.Bind(transform, "ProgressBtn", ShowInfo);
            var button = GetComponent<UnityEngine.UI.Button>();
            if (button != null) button.onClick.AddListener(ShowInfo);
            PassUI.Active(transform, "TutorialDesc", false);
            PassUI.Active(transform, "TutorialFinger", false);
        }
        private void Start() => Refresh();
        private void ShowInfo() => PassInfoDialog.Show(transform.GetComponentInParent<Canvas>()?.rootCanvas.transform).Forget();
        public void Refresh()
        {
            var manager = Managers.Pass;
            if (manager == null) return;
            bool final = manager.IsFinalRewardUnlocked();
            PassUI.Active(transform, "Coin", final);
            PassUI.Active(transform, "Level", !final);
            PassUI.Active(transform, "Finished", manager.IsFinalBundleFinished());
            if (Bar != null) Bar.SetProgress(manager.IsFinalBundleFinished() ? manager.FinalStagePoint : manager.GetCurrentPoints(), final ? manager.FinalStagePoint : manager.GetNextRequirePoints());
            PassUI.Text(transform, "Value", manager.GetCurrentPoints() + "/" + manager.GetNextRequirePoints());
            PassUI.Text(transform, "Count", manager.GetCurrentFinalBundleCoin().ToString());
            if (StageText != null) StageText.text = manager.GetCurrentStage().ToString();
        }
        public void PlayCollectAnim() { PassUI.Animate(transform); PlaySpark(); Refresh(); }
        public void PlaySpark() { if (SparkEffect != null) SparkEffect.Play(); }
        public void PlayProgressEffect() { if (FrameEffect != null) FrameEffect.Play(); }
        public void ChangeToFinishedUI() => Refresh();
        public void ChangeToFinalMode() => Refresh();
	}
}
