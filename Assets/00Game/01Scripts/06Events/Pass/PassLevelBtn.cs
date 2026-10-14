using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class PassLevelBtn : PassToastItemBase
	{
        public Sprite ClaimedBg, PassBg;
        [SerializeField] private TMPro.TMP_Text _levelText;
        [SerializeField] private Snui_Text _levelSnuiText;
        [SerializeField] private Image _image;
        private int _stage;
        protected override void Awake()
        {
            base.Awake();
            if (_levelText == null) _levelText = HierarchyReferenceCache.DescendantComponent<TMPro.TMP_Text>(transform, "Level");
            if (_levelText != null && _levelSnuiText == null) _levelSnuiText = _levelText.GetComponent<Snui_Text>();
            if (_image == null) _image = GetComponent<Image>();
            if (_button != null) _button.onClick.AddListener(() =>
                Preview(Managers.Pass.GetStageConfigByIndex(_stage).FreeRewards));
        }
        public void Init(int row)
        {
            _stage = row;
            if (_levelText != null)
            {
                if (_levelSnuiText != null) _levelSnuiText.SetText(row.ToString());
                else _levelText.text = row.ToString();
                _levelText.gameObject.SetActive(row > 0);
            }
            else
            {
                PassUI.Text(transform, "Level", row.ToString());
                PassUI.Active(transform, "Level", row > 0);
            }
            ApplyBackground(Managers.Pass.IsStageUnlocked(row));
        }
        public void ChangeToUnLockStyle() => ApplyBackground(true);
        public void ChangeToLockStyle() => ApplyBackground(false);
        public void SetDisplayStage(int stage) => ApplyBackground(_stage < stage);
        private void ApplyBackground(bool unlocked)
        {
            var image = _image != null ? _image : GetComponent<Image>();
            if (image != null) image.overrideSprite = _stage == 0 ? PassBg : unlocked ? ClaimedBg : null;
        }
	}
}
