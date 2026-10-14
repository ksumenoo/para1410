using UnityEngine;

namespace Echo.Audio
{
    // Ссылки на звуки. Заполняет редакторский шаг (Echo → Интерфейс → Обновить арт и звук) из Assets/Art/Audio.
    // Лежит в Assets/_Echo/UI/Resources. Нет клипа — тишина без ошибок.
    public class EchoAudioLibrary : ScriptableObject
    {
        public const string ResourceName = "EchoAudioLibrary";

        [Header("Музыка")]
        public AudioClip musicPast;
        public AudioClip musicFuture;

        [Header("Звуки")]
        public AudioClip[] steps;
        public AudioClip pickup;
        public AudioClip drop;
        public AudioClip door;
        public AudioClip alarm;
        public AudioClip shot;
        public AudioClip shotBack;
        public AudioClip hit;
        public AudioClip reactorHum;
        public AudioClip click;
        public AudioClip radioOn;
        public AudioClip radioOff;
        public AudioClip radioNoise;
        public AudioClip ending;

        static EchoAudioLibrary _instance;
        static bool _loaded;

        public static EchoAudioLibrary I
        {
            get
            {
                if (!_loaded) { _loaded = true; _instance = Resources.Load<EchoAudioLibrary>(ResourceName); }
                return _instance;
            }
        }
    }
}
