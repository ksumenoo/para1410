namespace Echo.UI
{
    // Состояние рации для интерфейса. Пишут RadioInput (клавиши) и VivoxRadio (голос), читает HUD.
    public static class RadioStatus
    {
        // Голосовой канал Vivox открыт (без пакета Vivox всегда false — рация работает как иконка и помехи).
        public static bool VoiceReady;
        // Микрофон включён (клавиша M).
        public static bool MicOn = true;
        // Герой сейчас говорит в рацию (удерживается V при включённом микрофоне).
        public static bool Transmitting;
        // Напарник говорит.
        public static bool PartnerSpeaking;
        // Короткая подсказка для подписи у иконки.
        public static string Info = "";
    }
}
