using UnityEngine;

public class BgmSource : MonoBehaviour
{
	private AudioSource _audioSource;

	private void Awake()
	{
		_audioSource = GetComponent<AudioSource>();
		GameAudioSettings.RegisterMusicSource(_audioSource);
	}

	private void OnDestroy()
	{
		GameAudioSettings.UnregisterMusicSource(_audioSource);
	}
}
