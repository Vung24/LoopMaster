using System.Threading;
using Cysharp.Threading.Tasks;
using Snui;
using Snui.Profile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HomePage : MonoBehaviour
{
	[SerializeField]
	private RectTransform _topNode;

	[SerializeField]
	private HomeEventController _eventController;

	[SerializeField]
	private PlayButton _playButton;

	[SerializeField]
	private Button _settingButton;

	[SerializeField]
	private LevelBoxMachine _levelBoxMachine;

	[SerializeField]
	private MyAvatar _myAvatar;

	[SerializeField]
	private AudioClip _homeInAudioClip;

	[SerializeField]
	private GameObject _clickMask;

	public bool IsAnimPlaying;

	private SpecialStreakProgress _specialStreakProgress;
	private CancellationTokenSource _cts;

	public PlayButton PlayButton => _playButton;
	public HomeEventController EventController => _eventController;

	private void Awake()
	{
		_cts = new CancellationTokenSource();

		if (_playButton == null)
		{
			_playButton = GetComponentInChildren<PlayButton>(true);
		}
		if (_playButton != null)
		{
			_playButton.Bind(this);
		}

		if (_eventController == null)
		{
			_eventController = GetComponentInChildren<HomeEventController>(true);
		}

		if (_settingButton == null)
		{
			var btnTrans = HierarchyReferenceCache.Descendant(transform, "SettingBtn");
			if (btnTrans != null)
			{
				_settingButton = btnTrans.GetComponent<Button>();
			}
		}
		if (_settingButton != null)
		{
			_settingButton.onClick.AddListener(OnSettingClick);
		}

		if (_levelBoxMachine == null)
		{
			_levelBoxMachine = GetComponentInChildren<LevelBoxMachine>(true);
		}

		if (_topNode == null)
		{
			var topTrans = HierarchyReferenceCache.Child(transform, "TopNode");
			if (topTrans != null)
			{
				_topNode = topTrans as RectTransform;
			}
		}
	}

	private void OnEnable()
	{
		RefreshProgress();
	}

	private void Start()
	{
		Controller().Initialize();
		StartAsync().Forget();
	}

	private async UniTaskVoid StartAsync()
	{
		if (_homeInAudioClip != null && AudioManager.Current != null)
		{
			AudioManager.Current.PlayOneShot(_homeInAudioClip);
		}

		if (_eventController != null)
		{
			await _eventController.PlayEnterAnimation(false);
		}

		if (_levelBoxMachine != null)
		{
			_levelBoxMachine.Bind(Controller());
		}

		if (_eventController != null)
		{
			await _eventController.ShowPopupDialog();
			await _eventController.PlayIdleAnimation();
		}
	}

	private HomeController Controller()
	{
		HomeController controller = HomeController.ForScene(gameObject.scene);
		if (controller == null) controller = gameObject.AddComponent<HomeController>();
		if (controller.HomePage == null) controller.HomePage = this;
		return controller;
	}

	public void RefreshProgress()
	{
		var catalog = Resources.Load<BuiltinLevelCatalog>(BuiltinLevelCatalog.ResourceName);
		bool complete = GameProgressStore.IsCampaignComplete;
		bool available = !complete && catalog != null && catalog.GetLevel(GameProgressStore.NextPlayableLevel) != null;

		if (_playButton != null)
		{
			var btn = _playButton.GetComponent<Button>();
			if (btn != null) btn.interactable = available;
		}

		string level = complete ? "ALL LEVELS COMPLETE" : available ? GameProgressStore.NextPlayableLevel.ToString() : "LEVEL DATA MISSING";
		if (_playButton != null)
		{
			foreach (TMP_Text label in _playButton.GetComponentsInChildren<TMP_Text>(true))
			{
				if (int.TryParse(label.text, out _) || label.name == "LevelText")
				{
					label.text = level;
					label.enableAutoSizing = true;
					label.fontSizeMin = 18;
				}
				else if (label.text.StartsWith("LEVEL ", System.StringComparison.OrdinalIgnoreCase))
				{
					label.text = "LEVEL " + level;
				}
			}
		}
	}

	public void OnClickPlay()
	{
		if (!isActiveAndEnabled) return;
		Controller().TryEnterGame();
	}

	private void OnSettingClick()
	{
		Controller().OpenSettings();
	}

	public bool IsSkipAnimationsBlocked() => false;

	public bool CommitProgressBeforeEnterGame()
	{
		_eventController?.CommitProgressBeforeEnterGame();
		return true;
	}

	private void OnDestroy()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		if (_settingButton != null)
		{
			_settingButton.onClick.RemoveListener(OnSettingClick);
		}
	}
}

