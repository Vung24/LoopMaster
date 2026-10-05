using DG.Tweening;
using Snui;
using UnityEngine;
using UnityEngine.UI;

public class EventInfoDialog : DialogContent
{
	public AudioClip ContentEffect;
	public AudioClip WordsEffect;
	public Transform tf_bone1;
	public Transform tf_bone2;
	public Transform tf_bone3;
	public Transform tf_bone4;
	public Transform tf_bone5;
	public Transform tf_bone6;
	public Transform tf_bone7;
	public Transform tf_bone8;
	public Transform tf_bone9;
	public Transform BtnText;

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