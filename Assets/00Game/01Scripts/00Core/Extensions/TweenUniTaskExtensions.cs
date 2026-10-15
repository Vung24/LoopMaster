using Cysharp.Threading.Tasks;
using DG.Tweening;

public static class TweenUniTaskExtensions
{
	public static UniTask ToUniTask(this Tween tween)
	{
		if (tween == null || !tween.IsActive() || tween.IsComplete())
		{
			return UniTask.CompletedTask;
		}

		var utcs = new UniTaskCompletionSource();
		tween.OnComplete(() => utcs.TrySetResult());
		tween.OnKill(() => utcs.TrySetResult());
		return utcs.Task;
	}
}
