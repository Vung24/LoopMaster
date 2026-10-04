using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Snui;
using Cysharp.Threading.Tasks;

public class SpecialStreakCell : MonoBehaviour
{
    [SerializeField] private Transform _lineUp, _lineDown, _lockNode, _completeNode, _grandTitle;
    [SerializeField] private TMP_Text _labelTex;
    [SerializeField] private RewardCell _rewardCell;
    [SerializeField] private Image _labelIcon;
    [SerializeField] private Sprite _purpleIcon, _greenIcon;
    public void Init(int rowIndex)
    {
        var manager = SpecialStreakManager.Ensure();
        var stage = manager.GetRewardInfo(rowIndex);
        if (stage == null) return;
        bool claimed = rowIndex < manager.ClaimedIndex;
        SpecialStreakPresentation.SetActive(_lineUp, rowIndex > 0);
        SpecialStreakPresentation.SetActive(_lineDown, rowIndex + 1 < manager.GetRewardInfosConfig().Count);
        SpecialStreakPresentation.SetActive(_completeNode, claimed);
        SpecialStreakPresentation.SetActive(_lockNode, rowIndex > manager.ClaimedIndex);
        SpecialStreakPresentation.SetActive(_grandTitle, rowIndex + 1 == manager.GetRewardInfosConfig().Count);
        if (_labelTex != null) _labelTex.text = stage.Goal.ToString();
        if (_labelIcon != null) _labelIcon.sprite = claimed ? _greenIcon : _purpleIcon;
        if (_rewardCell != null) _rewardCell.Init(stage.Items()[0], true, false).Forget();
    }
}
