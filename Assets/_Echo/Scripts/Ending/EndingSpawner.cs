using Echo.UI;
using Unity.Netcode;
using UnityEngine;

namespace Echo.Ending
{
    // Система на объекте Game: на старте партии сервер создаёт сетевые синглтоны интерфейса
    // (концовки и кастомизация). Префабы вписывает редакторский шаг сборки сцены.
    [AddToGameSystems]
    public class EndingSpawner : MonoBehaviour
    {
        public NetworkObject endingPrefab;
        public NetworkObject appearancePrefab;

        void OnEnable() => GameEvents.ServerGameStarted += OnGameStarted;
        void OnDisable() => GameEvents.ServerGameStarted -= OnGameStarted;

        void OnGameStarted()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            if (endingPrefab != null && EndingState.I == null)
                Instantiate(endingPrefab).Spawn(true);
            if (appearancePrefab != null && AppearanceState.I == null)
                Instantiate(appearancePrefab).Spawn(true);
        }
    }
}
