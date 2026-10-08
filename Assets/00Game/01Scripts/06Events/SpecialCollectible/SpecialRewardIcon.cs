using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui.SpecialCol
{
	public class SpecialRewardIcon : MonoBehaviour
	{
		[SerializeField]
		private Image _icon;
		[SerializeField]
		private TMP_Text _countText;
		[SerializeField]
		private ParticleSystem _particle;
		[SerializeField]
		private ParticleSystem _doubleParticle;
		[SerializeField]
		private GameObject _doubleBadge;

		private int _currentCount;

		private void Awake()
		{
			if (_icon == null) _icon = GetComponentInChildren<Image>(true);
			if (_countText == null) _countText = GetComponentInChildren<TMP_Text>(true);
		}

		public void Setup(int count, Sprite sprite, bool isDouble = false)
		{
			_currentCount = count;
			if (_icon != null && sprite != null)
			{
				_icon.sprite = sprite;
			}
			if (_countText != null)
			{
				_countText.text = count > 1 ? $"x{count}" : string.Empty;
			}
			if (_doubleBadge != null)
			{
				_doubleBadge.SetActive(isDouble);
			}
		}

		public void SetCount(int count)
		{
			_currentCount = count;
			if (_countText != null)
			{
				_countText.text = count > 1 ? $"x{count}" : string.Empty;
			}
		}

		public void UpdateCount(int count)
		{
			SetCount(count);
			PlayShakeAnimation();
		}

		public async UniTask PlayEnterAnim()
		{
			transform.localScale = Vector3.zero;
			await transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetLink(gameObject).ToUniTask();
		}

		public async UniTask PlaySingleAnim(Vector3 startPos)
		{
			transform.position = startPos;
			transform.localScale = Vector3.one * 1.5f;
			if (_particle != null) _particle.Play();

			var seq = DOTween.Sequence();
			seq.Append(transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack));
			seq.SetLink(gameObject);
			await seq.ToUniTask();
		}

		public async UniTask PlayDoubleAnim(Vector3 startPos)
		{
			PlayDoubleParticle();
			await PlaySingleAnim(startPos);
		}

		public void PlayDoubleParticle()
		{
			if (_doubleParticle != null)
			{
				_doubleParticle.Play();
			}
		}

		public void PlayShakeAnimation()
		{
			transform.DOComplete();
			transform.DOShakeScale(0.3f, 0.2f, 10, 90f).SetLink(gameObject);
		}
	}
}