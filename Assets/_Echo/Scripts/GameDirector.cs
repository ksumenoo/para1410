using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;

namespace Echo
{
    // Сервер: создаёт героев, предметы, двери и охрану по меткам, раздаёт героев игрокам,
    // ведёт чекпоинты и рестарт комнаты, следит за сопряжением у реактора.
    public class GameDirector : MonoBehaviour
    {
        public static GameDirector I { get; private set; }

        public NetworkObject heroPrefab;
        public NetworkObject itemPrefab;
        public NetworkObject doorPrefab;
        public NetworkObject guardPrefab;

        [Header("Сопряжение")]
        public float reactorRadius = 2.8f;
        public float reactorHoldSeconds = 2f;

        public bool Swapped { get; private set; }
        public bool Restarting { get; private set; }

        NetworkManager _nm;
        readonly List<Transform> _reactors = new List<Transform>();
        readonly List<Vector3> _checkpoints = new List<Vector3>();   // локальные позиции, по возрастанию x
        readonly Dictionary<ulong, int> _reached = new Dictionary<ulong, int>();
        float _hold;

        void Awake() => I = this;

        void Start()
        {
            _nm = NetworkManager.Singleton;
            _nm.OnServerStarted += OnServerStarted;
            _nm.OnClientConnectedCallback += OnClientConnected;
        }

        void OnDestroy()
        {
            if (_nm == null) return;
            _nm.OnServerStarted -= OnServerStarted;
            _nm.OnClientConnectedCallback -= OnClientConnected;
        }

        void OnServerStarted()
        {
            Swapped = false;
            Restarting = false;
            _hold = 0f;
            _reached.Clear();
            var lines = TimeLines.I;

            // Навигация для охраны и мутантов — печём на сервере до появления героев.
            foreach (var surface in FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
                surface.BuildNavMesh();

            for (int id = 1; id <= 2; id++)
            {
                var pos = lines.ToWorld(lines.startLocal, id);
                var no = Instantiate(heroPrefab, pos, Quaternion.identity);
                no.SpawnWithOwnership(NetworkManager.ServerClientId, true);
                var hero = no.GetComponent<Hero>();
                hero.HeroIdVar.Value = id;
                hero.Line.Value = id;
            }

            foreach (var m in FindObjectsByType<ItemMarker>(FindObjectsSortMode.None))
            {
                int line = lines.LineOf(m.transform.position);
                ItemSystem.Spawn(m.type, m.keyId, line, lines.ToLocal(m.transform.position, line), false, 0);
            }

            foreach (var d in FindObjectsByType<DoorMarker>(FindObjectsSortMode.None))
            {
                var no = Instantiate(doorPrefab, d.transform.position, d.transform.rotation);
                no.Spawn(true);
                var door = no.GetComponent<Door>();
                door.DoorId.Value = d.doorId;
                door.KeyId.Value = d.keyId;
                door.Line.Value = lines.LineOf(d.transform.position);
            }

            if (guardPrefab != null)
                foreach (var g in FindObjectsByType<GuardMarker>(FindObjectsSortMode.None))
                {
                    var no = Instantiate(guardPrefab, g.transform.position, g.transform.rotation);
                    no.Spawn(true);
                    var patrol = new List<Vector3> { g.transform.position };
                    foreach (Transform p in g.transform) patrol.Add(p.position);
                    no.GetComponent<Guard>().ServerInit(lines.LineOf(g.transform.position), patrol, g.soldier);
                }

            _reactors.Clear();
            _reactors.AddRange(FindObjectsByType<ReactorMarker>(FindObjectsSortMode.None).Select(r => r.transform));

            _checkpoints.Clear();
            _checkpoints.Add(lines.startLocal);
            foreach (var c in FindObjectsByType<CheckpointMarker>(FindObjectsSortMode.None))
            {
                int line = lines.LineOf(c.transform.position);
                var local = lines.ToLocal(c.transform.position, line);
                local.y = lines.startLocal.y;
                _checkpoints.Add(local);
            }
            _checkpoints.Sort((a, b) => a.x.CompareTo(b.x));

            GameEvents.RaiseGameStarted();
        }

        void OnClientConnected(ulong clientId)
        {
            if (!_nm.IsServer || clientId == NetworkManager.ServerClientId) return;
            int clientHero = GameSession.HostHeroChoice == 1 ? 2 : 1;
            var hero = Hero.Get(clientHero);
            if (hero != null && hero.OwnerClientId == NetworkManager.ServerClientId)
                hero.NetworkObject.ChangeOwnership(clientId);
        }

        void Update()
        {
            if (_nm == null || !_nm.IsServer || !_nm.IsListening) return;
            TrackCheckpoints();
            if (!Swapped) UpdateReactor();
        }

        // ---------- Чекпоинты и рестарт ----------

        void TrackCheckpoints()
        {
            foreach (var h in Hero.All)
            {
                if (!h.IsSpawned) continue;
                float x = h.LocalPos.x;
                _reached.TryGetValue(h.NetworkObjectId, out int idx);
                while (idx + 1 < _checkpoints.Count && x >= _checkpoints[idx + 1].x - 0.5f) idx++;
                _reached[h.NetworkObjectId] = idx;
            }
        }

        Vector3 CheckpointFor(Hero h)
        {
            _reached.TryGetValue(h.NetworkObjectId, out int idx);
            return _checkpoints.Count > 0 ? _checkpoints[Mathf.Clamp(idx, 0, _checkpoints.Count - 1)] : TimeLines.I.startLocal;
        }

        public void ServerCaught(Hero hero)
        {
            if (Restarting) return;
            StartCoroutine(RestartRoom("Герой " + hero.HeroId + " пойман. Комната заново."));
        }

        public void ServerKilled(Hero hero)
        {
            if (Restarting) return;
            StartCoroutine(RestartRoom("Герой " + hero.HeroId + " погиб. Комната заново."));
        }

        IEnumerator RestartRoom(string message)
        {
            Restarting = true;
            foreach (var h in Hero.All) h.ToastRpc(message);
            yield return new WaitForSeconds(0.8f);
            foreach (var h in Hero.All)
            {
                h.HidingIn.Value = -1;
                h.TeleportRpc(TimeLines.I.ToWorld(CheckpointFor(h), h.Line.Value));
            }
            foreach (var g in Guard.All) g.ServerReset();
            GameEvents.RaiseRoomRestart();
            _hold = 0f;
            yield return new WaitForSeconds(0.3f);
            Restarting = false;
        }

        // ---------- Сопряжение ----------

        void UpdateReactor()
        {
            var h1 = Hero.Get(1);
            var h2 = Hero.Get(2);
            if (h1 == null || h2 == null) return;

            bool ready = h1.Holding && h2.Holding && NearReactor(h1) && NearReactor(h2);
            _hold = ready ? _hold + Time.deltaTime : Mathf.Max(0f, _hold - Time.deltaTime * 2f);
            float progress = Mathf.Clamp01(_hold / reactorHoldSeconds);
            SetProgress(h1, progress);
            SetProgress(h2, progress);

            if (_hold >= reactorHoldSeconds) DoSwap(h1, h2);
        }

        static void SetProgress(Hero h, float p)
        {
            if (Mathf.Abs(h.ReactorProgress.Value - p) > 0.02f || (p == 0f && h.ReactorProgress.Value != 0f))
                h.ReactorProgress.Value = p;
        }

        bool NearReactor(Hero h)
        {
            foreach (var r in _reactors)
                if (TimeLines.I.LineOf(r.position) == h.Line.Value &&
                    Hero.FlatDistance(r.position, h.transform.position) < reactorRadius)
                    return true;
            return false;
        }

        void DoSwap(Hero h1, Hero h2)
        {
            Swapped = true;
            _hold = 0f;
            foreach (var h in new[] { h1, h2 })
            {
                int newLine = 3 - h.Line.Value;
                var local = TimeLines.I.ToLocal(h.transform.position, h.Line.Value);
                h.Line.Value = newLine;
                h.ReactorProgress.Value = 0f;
                h.HidingIn.Value = -1;
                h.TeleportRpc(TimeLines.I.ToWorld(local, newLine));
                h.ToastRpc("Сопряжение! Вы поменялись местами.");
            }
            foreach (var g in Guard.All) g.ServerReset();
            GameEvents.RaiseSwapped();
        }
    }
}
