using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

namespace Snui
{
	public class CheatItemSelector : MonoBehaviour
	{
        public UnityAction<int> OnSelected;
        [SerializeField] private CheatItem _cheatItemPrefab;
        [SerializeField] private Button _closeBtn;
        [SerializeField] private ScrollRect _scrollRect;
        private bool _closed;

        public static UniTask<CheatItemSelector> Show(Transform parent)
        {
            var prefab = PrefabLibrary.Current?.CheatItemSelectorPrefab;
            if (prefab == null || parent == null) return UniTask.FromResult<CheatItemSelector>(null);
            var selector = Instantiate(prefab, parent, false).GetComponent<CheatItemSelector>();
            Snui_Text.EnsureFontAssets(selector.transform);
            return UniTask.FromResult(selector);
        }

        private void Awake() { if (_closeBtn != null) _closeBtn.onClick.AddListener(Close); }

        public void Init(RewardType[] rewardTypes, int selectedIndex)
        {
            Clear();
            if (rewardTypes == null) return;
            for (int i = 0; i < rewardTypes.Length; i++)
            {
                var item = CreateItem(i, selectedIndex);
                if (item != null) item.UpdateIcon(rewardTypes[i]);
            }
        }

        public void Init(string[] paths, int selectedIndex)
        {
            Clear();
            if (paths == null) return;
            for (int i = 0; i < paths.Length; i++)
            {
                var item = CreateItem(i, selectedIndex);
                if (item != null) { item.UpdateIcon(paths[i]); item.SetInfinity(false); }
            }
        }

        private CheatItem CreateItem(int index, int selectedIndex)
        {
            if (_scrollRect == null || _scrollRect.content == null || _cheatItemPrefab == null) return null;
            var item = Instantiate(_cheatItemPrefab, _scrollRect.content, false);
            item.SetSelected(index == selectedIndex);
            item.OnClick = () => OnClickCheatItem(index);
            return item;
        }

        private void Clear()
        {
            if (_scrollRect == null || _scrollRect.content == null) return;
            foreach (var item in _scrollRect.content.GetComponentsInChildren<CheatItem>(true))
            { item.gameObject.SetActive(false); Destroy(item.gameObject); }
        }

        private void OnClickCheatItem(int i)
        {
            if (_closed) return;
            _closed = true;
            OnSelected?.Invoke(i);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        public void Close() { _closed = true; gameObject.SetActive(false); Destroy(gameObject); }
	}
}
