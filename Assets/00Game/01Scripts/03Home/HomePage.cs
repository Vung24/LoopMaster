using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HomePage : MonoBehaviour
{
    [SerializeField] private Button _playButton;
    [SerializeField] private TMP_Text _levelLabel;
    private void Awake()
    {
        if (_playButton == null)
            foreach (Button button in GetComponentsInChildren<Button>(true))
                if (button.name == "PlayBtn") { _playButton = button; break; }
        if (_playButton != null)
        {
            var play = _playButton.GetComponent<PlayButton>();
            if (play != null) play.Bind(this);
            else _playButton.onClick.AddListener(OnClickPlay);
        }
    }
    private void OnEnable()
    {
        RefreshProgress();
    }
    private void Start()
    {
        Controller().Initialize();
        PrepareOfflinePage();
    }
    private HomeController Controller()
    {
        HomeController controller = HomeController.ForScene(gameObject.scene);
        if (controller == null) controller = gameObject.AddComponent<HomeController>();
        if (controller.HomePage == null) controller.HomePage = this;
        return controller;
    }
    private void PrepareOfflinePage()
    {
        foreach (Transform node in GetComponentsInChildren<Transform>(true))
            if (node.name == "UnlimitedTip" || node.name == "HomeEvents")
                node.gameObject.SetActive(false);
    }
    public void RefreshProgress()
    {
        var catalog = Resources.Load<BuiltinLevelCatalog>(BuiltinLevelCatalog.ResourceName);
        bool complete = GameProgressStore.IsCampaignComplete;
        bool available = !complete && catalog != null && catalog.GetLevel(GameProgressStore.NextPlayableLevel) != null;
        if (_playButton != null) _playButton.interactable = available;
        string level = complete ? "ALL LEVELS COMPLETE" : available ? GameProgressStore.NextPlayableLevel.ToString() : "LEVEL DATA MISSING";
        if (_levelLabel != null) _levelLabel.text = level;
        else if (_playButton != null)
            foreach (TMP_Text label in _playButton.GetComponentsInChildren<TMP_Text>(true))
                if (int.TryParse(label.text, out _) || label.name == "LevelText") { label.text = level; label.enableAutoSizing = true; label.fontSizeMin = 18; }
                else if (label.text.StartsWith("LEVEL ", System.StringComparison.OrdinalIgnoreCase)) label.text = "LEVEL " + level;
    }
    public void OnClickPlay()
    {
        if (!isActiveAndEnabled) return;
        Controller().TryEnterGame();
    }
	public bool IsSkipAnimationsBlocked() => false;
	public bool CommitProgressBeforeEnterGame() => true;

    private void OnDestroy()
    {
        if (_playButton != null) _playButton.onClick.RemoveListener(OnClickPlay);
    }
}

