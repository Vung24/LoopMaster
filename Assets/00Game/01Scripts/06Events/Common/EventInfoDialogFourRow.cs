using DG.Tweening;
using Snui;
using UnityEngine;
using UnityEngine.UI;

public class EventInfoDialogFourRow : DialogContent
{
	public AudioClip ContentEffect;
	public AudioClip WordsEffect;
	public Transform tf_tutorial_1;
	public Transform tf_TP_tutorial_2;
	public Transform tf_TP_tutorial_3;
	public Transform tf_TP_tutorial_4;
	public Transform tf_TP_tutorial_5;
	public Transform tf_TP_tutorial_6;
	public Transform tf_TP_tutorial_7;
	public Transform tf_TP_tutorial_8;

	[SerializeField]
	private Button _continueBtn;

	protected override void Awake()
	{
		base.Awake();
		if (_continueBtn == null)
		{
			var btnTrans = transform.Find("ContinueBtn");
			if (btnTrans != null)
			{
				_continueBtn = btnTrans.GetComponent<Button>();
			}
			else
			{
				_continueBtn = GetComponentInChildren<Button>(true);
			}
		}

		if (_continueBtn != null)
		{
			_continueBtn.onClick.AddListener(OnClickButton);
		}
	}

	protected override void Start()
	{
		base.Start();
		SetUp();
		PlayAnimation();
	}

	private void OnClickButton()
	{
		Dismiss(RESULT_CLOSE);
	}

	private void SetUp()
	{
	}

	public Tween PlayAnimation()
	{
		return null;
	}
}