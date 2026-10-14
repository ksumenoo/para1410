#if ECHO_UGUI
using Echo.Ending;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.UI
{
    // Белая вспышка на весь экран при сопряжении (0,5 с). Событие приходит с сервера через EndingState.
    public class FlashView
    {
        const float Duration = 0.5f;

        readonly GameObject _root;
        readonly Image _image;
        float _start = -10f;

        public FlashView(Transform parent)
        {
            _image = UiKit.Box("Flash", parent, new Color(1f, 1f, 1f, 0f), false, false);
            UiKit.Stretch(_image.rectTransform);
            _root = _image.gameObject;
            _root.SetActive(false);
            EndingState.SwapFlash += Trigger;
        }

        void Trigger() => _start = Time.unscaledTime;

        public void Tick()
        {
            float t = (Time.unscaledTime - _start) / Duration;
            if (t >= 1f)
            {
                UiKit.SetActive(_root, false);
                return;
            }
            UiKit.SetActive(_root, true);
            // Резко вверх за первые 10 %, дальше плавно гаснет.
            float a = t < 0.1f ? t / 0.1f : 1f - (t - 0.1f) / 0.9f;
            _image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a) * 0.95f);
        }
    }
}
#endif
