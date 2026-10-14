using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echo.EditorTools
{
    // Шаг сборки сцены из другого модуля: public static void Build(EchoBuildContext ctx) с этим атрибутом.
    // Вызывается после того, как построены обе линии и префабы ядра. Меньший order — раньше.
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class EchoBuildStepAttribute : Attribute
    {
        public readonly int Order;
        public EchoBuildStepAttribute(int order = 0) { Order = order; }
    }

    // Построитель уровня: public static void BuildLine(EchoBuildContext ctx, Transform lineRoot, int line).
    // Если такой метод есть, он заменяет тестовый коридор ядра. Должен быть ровно один.
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class EchoLevelBuilderAttribute : Attribute { }

    public class EchoBuildContext
    {
        public Transform Line1;
        public Transform Line2;
        public GameObject GameSystems;
        public string Root = "Assets/_Echo";
        internal readonly List<GameObject> NetworkPrefabs = new List<GameObject>();

        // Зарегистрировать сетевой префаб (с NetworkObject на корне) в списке префабов NetworkManager.
        public void RegisterNetworkPrefab(GameObject prefab)
        {
            if (prefab != null && !NetworkPrefabs.Contains(prefab)) NetworkPrefabs.Add(prefab);
        }

        public Transform LineRoot(int line) => line == 1 ? Line1 : Line2;
    }
}
