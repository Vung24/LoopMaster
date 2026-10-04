using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Snui
{
	public class Steppers : MonoBehaviour
	{
		public UnityAction OnMaxValueExceeded;
		public UnityAction OnMinValueExceeded;
		public int StepCount = 1;
		public float Ping = 1f;
		public int MaxCount = 100;
		public int MinCount;

		[SerializeField] private Snui_Button _decreaseBtn;
		[SerializeField] private Snui_Button _increaseBtn;
		[SerializeField] private TMP_Text _text;
		private int _number;
		private bool _hasNoticeExceeded;
		private Coroutine _coroutine;

		private void Awake()
		{
			if (_decreaseBtn == null)
			{
				Transform dec = transform.Find("DecreaseButton");
				if (dec != null) _decreaseBtn = dec.GetComponent<Snui_Button>();
			}
			if (_increaseBtn == null)
			{
				Transform inc = transform.Find("IncreaseButton");
				if (inc != null) _increaseBtn = inc.GetComponent<Snui_Button>();
			}
			if (_text == null)
			{
				Transform t = transform.Find("Text");
				if (t != null) _text = t.GetComponent<TMP_Text>();
				if (_text == null) _text = GetComponentInChildren<TMP_Text>();
			}
		}

		private void Start()
		{
			if (_decreaseBtn != null)
			{
				_decreaseBtn.OnPointerDownEvent.AddListener(OnDecreaseButtonDown);
				_decreaseBtn.OnPointerUpEvent.AddListener(OnDecreaseButtonUp);
			}
			if (_increaseBtn != null)
			{
				_increaseBtn.OnPointerDownEvent.AddListener(OnIncreaseButtonDown);
				_increaseBtn.OnPointerUpEvent.AddListener(OnIncreaseButtonUp);
			}
			SetNumber(_number);
		}

		public void SetNumber(int num)
		{
			if (MaxCount < MinCount)
			{
				MaxCount = MinCount;
			}
			_number = Mathf.Clamp(num, MinCount, MaxCount);
			if (_text != null)
			{
				_text.text = _number.ToString();
			}
		}

		public int GetNumber()
		{
			return _number;
		}

		private void OnDecreaseButtonDown()
		{
			Invoke(nameof(LongPreaseDecreaseButton), Ping);
		}

		private void OnDecreaseButtonUp()
		{
			CancelInvoke(nameof(LongPreaseDecreaseButton));
			StopNumberCoroutine();
			int next = _number - StepCount;
			if (next < MinCount && !_hasNoticeExceeded)
			{
				OnMinValueExceeded?.Invoke();
			}
			_hasNoticeExceeded = false;
			SetNumber(next);
		}

		private void OnIncreaseButtonDown()
		{
			Invoke(nameof(LongPreaseIncreaseButton), Ping);
		}

		private void OnIncreaseButtonUp()
		{
			CancelInvoke(nameof(LongPreaseIncreaseButton));
			StopNumberCoroutine();
			int next = _number + StepCount;
			if (next > MaxCount && !_hasNoticeExceeded)
			{
				OnMaxValueExceeded?.Invoke();
			}
			_hasNoticeExceeded = false;
			SetNumber(next);
		}

		private void LongPreaseDecreaseButton()
		{
			StopNumberCoroutine();
			_coroutine = StartCoroutine(IEChangeNumber(-StepCount, 0.1f));
		}

		private void LongPreaseIncreaseButton()
		{
			StopNumberCoroutine();
			_coroutine = StartCoroutine(IEChangeNumber(StepCount, 0.1f));
		}

		private void StopNumberCoroutine()
		{
			if (_coroutine != null)
			{
				StopCoroutine(_coroutine);
				_coroutine = null;
			}
		}

		private IEnumerator IEChangeNumber(int step, float interval = 0.1f)
		{
			var wait = new WaitForSeconds(interval);
			while (true)
			{
				if (step > 0)
				{
					if (_number + step > MaxCount && !_hasNoticeExceeded)
					{
						OnMaxValueExceeded?.Invoke();
						_hasNoticeExceeded = true;
					}
				}
				else if (step < 0)
				{
					if (_number + step < MinCount && !_hasNoticeExceeded)
					{
						OnMinValueExceeded?.Invoke();
						_hasNoticeExceeded = true;
					}
				}
				SetNumber(_number + step);
				yield return wait;
			}
		}

		private void OnDestroy()
		{
			CancelInvoke();
			StopNumberCoroutine();
			if (_decreaseBtn != null)
			{
				_decreaseBtn.OnPointerDownEvent.RemoveListener(OnDecreaseButtonDown);
				_decreaseBtn.OnPointerUpEvent.RemoveListener(OnDecreaseButtonUp);
			}
			if (_increaseBtn != null)
			{
				_increaseBtn.OnPointerDownEvent.RemoveListener(OnIncreaseButtonDown);
				_increaseBtn.OnPointerUpEvent.RemoveListener(OnIncreaseButtonUp);
			}
		}
	}
}