using UnityEngine;

namespace Echo.UI
{
    // Ссылки на арт интерфейса. Заполняет редакторский шаг (Echo → Интерфейс → Обновить арт),
    // лежит в Assets/_Echo/UI/Resources. Нет файла или картинки — интерфейс рисует заглушки.
    public class EchoUiAssets : ScriptableObject
    {
        public const string ResourceName = "EchoUiAssets";

        public Sprite endLoop;
        public Sprite endOneBody;
        public Sprite slot;
        public Sprite radioTalk;
        public Sprite radioMuted;
        [Tooltip("По порядку ItemType: Key, Barrel, Backpack, Ammo, Pistol")]
        public Sprite[] itemIcons = new Sprite[5];

        static EchoUiAssets _instance;
        static bool _loaded;

        public static EchoUiAssets I
        {
            get
            {
                if (!_loaded) { _loaded = true; _instance = Resources.Load<EchoUiAssets>(ResourceName); }
                return _instance;
            }
        }

        public Sprite ItemIcon(ItemType type)
        {
            int i = (int)type;
            return itemIcons != null && i >= 0 && i < itemIcons.Length ? itemIcons[i] : null;
        }
    }
}
