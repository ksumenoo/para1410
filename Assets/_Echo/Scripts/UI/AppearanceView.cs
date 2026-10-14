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
            UiKit.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -52f), new Vector2(380f, 296f));
            _root = panel.gameObject;

            var title = UiKit.Label("Title", panel.transform, "Внешность героя (один на двоих)", 20, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(370f, 28f));

            for (int i = 0; i < Parts; i++)
            {
                int part = i;
                float y = -40f - i * 30f;
                var name = UiKit.Label("Name" + i, panel.transform, AppearanceState.PartNames[i], 16, Color.white, TextAnchor.MiddleLeft);
                UiKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(130f, 28f));

                var prev = UiKit.MakeButton("Prev" + i, panel.transform, "<", UiKit.Button, () => Step(part, -1), 20);
                UiKit.Place(prev.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, y), new Vector2(30f, 28f));
                _values[i] = UiKit.Label("Value" + i, panel.transform, "", 18, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Place(_values[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(184f, y), new Vector2(66f, 28f));
                var next = UiKit.MakeButton("Next" + i, panel.transform, ">", UiKit.Button, () => Step(part, +1), 20);
                UiKit.Place(next.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(254f, y), new Vector2(30f, 28f));

                _swatches[i] = UiKit.Box("Swatch" + i, panel.transform, Color.white);
                UiKit.Place(_swatches[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(298f, y), new Vector2(60f, 28f));
            }

            _status = UiKit.Label("Status", panel.transform, "", 14, UiKit.Soft, TextAnchor.MiddleCenter);
            UiKit.Place(_status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(370f, 22f));
            _ready = UiKit.MakeButton("Ready", panel.transform, "Готово", UiKit.ButtonOn, OnReady, 20);
            UiKit.Place(_ready.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(340f, 38f));
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

        static bool MineReady(AppearanceState state, out bool minePresent)
        {
            minePresent = false;
            bool ready = false;
            foreach (var h in Hero.All)
            {
                if (!h.IsSpawned || !h.IsOwner) continue;
                minePresent = true;
                if ((state.ReadyMask.Value & (1 << (h.HeroId - 1))) != 0) ready = true;
            }
            return ready;
        }

        public void Tick(bool inGame)
        {
            var state = AppearanceState.I;
            bool show = inGame && !UiSettings.SkipCustomization && state != null && state.IsSpawned
                        && !state.AllReady && Hero.All.Count >= 2 && !IntroView.Active;
            bool mineReady = false, minePresent = false;
            if (show)
            {
                mineReady = MineReady(state, out minePresent);
                // Этот игрок уже нажал «Готово» — экран закрывается, игра идёт дальше.
                if (mineReady) show = false;
            }
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

            bool partner = PartnerPresent();
            _ready.interactable = minePresent && partner;
            UiKit.Set(_status, partner ? "Довольны — нажмите «Готово» оба." : "Ждём второго игрока…");
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
