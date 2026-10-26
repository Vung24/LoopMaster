using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class BoosterPresentationUI : MonoBehaviour
{
    [SerializeField] private Canvas _overlayCanvas;
    [SerializeField] private Image _flightIcon;
    [SerializeField] private BoosterTutorialGuide _guide;
    [SerializeField] private CanvasGroup _inputGroup;
    [SerializeField] private BoosterUnlockVfx _unlockVfx;

    public BoosterUnlockVfx UnlockVfx => _unlockVfx;

    public int SortingOrder => _overlayCanvas != null ? _overlayCanvas.sortingOrder : 1100;

    /// <summary>Instantiates the editable booster UI prefab under the gameplay canvas.</summary>
    public static BoosterPresentationUI Create(Transform parent)
    {
        GameObject prefab = Resources.Load<GameObject>("BoosterPresentation");
        if (prefab == null)
            throw new InvalidOperationException("Missing Resources/BoosterPresentation.prefab. Check its import errors and .meta GUID.");
        GameObject instance = Instantiate(prefab, parent, false);
        BoosterPresentationUI view = instance.GetComponent<BoosterPresentationUI>();
        if (view == null || !view.HasRequiredReferences())
        {
            instance.SetActive(false);
            Destroy(instance);
            throw new InvalidOperationException("BoosterPresentation.prefab needs BoosterPresentationUI on its root and valid Canvas, FlightIcon and Guide references.");
        }
        Canvas owner = parent.GetComponentInParent<Canvas>();
        view._overlayCanvas.sortingOrder = owner == null ? 1100 : owner.sortingOrder + 10;
        Snui.Snui_Text.EnsureFontAssets(view.transform);
        return view;
    }

    private bool HasRequiredReferences()
    {
        return _overlayCanvas != null && _flightIcon != null && _guide != null && _inputGroup != null;
    }

    public void SetInputBlocked(bool blocked)
    {
        _inputGroup.blocksRaycasts = blocked;
    }

    public Image ShowFlight()
    {
        _flightIcon.gameObject.SetActive(true);
        return _flightIcon;
    }

    public BoosterTutorialGuide ShowGuide()
    {
        _guide.gameObject.SetActive(true);
        return _guide;
    }

    public void Dismiss()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
