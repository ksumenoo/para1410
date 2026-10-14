using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Echo.Combat
{
    // Сервер: спавнит мутантов и сгустки по меткам, возвращает их на рестарте комнаты, оглушает охрану.
    // На клиентах только хранит материалы эффектов.
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

        // Если в ядре появится Guard.ServerStun(float) — используем его, иначе оглушаем сами (выключаем NavMeshAgent).
        static readonly MethodInfo NativeStun = typeof(Guard).GetMethod("ServerStun",
            BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(float) }, null);

        readonly Dictionary<Guard, float> _stunned = new Dictionary<Guard, float>();
        readonly List<Guard> _expired = new List<Guard>();

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

        bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        void OnGameStarted()
        {
            _stunned.Clear();
            if (TimeLines.I == null) return;
            if (mutantPrefab != null)
                foreach (var m in FindObjectsByType<MutantMarker>(FindObjectsSortMode.None))
                    SpawnEnemy(mutantPrefab, m.transform);
            if (timeClotPrefab != null)
                foreach (var c in FindObjectsByType<TimeClotMarker>(FindObjectsSortMode.None))
                    SpawnEnemy(timeClotPrefab, c.transform);
        }

        void SpawnEnemy(NetworkObject prefab, Transform marker)
        {
            var pos = marker.position;
            var rot = Quaternion.Euler(0f, marker.eulerAngles.y, 0f);
            var no = Instantiate(prefab, pos, rot);
            no.Spawn(true);
            var enemy = no.GetComponent<Enemy>();
            if (enemy != null) enemy.ServerInit(TimeLines.I.LineOf(pos));
        }

        void OnRoomRestart()
        {
            UnstunAll(true);
            foreach (var e in Enemy.All)
                if (e.IsSpawned) e.ServerReset();
        }

        void OnSwapped() => UnstunAll(true);

        // ---------- Оглушение охраны ----------

        public bool ServerStunGuard(Guard g, float seconds)
        {
            if (!IsServer || g == null || !g.IsSpawned) return false;
            if (NativeStun != null)
            {
                NativeStun.Invoke(g, new object[] { seconds });
                return true;
            }
            var agent = g.GetComponent<NavMeshAgent>();
            if (agent == null) return false;
            // Guard.Update ничего не делает при выключенном агенте: не ходит, не видит, не ловит.
            if (agent.enabled)
            {
                if (agent.isOnNavMesh) agent.ResetPath();
                agent.enabled = false;
            }
            _stunned[g] = Time.time + seconds;
            return true;
        }

        void Update()
        {
            if (_stunned.Count == 0) return;
            _expired.Clear();
            foreach (var kv in _stunned)
                if (kv.Key == null || !kv.Key.IsSpawned || Time.time >= kv.Value) _expired.Add(kv.Key);
            foreach (var g in _expired)
            {
                _stunned.Remove(g);
                Unstun(g, false);
            }
        }

        void UnstunAll(bool reset)
        {
            if (_stunned.Count == 0) return;
            foreach (var g in new List<Guard>(_stunned.Keys)) Unstun(g, reset);
            _stunned.Clear();
        }

        static void Unstun(Guard g, bool reset)
        {
            if (g == null || !g.IsSpawned) return;
            var agent = g.GetComponent<NavMeshAgent>();
            if (agent != null && !agent.enabled) agent.enabled = true;
            // Ядро сбрасывает охрану до наших обработчиков, а у выключенного агента Warp не срабатывает — повторяем.
            if (reset) g.ServerReset();
        }
    }
}
