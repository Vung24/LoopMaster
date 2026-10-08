using System.Threading;
using Cysharp.Threading.Tasks;
using Spine;

public static class SpineExtension
{
	public static async UniTask AsyncWaitForCompletion(this TrackEntry trackEntry, CancellationToken cancellationToken = default)
	{
		if (trackEntry == null) return;
		float duration = trackEntry.AnimationEnd - trackEntry.AnimationStart;
		if (duration <= 0f && trackEntry.Animation != null)
		{
			duration = trackEntry.Animation.Duration;
		}

		if (duration > 0f)
		{
			await UniTask.WaitForSeconds(duration, cancellationToken: cancellationToken);
		}
	}
}

