namespace Echo
{
    // Настройки текущей партии, выбранные в меню.
    public static class GameSession
    {
        // Оба героя управляются с одной клавиатуры (отладка и запасной режим сдачи).
        public static bool SinglePc;

        // Каким героем играет тот, кто создал игру. Второй игрок получает другого.
        public static int HostHeroChoice = 1;

        // Что показать в углу экрана: код сессии или IP.
        public static string JoinInfo = "";
    }
}
