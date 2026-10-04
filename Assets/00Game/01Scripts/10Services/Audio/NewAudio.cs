using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class NewAudio : MonoBehaviour
{
    private AudioSource _audioSource;
    private AudioManager _manager;
    public AudioSource AudioSource => _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        RegisterSource();
    }

    private void Start() => RegisterSource();

    private void RegisterSource()
    {
        if (_manager != null) return;
        _manager = AudioManager.Current;
        if (_manager != null) _manager.AddGameObjAudio(gameObject, _audioSource);
    }

    private void OnDestroy()
    {
        if (_manager != null) _manager.RemoveGameObjAudio(gameObject);
    }
}
