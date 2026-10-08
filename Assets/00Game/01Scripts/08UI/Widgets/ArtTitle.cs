using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Snui
{
    public class ArtTitle : MonoBehaviour
    {
        private int requestVersion;

        public void SetTitle(string title)
        {
            requestVersion++;
            foreach (TMP_Text label in GetComponentsInChildren<TMP_Text>(true)) label.text = title ?? string.Empty;
            foreach (Text label in GetComponentsInChildren<Text>(true)) label.text = title ?? string.Empty;
        }

        public async UniTaskVoid SetTitleAsync(UniTask<string> titleTask)
        {
            int version = ++requestVersion;
            try
            {
                string value = await titleTask;
                if (this != null && version == requestVersion) SetTitle(value);
            }
            catch (Exception error) { if (this != null) Debug.LogException(error, this); }
        }
    }
}
