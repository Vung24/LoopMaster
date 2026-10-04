using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class FadeInAnim : MonoBehaviour
{
	public bool PlayOnStart;
	public float Duration = 0.3f;
	private CanvasGroup _canvasGroup;

	private void Awake() => _canvasGroup = GetComponent<CanvasGroup>();
	private void Start() { if (PlayOnStart) Play(); }

	public Tween Play()
	{
		if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
		_canvasGroup.DOKill();
		_canvasGroup.alpha = 0f;
		return _canvasGroup.DOFade(1f, Mathf.Max(0.01f, Duration)).SetEase(Ease.OutQuad).SetLink(gameObject);
	}
}
