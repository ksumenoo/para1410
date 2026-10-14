using System;

namespace Echo
{
    // Точки расширения ядра. Другие модули (бой, уровни, интерфейс) подключаются через них,
    // не редактируя файлы ядра.

    // Компонент с этим атрибутом сборщик сцены добавит на сетевой префаб героя.
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AddToHeroPrefabAttribute : Attribute { }

    // MonoBehaviour с этим атрибутом сборщик сцены добавит на объект "Game" (системы сцены).
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AddToGameSystemsAttribute : Attribute { }

    // Серверные события партии. Подписывайтесь в OnEnable/Start, отписывайтесь в OnDisable.
    public static class GameEvents
    {
        // Сервер создал героев, предметы, двери и охрану. Спавните здесь свои сетевые объекты по своим меткам.
        public static event Action ServerGameStarted;
        // Общий рестарт комнаты (кого-то поймали или убили): верните своих врагов/пули в исходное.
        public static event Action ServerRoomRestart;
        // Герои поменялись местами у реактора.
        public static event Action ServerSwapped;

        internal static void RaiseGameStarted() => ServerGameStarted?.Invoke();
        internal static void RaiseRoomRestart() => ServerRoomRestart?.Invoke();
        internal static void RaiseSwapped() => ServerSwapped?.Invoke();
    }
}
