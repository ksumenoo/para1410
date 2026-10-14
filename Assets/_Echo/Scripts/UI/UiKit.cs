#if ECHO_UGUI
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Echo.UI
{
    // Мелкие помощники для сборки uGUI из кода: мультяшный стиль — плоские цвета и тёмный контур.
    public static class UiKit
    {
        public static readonly Color Ink = new Color(0.06f, 0.06f, 0.1f, 1f);
        public static readonly Color Panel = new Color(0.13f, 0.14f, 0.24f, 0.88f);
        public static readonly Color PanelDark = new Color(0.07f, 0.07f, 0.12f, 0.94f);
        public static readonly Color Slot = new Color(0.2f, 0.22f, 0.36f, 0.9f);
        public static readonly Color Accent = new Color(1f, 0.82f, 0.25f, 1f);
        public static readonly Color Teal = new Color(0.3f, 0.9f, 0.8f, 1f);
        public static readonly Color Danger = new Color(1f, 0.4f, 0.3f, 1f);
        public static readonly Color Soft = new Color(0.85f, 0.88f, 0.95f, 1f);
        public static readonly Color Button = new Color(0.32f, 0.38f, 0.7f, 1f);
        public static readonly Color ButtonOn = new Color(0.95f, 0.6f, 0.2f, 1f);

        static Font _font;
        static Sprite _white;

        // Встроенный шрифт Unity (Liberation Sans): умеет кириллицу, импортировать ничего не нужно.
        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Sprite White
        {
            get
            {
                if (_white == null)
                {
                    var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                    var px = new Color32[16];
                    for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                    tex.SetPixels32(px);
                    tex.Apply();
                    _white = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
                    _white.hideFlags = HideFlags.HideAndDontSave;
                }
                return _white;
            }
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        // Точечный якорь: anchor — точка на родителе (0..1), pos — смещение от неё, size — размер.
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static Image Box(string name, Transform parent, Color fill, bool outline = true, bool raycast = false)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = White;
            img.color = fill;
            img.raycastTarget = raycast;
            if (outline) AddOutline(rt.gameObject, 2f);
            return img;
        }

        static void AddOutline(GameObject go, float distance)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = Ink;
            o.effectDistance = new Vector2(distance, -distance);
            o.useGraphicAlpha = false;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false, bool outline = true)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            if (outline) AddOutline(rt.gameObject, 1.5f);
            return t;
        }

        public static Button MakeButton(string name, Transform parent, string caption, Color fill, UnityAction onClick, int size = 20)
        {
            var img = Box(name, parent, fill, true, true);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.82f, 0.92f, 1f, 1f);
            colors.pressedColor = new Color(0.65f, 0.65f, 0.7f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            btn.colors = colors;
            var label = Label("Caption", img.transform, caption, size, Color.white, TextAnchor.MiddleCenter, true);
            Stretch(label.rectTransform, 4f);
            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        public static void SetCaption(Button button, string caption)
        {
            var t = button.GetComponentInChildren<Text>();
            if (t != null && t.text != caption) t.text = caption;
        }

        public static InputField MakeInput(string name, Transform parent, string initial, string placeholder, int maxLength)
        {
            var bg = Box(name, parent, new Color(0.95f, 0.96f, 0.99f, 1f), true, true);
            var input = bg.gameObject.AddComponent<InputField>();
            var text = Label("Text", bg.transform, "", 20, Ink, TextAnchor.MiddleLeft, false, false);
            Stretch(text.rectTransform, 8f);
            var hint = Label("Placeholder", bg.transform, placeholder, 18, new Color(0.45f, 0.47f, 0.55f, 1f), TextAnchor.MiddleLeft, false, false);
            hint.fontStyle = FontStyle.Italic;
            Stretch(hint.rectTransform, 8f);
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = maxLength;
            input.text = initial;
            return input;
        }

        public static void Set(Text t, string s)
        {
            if (t != null && t.text != s) t.text = s;
        }

        public static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }
    }
}
#endif
