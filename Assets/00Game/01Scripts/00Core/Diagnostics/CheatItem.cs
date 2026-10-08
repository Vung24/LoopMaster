using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Snui;

public class CheatItem : MonoBehaviour
{
    public UnityAction OnClick;
    [SerializeField] private Image _icon;
    [SerializeField] private Button _button;
    [SerializeField] private Image _tick;
    [SerializeField] private Image _infinity;

    private void Awake()
    {
        if (_button == null) _button = GetComponent<Button>();
        if (_icon == null) _icon = GetComponent<Image>();
        if (_button != null) _button.onClick.AddListener(Click);
    }

    private void Click() => OnClick?.Invoke();
    public void UpdateIcon(RewardType rewardType)
    {
        UpdateIcon(rewardType?.GetIconPath());
        SetInfinity(rewardType != null && rewardType.IsTimeLimited());
    }

    public void UpdateIcon(string path)
    {
        if (_icon == null) return;
        // The native version loads an addressable sprite, then fades it in.
        _icon.sprite = PrefabLibrary.Current?.ResolveRewardIcon(path);
        if (_icon.sprite == null && !string.IsNullOrEmpty(path))
            _icon.sprite = Resources.Load<Sprite>(System.IO.Path.ChangeExtension(path, null));
        _icon.enabled = _icon.sprite != null;
        _icon.color = Color.white;
    }

    public void SetSelected(bool isSelected) { if (_tick != null) _tick.gameObject.SetActive(isSelected); }
    public void SetInfinity(bool isInfinity) { if (_infinity != null) _infinity.gameObject.SetActive(isInfinity); }
    private void OnDestroy() { if (_button != null) _button.onClick.RemoveListener(Click); }
}
