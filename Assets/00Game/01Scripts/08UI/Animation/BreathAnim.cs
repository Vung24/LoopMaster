using DG.Tweening;
using UnityEngine;

public class BreathAnim : MonoBehaviour
{
	public float MaxScale = 1.08f;
	private Vector3 _originScale;
	private Sequence sequence;

	public void Play(float delay = 0f, float interval = 3f)
	{
		Stop();
		_originScale = transform.localScale;
		float half = Mathf.Max(0.05f, interval * 0.5f);
		sequence = DOTween.Sequence()
			.SetDelay(Mathf.Max(0f, delay))
			.Append(transform.DOScale(_originScale * Mathf.Max(1f, MaxScale), half).SetEase(Ease.InOutSine))
			.Append(transform.DOScale(_originScale, half).SetEase(Ease.InOutSine))
			.SetLoops(-1, LoopType.Restart)
			.SetLink(gameObject);
	}

	public void Stop()
	{
		if (sequence != null && sequence.IsActive()) sequence.Kill();
		sequence = null;
		if (_originScale != Vector3.zero) transform.localScale = _originScale;
	}

	public bool IsPlaying() => sequence != null && sequence.IsActive() && sequence.IsPlaying();
	private void OnDisable() => Stop();
}
