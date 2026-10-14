#if ECHO_UGUI
using Echo.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.UI
{
    // Пауза по Esc. Игра по сети не останавливается — это просто меню поверх:
    // «Продолжить», «Управление», громкость и «Выйти в меню».
    public class PauseView
    {
        readonly GameObject _root, _main, _controls;
        readonly Text _info, _volume, _h1, _h2;

        public bool Visible => _root.activeSelf;

        public PauseView(Transform parent)
        {
            var dim = UiKit.Box("Pause", parent, new Color(0f, 0f, 0f, 0.55f), false, true);
            UiKit.Stretch(dim.rectTransform);
            _root = dim.gameObject;

            // ---- Главная страница паузы ----
            var panel = UiKit.Box("Panel", dim.transform, UiKit.Panel);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 440f));
            _main = panel.gameObject;

            var title = UiKit.Label("Title", panel.transform, "Пауза", 36, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(420f, 50f));
            _info = UiKit.Label("Info", panel.transform, "", 16, UiKit.Soft, TextAnchor.UpperCenter);
            UiKit.Place(_info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(420f, 44f));

            var resume = UiKit.MakeButton("Resume", panel.transform, "Продолжить", UiKit.Button, Hide, 22);
            UiKit.Place(resume.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -135f), new Vector2(400f, 52f));

            var ctrl = UiKit.MakeButton("Controls", panel.transform, "Управление", UiKit.Button, ShowControls, 22);
            UiKit.Place(ctrl.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(400f, 52f));

            _volume = UiKit.Label("Volume", panel.transform, "", 20, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Place(_volume.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -283f), new Vector2(240f, 40f));
            var less = UiKit.MakeButton("Less", panel.transform, "−", UiKit.Button, () => Volume(-0.1f), 26);
            UiKit.Place(less.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-160f, -283f), new Vector2(60f, 40f));
            var more = UiKit.MakeButton("More", panel.transform, "+", UiKit.Button, () => Volume(0.1f), 26);
            UiKit.Place(more.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(160f, -283f), new Vector2(60f, 40f));

            var leave = UiKit.MakeButton("Leave", panel.transform, "Выйти в меню", new Color(0.7f, 0.25f, 0.25f, 1f), Leave, 22);
            UiKit.Place(leave.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -357f), new Vector2(400f, 52f));

            // ---- Страница «Управление» ----
            var cp = UiKit.Box("ControlsPanel", dim.transform, UiKit.Panel);
            UiKit.Place(cp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 500f));
            _controls = cp.gameObject;
            var ct = UiKit.Label("Title", cp.transform, "Управление", 34, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.Place(ct.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(900f, 46f));
            var l1 = UiKit.Label("H1Head", cp.transform, ControlsInfo.Hero1Title, 24, new Color(0.55f, 0.85f, 1f, 1f), TextAnchor.MiddleCenter, true);
            UiKit.Place(l1.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-245f, -72f), new Vector2(460f, 34f));
            var l2 = UiKit.Label("H2Head", cp.transform, ControlsInfo.Hero2Title, 24, new Color(1f, 0.7f, 0.5f, 1f), TextAnchor.MiddleCenter, true);
            UiKit.Place(l2.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(245f, -72f), new Vector2(460f, 34f));
            _h1 = UiKit.Label("H1", cp.transform, "", 18, Color.white, TextAnchor.UpperLeft);
            UiKit.Place(_h1.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-245f, -112f), new Vector2(440f, 230f));
            _h2 = UiKit.Label("H2", cp.transform, "", 18, Color.white, TextAnchor.UpperLeft);
            UiKit.Place(_h2.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(245f, -112f), new Vector2(440f, 230f));
            var extra = UiKit.Label("Extra", cp.transform, ControlsInfo.Radio + "\n" + ControlsInfo.System + "\nЦель: встретиться у реактора, удерживать «действие» обоим и поменяться местами.", 17, UiKit.Soft, TextAnchor.UpperCenter);
            UiKit.Place(extra.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(920f, 80f));
            var back = UiKit.MakeButton("Back", cp.transform, "Назад", UiKit.Button, ShowMain, 22);
            UiKit.Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(260f, 48f));

            _root.SetActive(false);
        }

        public void Toggle()
        {
            if (!_root.activeSelf) Show();
            else if (_controls.activeSelf) ShowMain();   // Esc на «Управлении» возвращает на страницу паузы
            else Hide();
        }

        public void Show()
        {
            _root.SetActive(true);
            ShowMain();
            UiKit.Set(_info, GameSession.JoinInfo ?? "");
        }

        public void Hide() => UiKit.SetActive(_root, false);

        void ShowMain()
        {
            _main.SetActive(true);
            _controls.SetActive(false);
        }

        void ShowControls()
        {
            _main.SetActive(false);
            _controls.SetActive(true);
            UiKit.Set(_h1, ControlsInfo.Hero1);
            UiKit.Set(_h2, ControlsInfo.Hero2);
        }

        void Leave()
        {
            Hide();
            CoreBridge.Leave();
        }

        static void Volume(float delta)
        {
            AudioManager.MasterVolume = Mathf.Round((AudioManager.MasterVolume + delta) * 10f) / 10f;
            AudioManager.Play(Sfx.Click, 0.6f);
        }

        public void Tick()
        {
            if (!_root.activeSelf) return;
            UiKit.Set(_volume, "Громкость: " + Mathf.RoundToInt(AudioManager.MasterVolume * 100f) + "%");
        }
    }
}
#endif
