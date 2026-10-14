using UnityEngine;
using UnityEngine.InputSystem;

namespace Echo
{
    // Раскладка: по сети каждый играет на WASD. В режиме "Один ПК" Герой 2 на стрелках.
    public static class InputScheme
    {
        static bool Alt(Hero h) => GameSession.SinglePc && h.HeroId == 2;

        static readonly Key[] Digits =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0
        };

        public static Vector2 Move(Hero h, Keyboard kb)
        {
            float x = 0f, y = 0f;
            bool arrows = Alt(h) || !GameSession.SinglePc;
            bool wasd = !Alt(h);
            if (wasd)
            {
                if (kb.aKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed) x += 1f;
                if (kb.sKey.isPressed) y -= 1f;
                if (kb.wKey.isPressed) y += 1f;
            }
            if (arrows)
            {
                if (kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.rightArrowKey.isPressed) x += 1f;
                if (kb.downArrowKey.isPressed) y -= 1f;
                if (kb.upArrowKey.isPressed) y += 1f;
            }
            return new Vector2(Mathf.Clamp(x, -1f, 1f), Mathf.Clamp(y, -1f, 1f));
        }

        public static bool Sneak(Hero h, Keyboard kb) =>
            Alt(h) ? kb.rightShiftKey.isPressed : kb.leftShiftKey.isPressed;

        public static bool InteractDown(Hero h, Keyboard kb) =>
            Alt(h) ? kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                   : kb.eKey.wasPressedThisFrame;

        public static bool InteractHeld(Hero h, Keyboard kb) =>
            Alt(h) ? kb.enterKey.isPressed || kb.numpadEnterKey.isPressed
                   : kb.eKey.isPressed;

        public static bool Drop(Hero h, Keyboard kb) =>
            Alt(h) ? kb.backspaceKey.wasPressedThisFrame : kb.gKey.wasPressedThisFrame;

        public static bool Throw(Hero h, Keyboard kb) =>
            Alt(h) ? kb.rightCtrlKey.wasPressedThisFrame : kb.fKey.wasPressedThisFrame;

        public static bool InventoryToggle(Hero h, Keyboard kb) =>
            Alt(h) ? kb.backslashKey.wasPressedThisFrame : kb.tabKey.wasPressedThisFrame;

        public static bool NextSlot(Hero h, Keyboard kb) =>
            Alt(h) && kb.rightBracketKey.wasPressedThisFrame;

        public static int DigitSlot(Hero h, Keyboard kb)
        {
            if (Alt(h)) return -1;
            for (int i = 0; i < Digits.Length; i++)
                if (kb[Digits[i]].wasPressedThisFrame) return i;
            return -1;
        }

        public static string Hint(Hero h) => Alt(h)
            ? "Стрелки — ход · Enter — действие/спрятаться · Backspace — положить · RCtrl — бросить · \\ — инвентарь · ] — ячейка · RShift — красться"
            : "WASD — ход · E — действие/спрятаться · G — положить · F — бросить · Tab — инвентарь · 1–0 — ячейка · Shift — красться";
    }
}
