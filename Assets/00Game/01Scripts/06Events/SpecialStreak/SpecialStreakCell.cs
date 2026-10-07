using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Snui;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

public class SpecialStreakCell : MonoBehaviour
{
    [SerializeField] private Transform _lineUp, _lineDown, _lockNode, _completeNode, _grandTitle;
    [SerializeField] private TMP_Text _labelTex;
    [SerializeField] private RewardCell _rewardCell;
    [SerializeField] private Image _labelIcon;
    [SerializeField] private Sprite _purpleIcon, _greenIcon;
    private readonly List<Material> _materials = new List<Material>();
    private void Awake() => SpecialStreakPresentation.Prepare(transform, _materials);
    private void OnDestroy() => SpecialStreakPresentation.Cleanup(_materials);
    public void Init(int rowIndex)
    {
        Snui_Text.EnsureFontAssets(transform);
        foreach (var graphic in GetComponentsInChildren<MaskableGraphic>(true)) graphic.maskable = true;
        var manager = SpecialStreakManager.Ensure();
        var rewards = manager.GetRewardInfosConfig();
        if (rewards == null || rewards.Count == 0) return;

        int totalCount = rewards.Count;
        int stageIndex = totalCount - 1 - rowIndex;
        if (stageIndex < 0 || stageIndex >= totalCount) return;

        var stage = rewards[stageIndex];
        if (stage == null) return;

        bool claimed = stageIndex < manager.ClaimedIndex;
        bool isLocked = stageIndex > manager.ClaimedIndex;

        SpecialStreakPresentation.SetActive(_lineUp, rowIndex > 0);
        SpecialStreakPresentation.SetActive(_lineDown, rowIndex < totalCount - 1);
        SpecialStreakPresentation.SetActive(_completeNode, claimed);
        SpecialStreakPresentation.SetActive(_lockNode, isLocked);
        SpecialStreakPresentation.SetActive(_grandTitle, rowIndex == 0);

        if (_labelTex != null) _labelTex.text = (totalCount - rowIndex).ToString();
        if (_labelIcon != null) _labelIcon.sprite = claimed ? _greenIcon : _purpleIcon;
        if (_rewardCell != null && stage.Items().Count > 0)
        {
            _rewardCell.Init(stage.Items()[0], true, true).Forget();
        }
    }
}
