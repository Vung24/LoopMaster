using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
    public class FlyCoin : MonoBehaviour
    {
        [Min(0.001f)] public float _changeImageInterval = 0.01f;
        [SerializeField] protected Sprite[] _sprites;
        private Image _spritesImg;
        private bool _isPlaying;
        private bool _isLoop;
        private int _index;
        private float _time;

        public static UniTask<GameObject> LoadPrefab(Transform parent)
        {
            DataPrefabLibrary library = DataPrefabLibrary.Current;
            if (library == null || library.FlyCoinPrefab == null)
                throw new System.InvalidOperationException("The recovered FlyCoin prefab is not assigned.");
            return UniTask.FromResult(Instantiate(library.FlyCoinPrefab, parent, false));
        }

        public static UniTask<Sprite[]> LoadSprites(GameObject refPrefab)
        {
            FlyCoin coin = refPrefab == null ? null : refPrefab.GetComponent<FlyCoin>();
            Sprite[] sprites = coin != null ? coin._sprites : null;
            if ((sprites == null || sprites.Length == 0) && DataPrefabLibrary.Current != null)
                sprites = DataPrefabLibrary.Current.CoinSprites;
            return UniTask.FromResult(sprites == null ? new Sprite[0] : (Sprite[])sprites.Clone());
        }

        private void Awake()
        {
            _spritesImg = GetComponent<Image>();
            if (_spritesImg != null) _spritesImg.raycastTarget = false;
            if ((_sprites == null || _sprites.Length == 0) && DataPrefabLibrary.Current != null)
                SetSprites(DataPrefabLibrary.Current.CoinSprites);
            else SetSprites(_sprites);
        }

        public void SetSprites(Sprite[] sprites)
        {
            _sprites = sprites ?? new Sprite[0];
            _index = 0;
            _time = 0f;
            _isPlaying = false;
            if (_spritesImg == null) _spritesImg = GetComponent<Image>();
            if (_spritesImg != null && _sprites.Length > 0) _spritesImg.sprite = _sprites[0];
        }

        public void Play(bool isLoop)
        {
            _isLoop = isLoop;
            _index = 0;
            _time = 0f;
            _isPlaying = _sprites != null && _sprites.Length > 0;
            if (_spritesImg != null && _isPlaying) _spritesImg.sprite = _sprites[0];
        }

        private void Update()
        {
            if (!_isPlaying || _spritesImg == null || _sprites == null || _sprites.Length == 0) return;
            float interval = Mathf.Max(0.001f, _changeImageInterval);
            _time += Time.unscaledDeltaTime;
            int steps = Mathf.FloorToInt(_time / interval);
            if (steps == 0) return;
            _time %= interval;
            int next = _index + steps;
            if (_isLoop) _index = next % _sprites.Length;
            else { _index = Mathf.Min(next, _sprites.Length - 1); _isPlaying = next < _sprites.Length; }
            _spritesImg.sprite = _sprites[_index];
        }

        private void OnDisable() { _isPlaying = false; }
    }
}
