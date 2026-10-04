using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioPlayer : MonoBehaviour
{
    private readonly Dictionary<string, AudioClip> _cachedAudios = new Dictionary<string, AudioClip>();
    private readonly List<Coroutine> _audioTrack = new List<Coroutine>();
    private AudioClip _cachedLoopAudio;
    private AudioStruct _audio;
    private AudioSource _musicSource;
    private AudioSource _effectsSource;

    public void InitPlayer(AudioStruct audio, AssetBundle bundle)
    {
        StopAudio();
        _audio = audio;
        if (audio == null || bundle == null) return;
        PreloadAudioClips(bundle);
    }

    private void PreloadAudioClips(AssetBundle bundle)
    {
        _cachedLoopAudio = bundle.LoadAsset<AudioClip>("loop");
        if (_audio.AudioId == null) return;
        for (int group = 0; group < _audio.AudioId.Count; group++)
        {
            List<int> ids = _audio.AudioId[group];
            if (ids == null) continue;
            for (int index = 0; index < ids.Count; index++)
            {
                string key = string.Format("group_{0}_{1}", group, index);
                _cachedAudios[key] = bundle.LoadAsset<AudioClip>(key);
            }
        }
    }

    public void PlayAudio()
    {
        StopTracks();
        EnsureSources();
        _musicSource.Stop();
        _musicSource.clip = _cachedLoopAudio;
        _musicSource.loop = true;
        if (_cachedLoopAudio != null) _musicSource.Play();
        PlayGroupRandomAudio();
    }

    public void StopAudio()
    {
        StopTracks();
        if (_musicSource != null) _musicSource.Stop();
        if (_effectsSource != null) _effectsSource.Stop();
        ClearCached();
    }

    private void StopTracks()
    {
        foreach (Coroutine track in _audioTrack)
            if (track != null) StopCoroutine(track);
        _audioTrack.Clear();
    }

    private void ClearCached()
    {
        _cachedAudios.Clear();
        _cachedLoopAudio = null;
    }

    private void EnsureSources()
    {
        if (_musicSource == null)
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            GameAudioSettings.RegisterMusicSource(_musicSource);
        }
        if (_effectsSource == null)
        {
            _effectsSource = gameObject.AddComponent<AudioSource>();
            _effectsSource.playOnAwake = false;
        }
    }

    private void PlayGroupRandomAudio()
    {
        if (_audio == null || _audio.AudioId == null) return;
        for (int group = 0; group < _audio.AudioId.Count; group++)
            if (_audio.AudioId[group] != null && _audio.AudioId[group].Count > 0)
                _audioTrack.Add(StartCoroutine(PlayGroupAudio(group)));
    }

    private IEnumerator PlayGroupAudio(int groupIndex)
    {
        while (true) yield return new WaitForSeconds(Mathf.Max(0.01f, PlayRandomAudio(groupIndex)));
    }

    private float PlayRandomAudio(int groupIndex)
    {
        List<int> group = _audio.AudioId[groupIndex];
        string key = string.Format("group_{0}_{1}", groupIndex, Random.Range(0, group.Count));
        if (!_cachedAudios.TryGetValue(key, out AudioClip clip) || clip == null) return 0.1f;
        float delay = 0f;
        if (_audio.AudioIntervals != null && groupIndex < _audio.AudioIntervals.Count)
        {
            Interval interval = _audio.AudioIntervals[groupIndex];
            if (interval != null) delay = Random.Range(interval.a, interval.b);
        }
        if (GameAudioSettings.EffectsEnabled) _effectsSource.PlayOneShot(clip);
        return delay + clip.length;
    }

    private void OnDisable()
    {
        StopTracks();
        if (_musicSource != null) _musicSource.Stop();
        if (_effectsSource != null) _effectsSource.Stop();
    }

    private void OnDestroy()
    {
        StopAudio();
        GameAudioSettings.UnregisterMusicSource(_musicSource);
    }
}
