#if ECHO_UGUI
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.UI
{
    // Экран «зеркала»: оба игрока вместе собирают внешность героя (6 частей), затем нажимают «Готово».
    // Применяется ко всем героям сразу (AppearanceState), так что изменения видно в игре.
    public class AppearanceView
    {
        const int Parts = 6;

        readonly GameObject _root;
        readonly Text[] _values = new Text[Parts];
        readonly Image[] _swatches = new Image[Parts];
        readonly Button _ready;
        readonly Text _status;

        public AppearanceView(Transform parent)
        {
            var panel = UiKit.Box("Appearance", parent, UiKit.PanelDark, true, true);
            UiKit.Place(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(380f, 490f));
            _root = panel.gameObject;

            var title = UiKit.Label("Title", panel.transform, "Внешность героя", 26, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(360f, 36f));
            var sub = UiKit.Label("Sub", panel.transform, "Один герой на двоих — собираете вместе.", 15, UiKit.Soft, TextAnchor.MiddleCenter);
            UiKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(360f, 24f));

            for (int i = 0; i < Parts; i++)
            {
                int part = i;
                float y = -90f - i * 50f;
                var name = UiKit.Label("Name" + i, panel.transform, AppearanceState.PartNames[i], 17, Color.white, TextAnchor.MiddleLeft);
                UiKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(130f, 36f));

                var prev = UiKit.MakeButton("Prev" + i, panel.transform, "<", UiKit.Button, () => Step(part, -1), 22);
                UiKit.Place(prev.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, y), new Vector2(36f, 36f));
                _values[i] = UiKit.Label("Value" + i, panel.transform, "", 18, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Place(_values[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(190f, y), new Vector2(60f, 36f));
                var next = UiKit.MakeButton("Next" + i, panel.transform, ">", UiKit.Button, () => Step(part, +1), 22);
                UiKit.Place(next.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(254f, y), new Vector2(36f, 36f));

                _swatches[i] = UiKit.Box("Swatch" + i, panel.transform, Color.white);
                UiKit.Place(_swatches[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(304f, y - 3f), new Vector2(54f, 30f));
            }

            _status = UiKit.Label("Status", panel.transform, "", 16, UiKit.Soft, TextAnchor.MiddleCenter);
            UiKit.Place(_status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(360f, 44f));
            _ready = UiKit.MakeButton("Ready", panel.transform, "Готово", UiKit.ButtonOn, OnReady, 24);
            UiKit.Place(_ready.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(340f, 50f));
            _root.SetActive(false);
        }

        static void Step(int part, int delta)
        {
            if (AppearanceState.I != null) AppearanceState.I.Step(part, delta);
        }

        static void OnReady()
        {
            var state = AppearanceState.I;
            if (state == null) return;
            foreach (var h in Hero.All)
                if (h.IsSpawned && h.IsOwner) state.SetReady(h.HeroId, true);
        }

        static bool PartnerPresent()
        {
            var nm = NetworkManager.Singleton;
            return GameSession.SinglePc || (nm != null && nm.ConnectedClientsIds.Count >= 2);
        }

        public void Tick(bool inGame)
        {
            var state = AppearanceState.I;
            bool show = inGame && !UiSettings.SkipCustomization && state != null && state.IsSpawned && !state.AllReady && Hero.All.Count >= 2;
            UiKit.SetActive(_root, show);
            if (!show) return;

            var data = state.Data.Value;
            HeroAppearance look = null;
            foreach (var h in Hero.All) if (h.rigAppearance != null) { look = h.rigAppearance; break; }

            for (int i = 0; i < Parts; i++)
            {
                UiKit.Set(_values[i], (AppearanceState.Get(data, i) + 1) + "/" + AppearanceState.Count(i));
                Color? swatch = look != null ? SwatchColor(look, i, AppearanceState.Get(data, i)) : null;
                UiKit.SetActive(_swatches[i].gameObject, swatch.HasValue);
                if (swatch.HasValue && _swatches[i].color != swatch.Value) _swatches[i].color = swatch.Value;
            }

            bool minePresent = false, mineReady = false;
            foreach (var h in Hero.All)
            {
                if (!h.IsSpawned || !h.IsOwner) continue;
                minePresent = true;
                if ((state.ReadyMask.Value & (1 << (h.HeroId - 1))) != 0) mineReady = true;
            }
            bool partner = PartnerPresent();
            _ready.interactable = minePresent && partner && !mineReady;
            UiKit.SetCaption(_ready, mineReady ? "Ждём напарника…" : "Готово");
            UiKit.Set(_status, !partner ? "Ждём второго игрока…" :
                mineReady ? "Ты готов. Ждём, пока напарник нажмёт «Готово»." : "Когда довольны — нажмите «Готово» оба.");
        }

        static Color? SwatchColor(HeroAppearance a, int part, int index)
        {
            Color[] list = part == 3 ? a.skinColors : part == 4 ? a.hairColors : part == 5 ? a.suitColors : null;
            if (list == null || list.Length == 0) return null;
            return list[Mathf.Clamp(index, 0, list.Length - 1)];
        }
    }
}
#endif
