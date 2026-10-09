using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelDifficultyPanel : MonoBehaviour
{
	[SerializeField] private Image _bg;
	[SerializeField] private SkeletonGraphic _spine;
	[SerializeField] private TMP_Text _text;
	[SerializeField] private AudioClip _audioClip;
	private bool _isPlaying;

	private void Awake()
	{
		if (_bg == null) _bg = GetComponent<Image>();
		if (_spine == null) _spine = GetComponentInChildren<SkeletonGraphic>(true);
		if (_text == null) _text = GetComponentInChildren<TMP_Text>(true);
		if (_spine != null) _spine.gameObject.SetActive(false);
		if (_text != null) _text.gameObject.SetActive(false);
	}

	public UniTask Play() => Play(destroyCancellationToken);

	public async UniTask Play(CancellationToken cancellationToken)
	{
		if (_isPlaying) return;
		_isPlaying = true;
		using (var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken, destroyCancellationToken))
		{
			Tween textTween = null;
			Tween backgroundTween = null;
			try
			{
				if (GameAudioSettings.EffectsEnabled && _audioClip != null)
				{
					if (GameAudioPlayer.Current != null) GameAudioPlayer.Current.PlayOneShot(_audioClip);
					else AudioSource.PlayClipAtPoint(_audioClip, transform.position);
				}
				if (_spine != null && _spine.skeletonDataAsset != null)
				{
					_spine.gameObject.SetActive(true);
					_spine.Initialize(false);
					var data = _spine.skeletonDataAsset.GetSkeletonData(false);
					var animation = GameUtils.GetSkeletonAnimation(data, "idle");
					if (animation != null && _spine.AnimationState != null)
						_spine.AnimationState.SetAnimation(0, animation, false);
					_spine.Update(0f);
				}
				if (_text != null)
				{
					Snui.Snui_Text.EnsureFontAssets(_text.transform);
					var follower = _text.GetComponentInParent<BoneFollowerGraphic>();
					if (follower != null)
					{
						follower.Initialize();
						follower.LateUpdate();
					}
					Color color = _text.color;
					color.a = 1f;
					_text.color = color;
					_text.gameObject.SetActive(true);
					_text.ForceMeshUpdate();
				}
				await UniTask.WaitForSeconds(1.4f, cancellationToken: lifetime.Token);
				if (_text != null) textTween = _text.DOFade(0f, 0.2f).SetLink(gameObject);
				if (_bg != null)
				{
					backgroundTween = _bg.DOFade(0f, 0.2f).SetLink(gameObject);
					await UniTask.WaitUntil(() => !backgroundTween.IsActive() || backgroundTween.IsComplete(),
						cancellationToken: lifetime.Token);
				}
				else await UniTask.WaitForSeconds(0.2f, cancellationToken: lifetime.Token);
			}
			finally
			{
				if (textTween != null && textTween.IsActive()) textTween.Kill();
				if (backgroundTween != null && backgroundTween.IsActive()) backgroundTween.Kill();
				if (this != null) { gameObject.SetActive(false); Destroy(gameObject); }
			}
		}
	}
}
