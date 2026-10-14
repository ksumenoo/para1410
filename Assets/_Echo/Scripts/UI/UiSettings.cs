namespace Echo.UI
{
    // Настройки интерфейса (Чат 4). Меняются из кода или другими модулями.
    public static class UiSettings
    {
        // true — не рисовать старые IMGUI-интерфейсы ядра (Hud и меню NetworkBootstrap), их заменяет uGUI.
        public static bool HideLegacyUi = true;

        // true — не показывать экран кастомизации внешности при старте партии.
        public static bool SkipCustomization = false;
        public static bool SkipIntro = false;
    }
}
