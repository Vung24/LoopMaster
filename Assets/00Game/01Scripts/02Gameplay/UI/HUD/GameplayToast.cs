using DG.Tweening;
using TMPro;
using UnityEngine;

public class GameplayToast : MonoBehaviour
{
    private const float ShowCooldown = 2f;
    private const float FadeInDuration = 0.12f;
    private const float FadeOutDuration = 0.18f;
    private static GameplayToast _instance;

    [SerializeField] private TMP_Text _label;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _content;

    private Sequence _animation;
    private Vector2 _initialAnchoredPosition;
    private bool _hasInitialPosition;
    private float _nextAllowedShowTime;

    public static GameplayToast Instance => _instance;

    private void Awake()
    {
        _instance = this;
        CacheComponents();
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }
        gameObject.SetActive(false);
    }

    private void CacheComponents()
    {
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        if (_label == null) _label = GetComponentInChildren<TMP_Text>(true);
        if (_content == null) _content = transform as RectTransform;
        if (!_hasInitialPosition && _content != null)
        {
            _initialAnchoredPosition = _content.anchoredPosition;
            _hasInitialPosition = true;
        }
    }

    public static void Show(string message, float duration = 0.9f, float floatDistance = 45f)
    {
        if (string.IsNullOrEmpty(message)) return;

        GameplayToast toast = _instance;
        if (toast == null && UICanvas.Current != null)
        {
            toast = UICanvas.Current.Toast;
        }

        if (toast != null)
        {
            toast.PlayToast(message, duration, floatDistance);
        }
    }

    public void PlayToast(string message, float duration, float floatDistance)
    {
        if (string.IsNullOrEmpty(message) || Time.unscaledTime < _nextAllowedShowTime) return;

        CacheComponents();
        if (_label == null || _canvasGroup == null) return;

        // Suppressed clicks must not restart the animation or extend its lifetime.
        _nextAllowedShowTime = Time.unscaledTime + ShowCooldown;

        _animation?.Kill();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        _label.text = message;
        if (_content != null)
        {
            _content.anchoredPosition = _initialAnchoredPosition;
        }
        transform.localScale = Vector3.one * 0.85f;
        _canvasGroup.alpha = 0f;

        duration = Mathf.Max(FadeInDuration + FadeOutDuration, duration);

        _animation = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        _animation.Append(transform.DOScale(1f, FadeInDuration).SetEase(Ease.OutBack));
        _animation.Join(_canvasGroup.DOFade(1f, FadeInDuration));
        _animation.AppendInterval(duration - FadeInDuration - FadeOutDuration);
        if (_content != null)
        {
            _animation.Append(_content.DOBlendableLocalMoveBy(Vector3.up * floatDistance, FadeOutDuration).SetEase(Ease.OutQuad));
            _animation.Join(_canvasGroup.DOFade(0f, FadeOutDuration));
        }
        else
        {
            _animation.Append(_canvasGroup.DOFade(0f, FadeOutDuration));
        }
        _animation.OnComplete(OnAnimationComplete);
    }

    private void OnAnimationComplete()
    {
        if (gameObject != null) gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        _animation?.Kill();
        _animation = null;
    }

    private void OnDestroy()
    {
        _animation?.Kill();
        _animation = null;
        if (_instance == this) _instance = null;
    }
}
