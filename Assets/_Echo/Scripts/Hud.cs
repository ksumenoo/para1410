using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Echo
{
    // Временный интерфейс на IMGUI: подписи половин, инвентарь, сообщения, прогресс реактора.
    public class Hud : MonoBehaviour
    {
        static readonly Dictionary<int, (string text, float until)> Toasts = new Dictionary<int, (string, float)>();

        // Для нового интерфейса (Чат 4): событие и чтение текущего сообщения героя.
        public static event System.Action<int, string> ToastShown;

        public static void Toast(int heroId, string text)
        {
            Toasts[heroId] = (text, Time.time + 2.5f);
            ToastShown?.Invoke(heroId, text);
        }

        public static bool TryGetToast(int heroId, out string text)
        {
            text = null;
            if (!Toasts.TryGetValue(heroId, out var t) || Time.time >= t.until) return false;
            text = t.text;
            return true;
        }

        GUIStyle _label, _slot, _toast;

        void OnGUI()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !(nm.IsServer || nm.IsClient) || SplitCameras.I == null) return;
            EnsureStyles();

            float s = NetworkBootstrap.GuiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;
            float h = Screen.height / s;

            DrawHalf(SplitCameras.I.TopHero, 0f, w, h / 2f);
            DrawHalf(SplitCameras.I.BottomHero, h / 2f, w, h / 2f);

            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.DrawTexture(new Rect(0f, h / 2f - 1.5f, w, 3f), Texture2D.whiteTexture);
            GUI.color = old;
        }

        void DrawHalf(Hero hero, float y0, float w, float hh)
        {
            if (hero == null || !hero.IsSpawned) return;
            bool mine = hero.IsOwner && (GameSession.SinglePc || hero == SplitCameras.MyHero());

            string title = "Герой " + hero.HeroId + " · " + TimeLines.LineName(hero.Line.Value) + (mine ? "  (ты)" : "  (напарник)");
            if (hero.IsSneaking) title += " · крадётся";
            if (hero.IsHiding) title += " · прячется";
            GUI.Label(new Rect(12f, y0 + 8f, w - 24f, 26f), title, _label);

            var inv = hero.Inventory();
            bool full = mine && hero.InventoryOpen;
            if (full)
            {
                for (int i = 0; i < hero.Capacity; i++)
                {
                    var r = new Rect(12f + i * 52f, y0 + 38f, 48f, 48f);
                    var old = GUI.color;
                    if (i == hero.SelectedSlot) GUI.color = new Color(1f, 0.85f, 0.3f);
                    GUI.Box(r, (i < 10 ? ((i + 1) % 10) + "\n" : "") + (i < inv.Count ? inv[i].DisplayName : ""), _slot);
                    GUI.color = old;
                }
            }
            else
            {
                string sel = mine
                    ? (hero.SelectedSlot < inv.Count ? inv[hero.SelectedSlot].DisplayName : "пусто")
                    : inv.Count + " предм.";
                GUI.Label(new Rect(12f, y0 + 36f, w - 24f, 24f),
                    (mine ? "В руке: " + sel + "   ·   " : "Инвентарь: " + sel + "   ·   ") + inv.Count + "/" + hero.Capacity, _label);
            }
            if (hero.IsHiding)
                GUI.Label(new Rect(0f, y0 + hh / 2f - 20f, w, 30f), "В шкафчике", _toast);

            if (hero.ReactorProgress.Value > 0f)
            {
                var bar = new Rect(w / 2f - 160f, y0 + hh - 70f, 320f, 18f);
                GUI.Box(bar, GUIContent.none);
                var old = GUI.color;
                GUI.color = new Color(0.4f, 1f, 0.9f);
                GUI.DrawTexture(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * hero.ReactorProgress.Value, bar.height - 4f), Texture2D.whiteTexture);
                GUI.color = old;
                GUI.Label(new Rect(bar.x, bar.y - 24f, bar.width, 22f), "Сопряжение…", _label);
            }

            if (Toasts.TryGetValue(hero.HeroId, out var t) && Time.time < t.until)
                GUI.Label(new Rect(0f, y0 + hh / 2f - 60f, w, 30f), t.text, _toast);

            if (mine)
                GUI.Label(new Rect(12f, y0 + hh - 30f, w - 24f, 24f), InputScheme.Hint(hero), _label);
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
            _label.normal.textColor = Color.white;
            _slot = new GUIStyle(GUI.skin.box) { fontSize = 10, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            _toast = new GUIStyle(_label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }
    }
}
