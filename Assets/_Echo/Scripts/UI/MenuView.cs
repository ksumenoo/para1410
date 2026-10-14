#if ECHO_UGUI
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.UI
{
    // Главное меню на uGUI. Действия — те же, что в старом IMGUI-меню ядра (через CoreBridge).
    public class MenuView
    {
        readonly GameObject _root;
        readonly Button _hero1, _hero2;
        readonly InputField _code, _ip;
        readonly Text _status;
        readonly List<Button> _actions = new List<Button>();

        public MenuView(Transform parent)
        {
            var bg = UiKit.Box("Menu", parent, new Color(0.04f, 0.04f, 0.08f, 1f), false, true);
            UiKit.Stretch(bg.rectTransform);
            _root = bg.gameObject;

            var panel = UiKit.Box("Panel", bg.transform, UiKit.Panel);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 640f));
            var p = panel.transform;

            Lbl(p, "Наоборот получилось", 38, UiKit.Accent, 22f, 52f, true, TextAnchor.MiddleCenter);
            Lbl(p, "Каким героем играешь (если создаёшь игру):", 17, UiKit.Soft, 88f, 26f);

            _hero1 = Btn(p, "Герой 1\nнастоящее → будущее", -145f, 118f, 270f, 54f, () => GameSession.HostHeroChoice = 1, 16, false);
            _hero2 = Btn(p, "Герой 2\nбудущее → прошлое", 145f, 118f, 270f, 54f, () => GameSession.HostHeroChoice = 2, 16, false);
            Lbl(p, "Второй игрок получит другого героя.", 14, UiKit.Soft, 176f, 22f);

            Lbl(p, "Через интернет (код комнаты):", 17, UiKit.Soft, 208f, 26f);
            Btn(p, "Создать игру и получить код", 0f, 238f, 540f, 42f, () => CoreBridge.CreateRelay());
            _code = UiKit.MakeInput("Code", p, "", "КОД", 12);
            UiKit.Place(_code.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-135f, -290f), new Vector2(270f, 42f));
            Btn(p, "Войти по коду", 145f, 290f, 270f, 42f, () => CoreBridge.JoinRelay(_code.text.Trim().ToUpperInvariant()));

            Lbl(p, "По IP (одна сеть или отладка):", 17, UiKit.Soft, 346f, 26f);
            Btn(p, "Создать игру по IP (порт 7777)", 0f, 376f, 540f, 42f, () => CoreBridge.HostIp());
            _ip = UiKit.MakeInput("Ip", p, "127.0.0.1", "IP", 40);
            UiKit.Place(_ip.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-135f, -428f), new Vector2(270f, 42f));
            Btn(p, "Войти по IP", 145f, 428f, 270f, 42f, () => CoreBridge.JoinIp(_ip.text.Trim()));

            Btn(p, "Один ПК (оба героя с одной клавиатуры)", 0f, 494f, 540f, 50f, () => CoreBridge.StartSinglePc(), 20, true);
            _status = Lbl(p, "", 15, UiKit.Danger, 556f, 70f);
        }

        Text Lbl(Transform parent, string text, int size, Color color, float y, float height, bool bold = false, TextAnchor anchor = TextAnchor.UpperCenter)
        {
            var t = UiKit.Label("Label", parent, text, size, color, anchor, bold);
            UiKit.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(540f, height));
            return t;
        }

        Button Btn(Transform parent, string caption, float x, float y, float w, float h, UnityEngine.Events.UnityAction onClick, int size = 18, bool action = true)
        {
            var b = UiKit.MakeButton("Button", parent, caption, UiKit.Button, onClick, size);
            UiKit.Place(b.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -y), new Vector2(w, h));
            if (action) _actions.Add(b);
            return b;
        }

        public void Tick(bool active)
        {
            UiKit.SetActive(_root, active);
            if (!active) return;

            Paint(_hero1, GameSession.HostHeroChoice == 1);
            Paint(_hero2, GameSession.HostHeroChoice == 2);
            bool busy = CoreBridge.Busy;
            foreach (var b in _actions) if (b.interactable == busy) b.interactable = !busy;
            UiKit.Set(_status, CoreBridge.Status);
        }

        static void Paint(Button b, bool on)
        {
            var img = (Image)b.targetGraphic;
            var c = on ? UiKit.ButtonOn : UiKit.Button;
            if (img.color != c) img.color = c;
        }
    }
}
#endif
