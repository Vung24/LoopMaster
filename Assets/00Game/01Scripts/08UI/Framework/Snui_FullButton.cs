using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	[RequireComponent(typeof(Button))]
	[RequireComponent(typeof(CanvasGroup))]
	public class Snui_FullButton : MonoBehaviour
	{
		public bool EnableClickAudio = true;

		[SerializeField] private Button _button;
		[SerializeField] private CanvasGroup _canvasGroup;
		[SerializeField] private Transform _content;

		public Button Button => _button ??= GetComponent<Button>();
		public CanvasGroup CanvasGroup => _canvasGroup ??= GetComponent<CanvasGroup>();
		public Transform Content => _content ??= HierarchyReferenceCache.Child(transform, "Content") ?? transform;

		private void Awake()
		{
			if (_button == null) _button = GetComponent<Button>();
			if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
			if (_content == null) _content = HierarchyReferenceCache.Child(transform, "Content") ?? transform;

			if (_button != null)
			{
				_button.onClick.AddListener(PlayClickButtonSound);
			}
		}

		private void PlayClickButtonSound()
		{
			if (!EnableClickAudio || !GameAudioSettings.EffectsEnabled) return;
			if (GameAudioPlayer.Current != null)
			{
				GameAudioPlayer.Current.PlayClickAudio();
			}
			else
			{
				AudioClip clip = GameAssetsConfig.Instance?.ButtonClickAudio;
				if (clip != null)
				{
					AudioSource source = GetComponent<AudioSource>();
					if (source == null)
					{
						source = gameObject.AddComponent<AudioSource>();
						source.playOnAwake = false;
					}
					source.PlayOneShot(clip);
				}
			}
		}

		public void DisableButton()
		{
			if (Button != null)
			{
				Button.interactable = false;
			}
			if (CanvasGroup != null)
			{
				CanvasGroup.alpha = 0.5f;
			}
		}

		public void EnableButton()
		{
			if (Button != null)
			{
				Button.interactable = true;
			}
			if (CanvasGroup != null)
			{
				CanvasGroup.alpha = 1f;
			}
		}
	}
}