#if ECHO_UGUI
using Echo.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.UI
{
    // Пауза по Esc. Игра по сети не останавливается — это просто меню поверх: громкость и выход.
    public class PauseView
    {
        readonly GameObject _root;
        readonly Text _info, _volume;

        public bool Visible => _root.activeSelf;

        public PauseView(Transform parent)
        {
            var dim = UiKit.Box("Pause", parent, new Color(0f, 0f, 0f, 0.55f), false, true);
            UiKit.Stretch(dim.rectTransform);
            _root = dim.gameObject;

            var panel = UiKit.Box("Panel", dim.transform, UiKit.Panel);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 380f));

            var title = UiKit.Label("Title", panel.transform, "Пауза", 36, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(420f, 50f));
            _info = UiKit.Label("Info", panel.transform, "", 16, UiKit.Soft, TextAnchor.UpperCenter);
            UiKit.Place(_info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(420f, 44f));

            var resume = UiKit.MakeButton("Resume", panel.transform, "Продолжить", UiKit.Button, Hide, 22);
            UiKit.Place(resume.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(400f, 52f));

            _volume = UiKit.Label("Volume", panel.transform, "", 20, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Place(_volume.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -218f), new Vector2(240f, 40f));
            var less = UiKit.MakeButton("Less", panel.transform, "−", UiKit.Button, () => Volume(-0.1f), 26);
            UiKit.Place(less.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-160f, -218f), new Vector2(60f, 40f));
            var more = UiKit.MakeButton("More", panel.transform, "+", UiKit.Button, () => Volume(0.1f), 26);
            UiKit.Place(more.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(160f, -218f), new Vector2(60f, 40f));

            var leave = UiKit.MakeButton("Leave", panel.transform, "Выйти в меню", new Color(0.7f, 0.25f, 0.25f, 1f), Leave, 22);
            UiKit.Place(leave.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -292f), new Vector2(400f, 52f));

            _root.SetActive(false);
        }

        public void Toggle()
        {
            if (_root.activeSelf) Hide(); else Show();
        }

        public void Show()
        {
            _root.SetActive(true);
            UiKit.Set(_info, GameSession.JoinInfo ?? "");
        }

        public void Hide() => UiKit.SetActive(_root, false);

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
