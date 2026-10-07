using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class LoadingPanel : MonoBehaviour
{
	public static LoadingPanel Current;
	protected CanvasGroup _canvasGroup;
	private float _showStartTime;

	public static async UniTask TryDismiss()
	{
		if (Current != null)
		{
			await Current.Dismiss();
		}
	}

	protected virtual void Awake()
	{
		Current = this;
		_canvasGroup = GetComponent<CanvasGroup>();
		if (_canvasGroup == null)
		{
			_canvasGroup = gameObject.AddComponent<CanvasGroup>();
		}
		DontDestroyOnLoad(gameObject);
	}

	protected virtual float GetMinLoadingTime()
	{
		return 0f;
	}

	public async UniTask Show()
	{
		_showStartTime = Time.realtimeSinceStartup;
		await PlayShowAnim();
	}

	public async UniTask Dismiss()
	{
		float minTime = GetMinLoadingTime();
		float elapsed = Time.realtimeSinceStartup - _showStartTime;
		if (elapsed < minTime)
		{
			await UniTask.WaitForSeconds(minTime - elapsed, ignoreTimeScale: false);
		}
		await PlayDismissAnim();
	}

	protected virtual async UniTask PlayShowAnim()
	{
		if (_canvasGroup != null)
		{
			_canvasGroup.alpha = 0f;
			await _canvasGroup.DOFade(1f, 0.3f).SetLink(gameObject).AsyncWaitForCompletion();
		}
	}

	protected virtual async UniTask PlayDismissAnim()
	{
		if (_canvasGroup != null)
		{
			await _canvasGroup.DOFade(0f, 0.3f).SetLink(gameObject).AsyncWaitForCompletion();
		}
		if (Current == this)
		{
			Current = null;
		}
		Destroy(gameObject);
	}

	protected virtual void OnDestroy()
	{
		if (Current == this)
		{
			Current = null;
		}
	}
}