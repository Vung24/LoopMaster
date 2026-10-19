using Cysharp.Threading.Tasks;

public class SplashLoadingPanel : LoadingPanel
{
	protected override UniTask PlayShowAnim()
	{
		if (_canvasGroup != null) _canvasGroup.alpha = 1f;
		return UniTask.CompletedTask;
	}
}
