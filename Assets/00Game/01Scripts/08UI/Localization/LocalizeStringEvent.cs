using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Snui
{
    [DisallowMultipleComponent]
    public sealed class LocalizeStringEvent : MonoBehaviour
    {
        [Serializable] public sealed class StringEvent : UnityEvent<string> { }
        [SerializeField, FormerlySerializedAs("m_UpdateString")] private StringEvent _onUpdateString = new StringEvent();
        [SerializeField] private string _fallbackText;
        private TMP_Text label;

        public StringEvent OnUpdateString { get { return _onUpdateString; } }

        private void Awake() { label = GetComponent<TMP_Text>(); }
        private void OnEnable() { RefreshString(); }

        public void RefreshString()
        {
            if (label == null) label = GetComponent<TMP_Text>();
            if (label == null) return;
            if (string.IsNullOrEmpty(label.text) && !string.IsNullOrEmpty(_fallbackText))
                UpdateString(_fallbackText);
        }

        public void UpdateString(string value)
        {
            if (label == null) label = GetComponent<TMP_Text>();
            if (label != null) label.text = value ?? string.Empty;
            _onUpdateString?.Invoke(value ?? string.Empty);
        }
    }
}
