namespace Echo.UI
{
    // Настройки интерфейса (Чат 4). Меняются из кода или другими модулями.
    public static class UiSettings
    {
        // true — отключить старые IMGUI-интерфейсы ядра (Hud, NetworkBootstrap), когда работает uGUI.
        public static bool HideLegacyUi = false;

        // true — не показывать экран кастомизации внешности при старте партии.
        public static bool SkipCustomization = false;
    }
}
