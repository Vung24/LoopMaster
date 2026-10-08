using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PassSweepingLightAnim : MonoBehaviour
{
    public float MinInterval = 3, MaxInterval = 5, StartPosX = -500, EndPosX = 500;
    private Coroutine _loop;
    private Tween _tween;
    private void OnEnable() => _loop = StartCoroutine(PlayLoop());
    private IEnumerator PlayLoop()
    {
        var image = GetComponent<Image>();
        var rect = transform as RectTransform;
        if (image == null || rect == null) yield break;
        while (true)
        {
            image.enabled = false;
            yield return new WaitForSecondsRealtime(Random.Range(Mathf.Max(.1f, MinInterval), Mathf.Max(MinInterval, MaxInterval)));
            image.enabled = true;
            rect.anchoredPosition = new Vector2(StartPosX, rect.anchoredPosition.y);
            _tween = rect.DOAnchorPosX(EndPosX, .75f).SetEase(Ease.Linear).SetUpdate(true).SetLink(gameObject);
            yield return new WaitForSecondsRealtime(.75f);
        }
    }
    private void OnDisable() { if (_loop != null) StopCoroutine(_loop); _loop = null; _tween?.Kill(); }
}
