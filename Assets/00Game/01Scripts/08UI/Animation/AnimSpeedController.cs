using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class AnimSpeedController
{
    private readonly List<Tween> _tweens = new List<Tween>();
    private UniTaskCompletionSource _speedUpSource;

    public float Speed { get; private set; } = 1f;

    public AnimSpeedController()
    {
        Speed = 1f;
    }

    public void Reset()
    {
        Speed = 1f;
        _speedUpSource = null;
        _tweens.Clear();
    }

    public void SpeedUp(float speed)
    {
        Speed = speed;
        for (int i = _tweens.Count - 1; i >= 0; i--)
        {
            var tween = _tweens[i];
            if (tween != null && tween.IsActive())
            {
                tween.timeScale = speed;
            }
        }
        _speedUpSource?.TrySetResult();
    }

    public T Track<T>(T tween) where T : Tween
    {
        if (tween == null) return null;
        tween.timeScale = Speed;
        _tweens.Add(tween);
        tween.OnComplete(() => _tweens.Remove(tween));
        tween.OnKill(() => _tweens.Remove(tween));
        return tween;
    }

    public async UniTask WaitForSeconds(float seconds, CancellationToken cancellationToken = default)
    {
        float remaining = seconds;
        while (remaining > 0f)
        {
            if (cancellationToken.IsCancellationRequested) return;

            float currentSpeed = Mathf.Max(0.001f, Speed);
            float waitDuration = remaining / currentSpeed;

            _speedUpSource = new UniTaskCompletionSource();
            float startRealTime = Time.realtimeSinceStartup;

            var delayTask = UniTask.Delay(TimeSpan.FromSeconds(waitDuration), ignoreTimeScale: false, cancellationToken: cancellationToken);
            var winner = await UniTask.WhenAny(delayTask, _speedUpSource.Task);

            float elapsed = (Time.realtimeSinceStartup - startRealTime) * currentSpeed;
            remaining -= elapsed;

            if (winner == 0)
            {
                break;
            }
        }
    }
}