using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Snui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MoreHeartDialogContent : DialogContent
{
	public static readonly int RESULT_JUMP_HEART_BANK = 1;
	public static readonly int RESULT_ENTER_GAME = 2;

	private int _refillHeartPrice = 900;
	private JumpHeartDialogSourceScene _scene;

	[SerializeField] private TMP_Text _timeLabel;
	[SerializeField] private TMP_Text _heartNum;
	[SerializeField] private TMP_Text _coinCount;
	[SerializeField] private Button _refillBtn;
	[SerializeField] private Button _rvBtn;
	[SerializeField] private Snui_Button _extraBtn;
	private GameObject _extraBtnEnableHeart;
	private GameObject _extraBtnDisableHeart;
	private GameObject _extraBtnEnableText;
	private GameObject _extraBtnDisableText;
	private bool _isHide;
	private Coroutine _timeCoroutine;

	protected override void Awake()
	{
		base.Awake();
		if (_refillBtn != null)
		{
			_refillBtn.onClick.AddListener(OnRefillClicked);
		}
		if (_rvBtn != null)
		{
			_rvBtn.onClick.AddListener(OnClickRvBtn);
		}
		if (_extraBtn != null)
		{
			_extraBtn.Button?.onClick.AddListener(OnExtraBtnClicked);
		}
	}

	protected override void Start()
	{
		base.Start();
		SetupButtons();
		if (_timeCoroutine != null)
		{
			StopCoroutine(_timeCoroutine);
		}
		_timeCoroutine = StartCoroutine(UpdateTimeLabel());
	}

	private void OnEnable()
	{
		if (_timeCoroutine != null)
		{
			StopCoroutine(_timeCoroutine);
		}
		_timeCoroutine = StartCoroutine(UpdateTimeLabel());
	}

	private void OnDisable()
	{
		if (_timeCoroutine != null)
		{
			StopCoroutine(_timeCoroutine);
			_timeCoroutine = null;
		}
	}

	public static async UniTask<MoreHeartDialogContent> Show(Transform parent, JumpHeartDialogSourceScene sourceScene)
	{
		GameObject prefab = Resources.Load<GameObject>("Economy/Heart/MoreHeartDialogContent");
		if (prefab == null)
		{
			prefab = Resources.Load<GameObject>("Prefab/MoreHeartDialogContent");
		}
		if (prefab != null)
		{
			GameObject go = Instantiate(prefab, parent);
			var content = go.GetComponent<MoreHeartDialogContent>();
			if (content != null)
			{
				content.Init(sourceScene);
				content.Show();
			}
			return content;
		}
		await UniTask.CompletedTask;
		return null;
	}

	public static async UniTask<MoreHeartDialogContent> ShowInDialog(JumpHeartDialogSourceScene source)
	{
		GameObject prefab = Resources.Load<GameObject>("Economy/Heart/MoreHeartDialogContent");
		if (prefab == null)
		{
			prefab = Resources.Load<GameObject>("UI/Dialog/MoreHeartDialogContent");
		}
		if (prefab != null)
		{
			GameObject go = Instantiate(prefab);
			var content = go.GetComponent<MoreHeartDialogContent>();
			if (content != null)
			{
				content.Init(source);
				content.Show();
			}
			return content;
		}
		await UniTask.CompletedTask;
		return null;
	}

	public void Init(JumpHeartDialogSourceScene scene)
	{
		_scene = scene;
		if (Managers.Heart != null)
		{
			_refillHeartPrice = Managers.Heart.GetRefillHeartPrice();
			if (_refillHeartPrice <= 0)
			{
				_refillHeartPrice = 900;
			}
		}

		if (_coinCount != null)
		{
			_coinCount.text = _refillHeartPrice.ToString();
		}

		UpdateRecoverTime(string.Empty);
	}

	private void SetupButtons()
	{
		bool showRv = Managers.Heart != null && Managers.Heart.IsMoreHeartDialogShowRvBtn();
		if (_rvBtn != null)
		{
			_rvBtn.gameObject.SetActive(showRv);
		}

		if (_extraBtn != null)
		{
			bool hasTeam = Managers.Team != null;
			_extraBtn.gameObject.SetActive(hasTeam);
		}
	}

	private IEnumerator UpdateTimeLabel()
	{
		var wait = new WaitForSeconds(1f);
		while (true)
		{
			if (Managers.Heart != null)
			{
				int recoverTime = Managers.Heart.GetRecoverTime();
				if (recoverTime > 0)
				{
					UpdateRecoverTime(TimeFormatter.FormatSeconds(recoverTime));
				}
				else
				{
					UpdateRecoverTime("FULL");
				}
			}
			yield return wait;
		}
	}

	private void UpdateRecoverTime(string timeStr)
	{
		if (_timeLabel != null)
		{
			_timeLabel.text = timeStr;
		}
		if (_heartNum != null && Managers.Heart != null)
		{
			_heartNum.text = $"{Managers.Heart.GetHeartCount()}/{Managers.Heart.MAX_HEART}";
		}
	}

	private void OnCloseClicked()
	{
		Dismiss(RESULT_CLOSE);
	}

	private void OnClickRvBtn()
	{
		if (Managers.Heart != null)
		{
			Managers.Heart.AddHeart();
		}
		Dismiss(RESULT_ENTER_GAME);
	}

	private void OnRefillClicked()
	{
		if (Managers.Heart != null)
		{
			Managers.Heart.FillFullHeart(FillHeartWay.Coin);
		}
		Dismiss(RESULT_ENTER_GAME);
	}

	private void OnExtraBtnClicked()
	{
		if (Managers.Team != null)
		{
			Dismiss(RESULT_JUMP_HEART_BANK);
		}
	}
}