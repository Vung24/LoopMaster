using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class HomeIconTextBar : MonoBehaviour
	{
		public enum Style
		{
			Timer,
			Start,
			Claim,
			NoAd
		}

		[SerializeField] private Image _bg;
		[SerializeField] private TMP_Text _text;
		[SerializeField] private CountdownHelper _countdownHelper;
		[SerializeField] private Style _style;

		public CountdownHelper CountdownHelper => _countdownHelper;

		private void Awake()
		{
			if (_bg == null)
			{
				_bg = GetComponent<Image>();
			}

			if (_text == null)
			{
				_text = GetComponentInChildren<TMP_Text>(true);
			}

			if (_countdownHelper == null)
			{
				_countdownHelper = GetComponent<CountdownHelper>();
				if (_countdownHelper == null)
				{
					_countdownHelper = gameObject.AddComponent<CountdownHelper>();
				}
			}

			if (_countdownHelper != null)
			{
				_countdownHelper.OnTimerTick.AddListener(OnTimeTicked);
			}
		}

		public void SetText(string text)
		{
			if (_text != null)
			{
				_text.text = text;
			}
		}

		public void SetStyle(Style style)
		{
			_style = style;
		}

		public void CountdownUntil(long timestamp)
		{
			if (_countdownHelper != null)
			{
				_countdownHelper.CountdownUntil(timestamp);
			}
		}

		public void CountdownFixTime(long fixTime)
		{
			if (_countdownHelper != null)
			{
				_countdownHelper.CountdownFixTime(fixTime);
			}
		}

		public void AddOnTimerStopListener(UnityEngine.Events.UnityAction action)
		{
			if (_countdownHelper != null && action != null)
			{
				_countdownHelper.OnTimerStoped.AddListener(action);
			}
		}

		public void RemoveOnTimerStopListener(UnityEngine.Events.UnityAction action)
		{
			if (_countdownHelper != null && action != null)
			{
				_countdownHelper.OnTimerStoped.RemoveListener(action);
			}
		}

		private void OnTimeTicked(long time)
		{
			if (_text != null)
			{
				_text.text = TimeFormatter.FormatTimeLeft(time);
			}
		}

		private void OnDestroy()
		{
			if (_countdownHelper != null)
			{
				_countdownHelper.OnTimerTick.RemoveListener(OnTimeTicked);
			}
		}
	}
}