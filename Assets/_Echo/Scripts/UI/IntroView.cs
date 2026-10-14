#if ECHO_UGUI
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Echo.UI
{
    // Вступительный экран: лор, управление обоих героев, «Нажмите Enter». Показывается один раз за партию,
    // раньше экрана внешности.
    public class IntroView
    {
        public static bool Active { get; private set; }

        readonly GameObject _root;
        readonly Text _prompt;
        bool _done, _pendingSend;

        public IntroView(Transform parent)
        {
            var dim = UiKit.Box("Intro", parent, new Color(0.03f, 0.03f, 0.07f, 0.96f), false, true);
            UiKit.Stretch(dim.rectTransform);
            _root = dim.gameObject;

            var panel = UiKit.Box("Panel", dim.transform, UiKit.Panel);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060f, 660f));

            var title = UiKit.Label("Title", panel.transform, "NoRIN.Co · Испытание аппарата", 38, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1000f, 52f));

            var lore = UiKit.Label("Lore", panel.transform,
                "Ты — испытуемый в аппарате корпорации NoRIN.Co. Опыт пошёл не так: время раскололо тебя надвое.\n" +
                "Один ты идёт вперёд, в будущее. Другой — назад, в прошлое. У каждого своя временная линия.\n" +
                "Что один сделает в прошлом, то изменит мир другого. Помогайте друг другу, прячьтесь от охраны.\n" +
                "Встретьтесь у реактора, удерживайте «действие» вместе — и поменяйтесь местами.",
                21, Color.white, TextAnchor.UpperCenter);
            UiKit.Place(lore.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(980f, 150f));
            lore.lineSpacing = 1.15f;

            Column(panel.transform, -250f, ControlsInfo.Hero1Title, () => ControlsInfo.Hero1, "H1", new Color(0.55f, 0.85f, 1f, 1f));
            Column(panel.transform, 250f, ControlsInfo.Hero2Title, () => ControlsInfo.Hero2, "H2", new Color(1f, 0.7f, 0.5f, 1f));

            var extra = UiKit.Label("Extra", panel.transform, ControlsInfo.Radio + "\n" + ControlsInfo.System, 17, UiKit.Soft, TextAnchor.UpperCenter);
            UiKit.Place(extra.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(980f, 50f));

            _prompt = UiKit.Label("Prompt", panel.transform, "Нажмите Enter или кнопку, чтобы начать", 22, UiKit.Teal, TextAnchor.MiddleCenter, true);
            UiKit.Place(_prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(700f, 30f));
            var start = UiKit.MakeButton("Start", panel.transform, "Начать", UiKit.ButtonOn, Close, 24);
            UiKit.Place(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(260f, 46f));

            _root.SetActive(false);
        }

        readonly System.Collections.Generic.List<(Text text, System.Func<string> get)> _dynamic =
            new System.Collections.Generic.List<(Text, System.Func<string>)>();

        void Column(Transform parent, float x, string head, System.Func<string> body, string id, Color headColor)
        {
            var box = UiKit.Box("Col" + id, parent, UiKit.PanelDark);
            UiKit.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -35f), new Vector2(480f, 270f));
            var h = UiKit.Label("Head", box.transform, head, 24, headColor, TextAnchor.MiddleCenter, true);
            UiKit.Place(h.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(460f, 34f));
            var t = UiKit.Label("Body", box.transform, "", 17, Color.white, TextAnchor.UpperLeft);
            UiKit.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(440f, 210f));
            t.lineSpacing = 1.1f;
            _dynamic.Add((t, body));
        }

        void Close()
        {
            _done = true;
            _pendingSend = true;
            Echo.Audio.AudioManager.Play(Echo.Audio.Sfx.Click, 0.6f);
        }

        public void Tick(bool inGame)
        {
            if (!inGame) { _done = false; _pendingSend = false; }
            // «Начать» ушло серверу: враги оживают, когда нажали оба игрока. Повторяем, пока синглтон не появится.
            if (_pendingSend && AppearanceState.I != null && AppearanceState.I.SendStart()) _pendingSend = false;
            bool show = inGame && !_done && !UiSettings.SkipIntro;
            Active = show;
            UiKit.SetActive(_root, show);
            if (!show) return;

            foreach (var d in _dynamic) UiKit.Set(d.text, d.get());
            var c = _prompt.color;
            c.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f);
            _prompt.color = c;

            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Close();
        }
    }
}
#endif
