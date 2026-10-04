using TMPro;
using UnityEngine;

public sealed class LoadingText : MonoBehaviour
{
    private static readonly string[] AnimFrames = { ".", "..", "..." };
    private const float Interval = 0.5f;
    [SerializeField] private TMP_Text _animText;
    private int _animStage;
    private float _timer;

    private void OnEnable()
    {
        _animStage = 0;
        _timer = 0f;
        if (_animText == null)
            foreach (TMP_Text label in GetComponentsInChildren<TMP_Text>(true))
                if (label.name == "AnimText" || label.name == "SubText")
                { _animText = label; break; }
        RefreshText();
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < Interval) return;
        _timer -= Interval;
        _animStage = (_animStage + 1) % AnimFrames.Length;
        RefreshText();
    }

    private void RefreshText()
    {
        if (_animText == null) return;
        if (_animText.font == null) _animText.font = Snui.RecoveredTextFonts.Resolve();
        _animText.SetText(AnimFrames[_animStage]);
    }
}
