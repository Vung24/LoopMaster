using Cysharp.Threading.Tasks;
using Spine.Unity;
using UnityEngine;

public class SweepBreathBtnAnim : MonoBehaviour
{
	public SkeletonGraphic SweepSpine;
	public BreathAnim BreathAnim;

	private void Start() => PlayAnim().Forget();

	private async UniTask PlayAnim()
	{
		if (SweepSpine != null)
		{
			SweepSpine.gameObject.SetActive(true);
			SweepSpine.Initialize(false);
		}
		await UniTask.Delay(250, cancellationToken: this.GetCancellationTokenOnDestroy());
		if (BreathAnim != null) BreathAnim.Play(0f, 3f);
	}
}
