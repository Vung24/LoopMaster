using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
    [RequireComponent(typeof(Image), typeof(Button))]
    public class CloseButton : MonoBehaviour
    {
        public enum Style { Round, Square }

        [SerializeField] private Sprite _normalSprite;
        [SerializeField] private Sprite _pressedSprite;

        private void Awake()
        {
            Image image = GetComponent<Image>();
            if (image == null) image = gameObject.AddComponent<Image>();
            Button button = GetComponent<Button>();
            if (button == null) button = gameObject.AddComponent<Button>();
            if (image.sprite == null && _normalSprite != null) image.sprite = _normalSprite;
            image.raycastTarget = true;
            if (button.targetGraphic == null) button.targetGraphic = image;
            if (_pressedSprite == null) return;

            SpriteState state = button.spriteState;
            if (state.pressedSprite == null) state.pressedSprite = _pressedSprite;
            button.spriteState = state;
            if (button.transition == Selectable.Transition.None ||
                button.transition == Selectable.Transition.ColorTint)
                button.transition = Selectable.Transition.SpriteSwap;
        }
    }
}
