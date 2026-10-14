#if ECHO_UGUI
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.UI
{
    // HUD на uGUI: верхняя половина — свой герой, нижняя — напарник.
    // Имя и линия, предмет в руке, инвентарь (Tab), сообщения, прогресс сопряжения, подсказки, рация.
    public class HudView
    {
        readonly GameObject _root;
        readonly HeroHalf _top, _bottom;
        readonly Text _info;

        public HudView(Transform parent)
        {
            var rt = UiKit.NewRect("Hud", parent);
            UiKit.Stretch(rt);
            _root = rt.gameObject;
            _top = new HeroHalf(rt, true);
            _bottom = new HeroHalf(rt, false);

            var line = UiKit.Box("Divider", rt, new Color(1f, 1f, 1f, 0.85f), false);
            line.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            line.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            line.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            line.rectTransform.anchoredPosition = Vector2.zero;
            line.rectTransform.sizeDelta = new Vector2(0f, 3f);

            _info = UiKit.Label("Info", rt, "", 16, UiKit.Soft, TextAnchor.UpperCenter);
            UiKit.Place(_info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(600f, 40f));
        }

        public void Tick(bool active)
        {
            UiKit.SetActive(_root, active);
            if (!active) return;

            var cams = SplitCameras.I;
            Hero topHero = cams != null ? cams.TopHero : null;
            Hero bottomHero = cams != null ? cams.BottomHero : null;
            Hero my = SplitCameras.MyHero();
            _top.Tick(topHero, IsMine(topHero, my));
            _bottom.Tick(bottomHero, IsMine(bottomHero, my));

            var nm = NetworkManager.Singleton;
            string info = GameSession.JoinInfo ?? "";
            if (nm != null && nm.IsServer && !GameSession.SinglePc && nm.ConnectedClientsIds.Count < 2)
                info += (info.Length > 0 ? "\n" : "") + "Ждём второго игрока…";
            UiKit.Set(_info, info);
        }

        static bool IsMine(Hero hero, Hero my) =>
            hero != null && hero.IsOwner && (GameSession.SinglePc || hero == my);

        // ---------- Половина экрана одного героя ----------

        class Cell
        {
            public GameObject Root;
            public Image Bg, Icon;
            public Text Name, Index;
        }

        class HeroHalf
        {
            readonly RectTransform _root;
            readonly Text _title, _hand, _toast, _status, _hints, _radio, _reactorLabel;
            readonly Image _radioBg, _reactorFill;
            readonly GameObject _reactor;
            readonly RectTransform _invPanel;
            readonly List<Cell> _cells = new List<Cell>();
            float _nextSlow;

            public HeroHalf(Transform parent, bool top)
            {
                _root = UiKit.NewRect(top ? "HalfTop" : "HalfBottom", parent);
                _root.anchorMin = top ? new Vector2(0f, 0.5f) : Vector2.zero;
                _root.anchorMax = top ? Vector2.one : new Vector2(1f, 0.5f);
                _root.offsetMin = Vector2.zero;
                _root.offsetMax = Vector2.zero;

                _title = UiKit.Label("Title", _root, "", 20, UiKit.Accent, TextAnchor.UpperLeft, true);
                UiKit.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(820f, 30f));
                _hand = UiKit.Label("Hand", _root, "", 17, Color.white, TextAnchor.UpperLeft);
                UiKit.Place(_hand.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -38f), new Vector2(820f, 26f));

                _invPanel = UiKit.NewRect("Inventory", _root);
                UiKit.Place(_invPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -70f), new Vector2(900f, 56f));

                // Рация
                _radioBg = UiKit.Box("Radio", _root, UiKit.Panel);
                UiKit.Place(_radioBg.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -10f), new Vector2(240f, 32f));
                _radio = UiKit.Label("Text", _radioBg.transform, "", 15, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Stretch(_radio.rectTransform, 2f);

                // Сообщения и состояние
                _toast = UiKit.Label("Toast", _root, "", 26, UiKit.Accent, TextAnchor.MiddleCenter, true);
                UiKit.Place(_toast.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(1000f, 44f));
                _status = UiKit.Label("Status", _root, "", 24, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Place(_status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(600f, 40f));

                // Прогресс сопряжения
                var bar = UiKit.Box("Reactor", _root, UiKit.PanelDark);
                UiKit.Place(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(360f, 24f));
                _reactor = bar.gameObject;
                _reactorFill = UiKit.Box("Fill", bar.transform, UiKit.Teal, false);
                _reactorFill.rectTransform.anchorMin = new Vector2(0f, 0f);
                _reactorFill.rectTransform.anchorMax = new Vector2(0f, 1f);
                _reactorFill.rectTransform.offsetMin = new Vector2(3f, 3f);
                _reactorFill.rectTransform.offsetMax = new Vector2(-3f, -3f);
                _reactorLabel = UiKit.Label("Label", bar.transform, "Сопряжение…", 16, Color.white, TextAnchor.LowerCenter, true);
                UiKit.Place(_reactorLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(360f, 24f));

                // Подсказки клавиш
                _hints = UiKit.Label("Hints", _root, "", 14, new Color(0.9f, 0.92f, 1f, 0.9f), TextAnchor.LowerLeft);
                UiKit.Place(_hints.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 8f), new Vector2(1240f, 26f));
            }

            public void Tick(Hero hero, bool mine)
            {
                bool show = hero != null && hero.IsSpawned;
                UiKit.SetActive(_root.gameObject, show);
                if (!show) return;

                // Каждый кадр: сообщение, прогресс, рация, состояние
                UiKit.Set(_toast, CoreBridge.TryGetToast(hero.HeroId, out string msg) ? msg : "");
                UiKit.Set(_status, hero.IsHiding ? "В шкафчике" : "");

                float p = hero.ReactorProgress.Value;
                UiKit.SetActive(_reactor, p > 0f);
                if (p > 0f) _reactorFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);

                TickRadio(mine);

                // 10 раз в секунду: подписи и инвентарь
                if (Time.unscaledTime < _nextSlow) return;
                _nextSlow = Time.unscaledTime + 0.1f;

                string title = "Герой " + hero.HeroId + " · " + TimeLines.LineName(hero.Line.Value) + (mine ? "  (ты)" : "  (напарник)");
                if (hero.IsSneaking) title += " · крадётся";
                if (hero.IsHiding) title += " · прячется";
                UiKit.Set(_title, title);

                var inv = hero.Inventory();
                if (mine)
                {
                    string sel = hero.SelectedSlot < inv.Count ? inv[hero.SelectedSlot].DisplayName : "пусто";
                    UiKit.Set(_hand, "В руке: " + sel + "   ·   " + inv.Count + "/" + hero.Capacity);
                }
                else UiKit.Set(_hand, "Инвентарь: " + inv.Count + "/" + hero.Capacity);

                UiKit.Set(_hints, mine ? InputScheme.Hint(hero) : "");
                RefreshInventory(hero, inv, mine && hero.InventoryOpen);
            }

            void TickRadio(bool mine)
            {
                string text;
                Color fill;
                if (mine)
                {
                    if (RadioStatus.Transmitting) { text = "● ГОВОРИШЬ"; fill = UiKit.ButtonOn; }
                    else if (!RadioStatus.MicOn) { text = "Микрофон выкл (M)"; fill = new Color(0.35f, 0.35f, 0.4f, 0.9f); }
                    else if (RadioStatus.VoiceReady) { text = "Рация · держи V"; fill = new Color(0.15f, 0.5f, 0.45f, 0.9f); }
                    else { text = "Рация · V (без голоса)"; fill = new Color(0.25f, 0.3f, 0.45f, 0.9f); }
                }
                else
                {
                    if (RadioStatus.PartnerSpeaking) { text = "● напарник говорит"; fill = UiKit.ButtonOn; }
                    else { text = "Напарник на связи"; fill = new Color(0.2f, 0.22f, 0.33f, 0.8f); }
                }
                UiKit.Set(_radio, text);
                if (_radioBg.color != fill) _radioBg.color = fill;
            }

            void RefreshInventory(Hero hero, List<Item> inv, bool open)
            {
                UiKit.SetActive(_invPanel.gameObject, open);
                if (!open) return;

                int capacity = hero.Capacity;
                while (_cells.Count < capacity) _cells.Add(MakeCell(_cells.Count));
                var assets = EchoUiAssets.I;
                for (int i = 0; i < _cells.Count; i++)
                {
                    var cell = _cells[i];
                    bool exists = i < capacity;
                    UiKit.SetActive(cell.Root, exists);
                    if (!exists) continue;

                    Color bg = i == hero.SelectedSlot ? UiKit.Accent : UiKit.Slot;
                    if (cell.Bg.color != bg) cell.Bg.color = bg;

                    Item item = i < inv.Count ? inv[i] : null;
                    Sprite icon = item != null && assets != null ? assets.ItemIcon(item.ItemType) : null;
                    cell.Icon.sprite = icon;
                    UiKit.SetActive(cell.Icon.gameObject, icon != null);
                    UiKit.Set(cell.Name, item != null && icon == null ? item.DisplayName : "");
                }
            }

            Cell MakeCell(int index)
            {
                var cell = new Cell();
                var bg = UiKit.Box("Slot" + index, _invPanel, UiKit.Slot);
                UiKit.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(index * 58f, 0f), new Vector2(52f, 52f));
                cell.Root = bg.gameObject;
                cell.Bg = bg;
                cell.Icon = UiKit.Box("Icon", bg.transform, Color.white, false);
                cell.Icon.preserveAspect = true;
                UiKit.Stretch(cell.Icon.rectTransform, 6f);
                cell.Name = UiKit.Label("Name", bg.transform, "", 11, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Stretch(cell.Name.rectTransform, 2f);
                cell.Index = UiKit.Label("Index", bg.transform, index < 10 ? ((index + 1) % 10).ToString() : "", 12, UiKit.Soft, TextAnchor.UpperLeft, true);
                UiKit.Place(cell.Index.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(3f, -1f), new Vector2(20f, 16f));
                return cell;
            }
        }
    }
}
#endif
