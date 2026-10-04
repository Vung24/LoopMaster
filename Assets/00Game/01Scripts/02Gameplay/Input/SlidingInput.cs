using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(InputField))]
public class SlidingInput : MonoBehaviour
{
    private InputField _inputContent;
    private TextGenerator _textGenerator;
    private TextGenerationSettings _settings;
    private RectTransform _rect;
    private float _inputWidth;
    private int _row;
    private float? _oneLineHeight;
    private float _initialY;

    public InputField InputField
    {
        get
        {
            if (_inputContent == null) _inputContent = GetComponent<InputField>();
            return _inputContent;
        }
    }

    private void Awake()
    {
        _inputContent = InputField;
        _textGenerator = new TextGenerator();
        CacheTextSettings();
        _inputContent.onValueChanged.AddListener(OnValueChangeHandler);
        if (!string.IsNullOrEmpty(_inputContent.text)) OnValueChangeHandler(_inputContent.text);
    }

    private bool CacheTextSettings()
    {
        Text text = InputField.textComponent;
        if (text == null) return false;
        if (_rect != text.rectTransform)
        {
            _rect = text.rectTransform;
            _initialY = _rect.anchoredPosition.y;
            _oneLineHeight = null;
        }
        _settings = new TextGenerationSettings
        {
            font = text.font,
            fontSize = text.fontSize,
            fontStyle = text.fontStyle,
            lineSpacing = text.lineSpacing,
            scaleFactor = 1f
        };
        _inputWidth = _rect.rect.width;
        return text.font != null && _inputWidth > 0f;
    }

    private void OnValueChangeHandler(string input)
    {
        if (!CacheTextSettings()) return;
        if (!_oneLineHeight.HasValue)
            _oneLineHeight = LayoutUtility.GetPreferredHeight(_rect) + 2f;

        int rows = 1;
        if (!string.IsNullOrEmpty(input))
        {
            rows = 0;
            foreach (string line in input.Split('\n'))
                rows += string.IsNullOrWhiteSpace(line) ? 1
                    : Mathf.Max(1, Mathf.CeilToInt(_textGenerator.GetPreferredWidth(line, _settings) / _inputWidth));
        }
        _row = rows;
        UpdateContentPos();
    }

    private void UpdateContentPos()
    {
        float lineHeight = _oneLineHeight.GetValueOrDefault();
        _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, lineHeight * Mathf.Max(1, _row));
        Vector2 position = _rect.anchoredPosition;
        // Native shows three rows before sliding the remaining rows upward.
        position.y = _initialY + lineHeight * Mathf.Max(0, _row - 3);
        _rect.anchoredPosition = position;
    }

    private void OnDestroy()
    {
        if (_inputContent != null) _inputContent.onValueChanged.RemoveListener(OnValueChangeHandler);
        (_textGenerator as System.IDisposable)?.Dispose();
    }
}
