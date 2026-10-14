using Unity.Netcode;
using UnityEngine;

namespace Echo.Combat
{
    // Сервер: спавнит мутантов и сгустки по меткам, возвращает их в исходное на рестарте комнаты.
    // На клиентах только хранит материалы эффектов. Оглушение охраны — Guard.ServerStun (ядро).
    [AddToGameSystems]
    public class CombatDirector : MonoBehaviour
    {
        public static CombatDirector I { get; private set; }

        [Header("Префабы (ставит шаг сборки боя)")]
        public NetworkObject mutantPrefab;
        public NetworkObject timeClotPrefab;

        [Header("Материалы эффектов")]
        public Material bulletMaterial;
        public Material tracerInverseMaterial;
        public Material tracerNormalMaterial;
        public Material stunMaterial;

        void Awake() => I = this;

        void OnEnable()
        {
            GameEvents.ServerGameStarted += OnGameStarted;
            GameEvents.ServerRoomRestart += OnRoomRestart;
            GameEvents.ServerSwapped += OnSwapped;
        }

        void OnDisable()
        {
            GameEvents.ServerGameStarted -= OnGameStarted;
            GameEvents.ServerRoomRestart -= OnRoomRestart;
            GameEvents.ServerSwapped -= OnSwapped;
        }

        void OnGameStarted()
        {
            if (TimeLines.I == null) return;
            if (mutantPrefab != null)
                foreach (var m in FindObjectsByType<MutantMarker>(FindObjectsSortMode.None))
                {
                    var no = SpawnAt(mutantPrefab, m.transform);
                    var mutant = no.GetComponent<Mutant>();
                    if (mutant != null) mutant.startDead = m.startDead;
                    Init(no);
                }
            if (timeClotPrefab != null)
                foreach (var c in FindObjectsByType<TimeClotMarker>(FindObjectsSortMode.None))
                    Init(SpawnAt(timeClotPrefab, c.transform));
        }

        static NetworkObject SpawnAt(NetworkObject prefab, Transform marker)
        {
            var rot = Quaternion.Euler(0f, marker.eulerAngles.y, 0f);
            var no = Instantiate(prefab, marker.position, rot);
            no.Spawn(true);
            return no;
        }

        static void Init(NetworkObject no)
        {
            var enemy = no.GetComponent<Enemy>();
            if (enemy != null) enemy.ServerInit(TimeLines.I.LineOf(no.transform.position));
        }

        // Сопряжение: убитые Героем 2 мутанты встают для Героя 1.
        void OnSwapped()
        {
            foreach (var e in Enemy.All)
                if (e.IsSpawned) e.ServerOnSwapped();
        }

        // Рестарт комнаты: все враги в исходное (убитые встают, «мёртвые до нас» снова лежат с пулей).
        void OnRoomRestart()
        {
            foreach (var e in Enemy.All)
                if (e.IsSpawned) e.ServerReset();
        }
    }
}
