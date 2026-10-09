using Cysharp.Threading.Tasks;
using DG.Tweening;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

public class BouncyLavaPlayer : Snui.Lava.LavaPlayer
{
	[SerializeField] private SkeletonGraphic _waterSplashGraphic;
	public AudioClip SplashClip;

	public void DOJumpTo(Vector3 targetPos, float duration = 0.5f, float jumpHeight = 80f, float jumpDuration = 0.25f)
	{
		if (JumpClip != null && GameAudioPlayer.Current != null)
		{
			GameAudioPlayer.Current.PlayOneShot(JumpClip);
		}

		Vector3 localScale = transform.localScale;
		Sequence seq = DOTween.Sequence();
		seq.Append(transform.DOScale(new Vector3(localScale.x * 1.1f, localScale.y * 0.9f, localScale.z), 0.08f).SetEase(Ease.OutSine));
		seq.Append(transform.DOScale(new Vector3(localScale.x * 0.9f, localScale.y * 1.15f, localScale.z), 0.08f).SetEase(Ease.OutSine));
		seq.Append(transform.DOScale(new Vector3(localScale.x * 1.05f, localScale.y * 0.95f, localScale.z), 0.08f).SetEase(Ease.OutSine));
		seq.Append(transform.DOScale(localScale, 0.15f).SetEase(Ease.OutSine));

		Tween jumpTween = GameUtils.DOParabolaJumpTo(transform, targetPos, duration, jumpHeight, jumpDuration);
		seq.Insert(0f, jumpTween);
		seq.SetLink(gameObject);
	}

	public void DOJumpToFail(bool isRightSide, Vector3 targetPos, float duration, float jumpHeight, float jumpDuration, float splashDelay)
	{
		DOJumpTo(targetPos, duration, jumpHeight, jumpDuration);
		DOJumpToFailAsync(isRightSide, splashDelay, duration).Forget();
	}

	private async UniTaskVoid DOJumpToFailAsync(bool isRightSide, float splashDelay, float delay)
	{
		await UniTask.Delay((int)(delay * 1000), cancellationToken: this.GetCancellationTokenOnDestroy());
		if (this != null && gameObject != null)
		{
			DOFallDown(isRightSide, splashDelay);
		}
	}

	public Sequence CreateFallTween(bool isRightSide, Vector3 initialPos, float targetY, float duration, float splashDelay)
	{
		Vector3 localScale = transform.localScale;
		float side = isRightSide ? 1f : -1f;

		Sequence seq = DOTween.Sequence();
		seq.Append(transform.DOLocalRotate(new Vector3(0f, 0f, side * 25f), 0.08f).SetEase(Ease.OutSine));
		seq.Append(transform.DOLocalRotate(new Vector3(0f, 0f, side * -20f), 0.08f).SetEase(Ease.OutSine));
		seq.Append(transform.DOLocalRotate(new Vector3(0f, 0f, side * 15f), 0.08f).SetEase(Ease.OutSine));
		seq.Append(transform.DOLocalRotate(new Vector3(0f, 0f, side * 90f), 0.2f).SetEase(Ease.InSine));

		Sequence moveSeq = DOTween.Sequence();
		moveSeq.AppendInterval(0.1f);
		moveSeq.Append(transform.DOLocalMoveY(targetY - 120f, 0.4f).SetEase(Ease.InQuad));

		Sequence scaleSeq = DOTween.Sequence();
		scaleSeq.AppendInterval(0.15f);
		scaleSeq.Append(transform.DOScale(new Vector3(localScale.x * 0.4f, localScale.y * 0.4f, localScale.z), 0.35f).SetEase(Ease.InSine));

		if (_waterSplashGraphic != null)
		{
			_waterSplashGraphic.gameObject.SetActive(false);
		}

		seq.Insert(0f, moveSeq);
		seq.Insert(0f, scaleSeq);
		seq.InsertCallback(splashDelay, OnWaterSplashEvent);
		seq.SetLink(gameObject);
		return seq;
	}

	public void OnWaterSplashEvent()
	{
		if (SplashClip != null && GameAudioPlayer.Current != null)
		{
			GameAudioPlayer.Current.PlayOneShot(SplashClip);
		}

		if (_waterSplashGraphic != null)
		{
			_waterSplashGraphic.gameObject.SetActive(true);
			_waterSplashGraphic.transform.SetParent(transform.parent, true);
			_waterSplashGraphic.gameObject.AddComponent<LavaSplashLifetime>().Owner = gameObject;
			_waterSplashGraphic.transform.rotation = Quaternion.identity;
			_waterSplashGraphic.Initialize(true);
			_waterSplashGraphic.AnimationState?.SetAnimation(0, "animation", false);
		}

		Transform avatarTrans = HierarchyReferenceCache.Child(transform, "Avatar");
		if (avatarTrans == null) avatarTrans = transform;

		Graphic[] graphics = avatarTrans.GetComponentsInChildren<Graphic>(true);
		foreach (Graphic g in graphics)
		{
			g.DOColor(new Color(g.color.r, g.color.g, g.color.b, 0f), 0.25f)
				.SetEase(Ease.InSine)
				.SetLink(gameObject);
		}
	}

	public void DOFallDown(bool isRightSide, float splashDelay = 0.3f)
	{
		Vector3 pos = transform.localPosition;
		Sequence seq = DOTween.Sequence();
		seq.Append(CreateFallTween(isRightSide, pos, pos.y, 0.5f, splashDelay));
		seq.SetLink(gameObject);
	}
}

public sealed class LavaSplashLifetime : MonoBehaviour
{
	public GameObject Owner;
	private void LateUpdate() { if (Owner == null) Destroy(gameObject); }
}
