using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Echo
{
    public enum GuardState { Patrol = 0, Suspicious = 1, Chase = 2, Search = 3, Investigate = 4 }

    // Охрана (зона персонала) и военные (закрытая зона). Логика только на сервере,
    // клиенты видят позицию через NetworkTransform и состояние через NetworkVariable.
    [RequireComponent(typeof(NavMeshAgent))]
    public class Guard : NetworkBehaviour
    {
        public static readonly List<Guard> All = new List<Guard>();

        public readonly NetworkVariable<int> State = new NetworkVariable<int>(0);
        public readonly NetworkVariable<bool> Soldier = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<float> Awareness = new NetworkVariable<float>(0f);
        public readonly NetworkVariable<bool> Stunned = new NetworkVariable<bool>(false);

        [Header("Зрение")]
        public float guardRange = 8f;
        public float guardAngle = 70f;
        public float soldierRange = 11f;
        public float soldierAngle = 100f;
        public float sneakFactor = 0.6f;

        [Header("Движение")]
        public float walkSpeed = 2f;
        public float runSpeed = 4.3f;
        public float catchRadius = 1.1f;

        [Header("Вид")]
        public Renderer bodyRenderer;
        [Tooltip("Спрайт художника (если есть): заменяет капсулу, повёрнут к камере")]
        public SpriteRenderer spriteRenderer;
        public Sprite guardSprite;
        public Sprite soldierSprite;
        public float cameraPitch = 50f;
        public Material guardMaterial;
        public Material soldierMaterial;
        public Renderer alertIcon;
        public Material suspiciousMaterial;
        public Material chaseMaterial;
        public MeshFilter coneFilter;
        public MeshRenderer coneRenderer;
        public Material coneCalmMaterial;
        public Material coneAlertMaterial;

        NavMeshAgent _agent;
        int _line;
        readonly List<Vector3> _patrol = new List<Vector3>();
        Vector3 _spawnPos;
        Quaternion _spawnRot;
        int _waypoint;
        float _timer;
        float _lostTimer;
        Vector3 _lastSeen;
        Vector3 _investigatePos;
        Hero _target;
        bool _coneSoldier;
        float _stunUntil;

        public float Range => Soldier.Value ? soldierRange : guardRange;
        public float Angle => Soldier.Value ? soldierAngle : guardAngle;
        public GuardState Current => (GuardState)State.Value;

        void Awake() => _agent = GetComponent<NavMeshAgent>();

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (!IsServer) _agent.enabled = false;
            Soldier.OnValueChanged += (_, __) => ApplyLook();
            ApplyLook();
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        public void ServerInit(int line, IEnumerable<Vector3> patrol, bool soldier)
        {
            _line = line;
            _patrol.Clear();
            _patrol.AddRange(patrol);
            Soldier.Value = soldier;
            _spawnPos = transform.position;
            _spawnRot = transform.rotation;
            _agent.enabled = true;
            ServerReset();
        }

        public void ServerReset()
        {
            if (_agent.enabled) _agent.Warp(_spawnPos);
            transform.rotation = _spawnRot;
            _waypoint = 0;
            _timer = 0f;
            _target = null;
            _stunUntil = 0f;
            Stunned.Value = false;
            Awareness.Value = 0f;
            SetState(GuardState.Patrol);
        }

        // ---------- Вид (все клиенты) ----------

        void ApplyLook()
        {
            if (bodyRenderer) bodyRenderer.sharedMaterial = Soldier.Value ? soldierMaterial : guardMaterial;
            var sprite = Soldier.Value ? soldierSprite : guardSprite;
            if (spriteRenderer) spriteRenderer.sprite = sprite;
            bool useSprite = spriteRenderer != null && sprite != null;
            if (spriteRenderer) spriteRenderer.enabled = useSprite;
            if (bodyRenderer) bodyRenderer.enabled = !useSprite;
            BuildCone();
        }

        void BuildCone()
        {
            if (coneFilter == null) return;
            _coneSoldier = Soldier.Value;
            const int segments = 24;
            float half = Angle * 0.5f * Mathf.Deg2Rad;
            var verts = new Vector3[segments + 2];
            var tris = new int[segments * 3];
            verts[0] = new Vector3(0f, 0.06f, 0f);
            for (int i = 0; i <= segments; i++)
            {
                float a = -half + 2f * half * i / segments;
                verts[i + 1] = new Vector3(Mathf.Sin(a) * Range, 0.06f, Mathf.Cos(a) * Range);
            }
            for (int i = 0; i < segments; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }
            var mesh = new Mesh { name = "VisionCone", vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            coneFilter.sharedMesh = mesh;
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            if (_coneSoldier != Soldier.Value) { BuildCone(); ApplyLook(); }
            if (spriteRenderer && spriteRenderer.enabled)
            {
                var t = spriteRenderer.transform;
                t.rotation = Quaternion.Euler(cameraPitch, 0f, 0f);
                float fx = transform.forward.x;
                if (Mathf.Abs(fx) > 0.1f)
                {
                    var sc = t.localScale;
                    sc.x = Mathf.Abs(sc.x) * (fx < 0f ? -1f : 1f);
                    t.localScale = sc;
                }
            }
            var state = Current;
            if (alertIcon)
            {
                bool on = state == GuardState.Suspicious || state == GuardState.Chase || state == GuardState.Search;
                if (alertIcon.gameObject.activeSelf != on) alertIcon.gameObject.SetActive(on);
                if (on) alertIcon.sharedMaterial = state == GuardState.Chase ? chaseMaterial : suspiciousMaterial;
            }
            if (coneRenderer && coneRenderer.enabled == Stunned.Value) coneRenderer.enabled = !Stunned.Value;
            if (coneRenderer)
                coneRenderer.sharedMaterial = state == GuardState.Patrol ? coneCalmMaterial : coneAlertMaterial;
        }

        // ---------- Логика (сервер) ----------

        void Update()
        {
            if (!IsServer || !IsSpawned || !_agent.enabled || !_agent.isOnNavMesh) return;
            if (GameDirector.I != null && GameDirector.I.Restarting) return;
            if (GameEvents.EnemiesPaused) { _agent.isStopped = true; return; }
            if (Stunned.Value)
            {
                if (Time.time < _stunUntil) { _agent.isStopped = true; return; }
                Stunned.Value = false;
                _agent.isStopped = false;
                BeginSearch(transform.position);
            }

            Hero seen = null;
            float seenDist = float.MaxValue;
            foreach (var h in Hero.All)
            {
                if (!h.IsSpawned || h.Line.Value != _line || h.HidingIn.Value >= 0) continue;
                if (CanSee(h, out float d) && d < seenDist) { seen = h; seenDist = d; }
            }
            if (seen != null) { _lastSeen = seen.transform.position; _target = seen; }

            // Поймал: любой незаспрятанный герой вплотную
            foreach (var h in Hero.All)
            {
                if (!h.IsSpawned || h.Line.Value != _line || h.HidingIn.Value >= 0) continue;
                if (Hero.FlatDistance(h.transform.position, transform.position) < catchRadius &&
                    (Current == GuardState.Chase || seen == h))
                {
                    GameDirector.I.ServerCaught(h);
                    return;
                }
            }

            float dt = Time.deltaTime;
            switch (Current)
            {
                case GuardState.Chase:
                    _agent.isStopped = false;
                    _agent.speed = runSpeed;
                    _agent.SetDestination(_lastSeen);
                    _lostTimer = seen != null ? 0f : _lostTimer + dt;
                    if (_lostTimer > 2.5f) BeginSearch(_lastSeen);
                    break;

                default:
                    if (seen != null)
                    {
                        float near = seenDist < Range * 0.35f ? 3f : 1.3f;
                        Awareness.Value = Mathf.Min(1f, Awareness.Value + dt * near);
                        if (Current != GuardState.Suspicious) SetState(GuardState.Suspicious);
                        _agent.isStopped = true;
                        Face(_lastSeen, dt);
                        if (Awareness.Value >= 1f) BeginChase();
                        break;
                    }

                    if (Awareness.Value > 0f) Awareness.Value = Mathf.Max(0f, Awareness.Value - dt * 0.4f);
                    if (Current == GuardState.Suspicious)
                    {
                        if (Awareness.Value <= 0.05f) BeginInvestigate(_lastSeen);
                        break;
                    }
                    if (Current == GuardState.Search || Current == GuardState.Investigate)
                    {
                        UpdateLookAround(dt, Current == GuardState.Search ? 4f : 3f);
                        break;
                    }
                    UpdatePatrol(dt);
                    break;
            }
        }

        bool CanSee(Hero h, out float dist)
        {
            Vector3 eye = transform.position + Vector3.up * 1.5f;
            Vector3 to = h.transform.position + Vector3.up * 1.1f - eye;
            dist = to.magnitude;
            float range = Range * (h.Sneaking.Value ? sneakFactor : 1f);
            if (Current == GuardState.Chase) range *= 1.4f;
            if (dist > range) return false;

            var flat = new Vector3(to.x, 0f, to.z);
            bool close = dist < 1.6f;
            if (!close && Vector3.Angle(transform.forward, flat) > Angle * 0.5f) return false;

            // Стекло лежит в слое Ignore Raycast: сквозь него охрана видит.
            var hits = Physics.RaycastAll(eye, to / dist, dist - 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<Hero>() != null) continue;
                if (hit.collider.GetComponentInParent<Guard>() != null) continue;
                return false;
            }
            return true;
        }

        void UpdatePatrol(float dt)
        {
            _agent.speed = walkSpeed;
            if (_patrol.Count == 0)
            {
                _agent.isStopped = true;
                return;
            }
            _agent.isStopped = false;
            var dest = _patrol[_waypoint % _patrol.Count];
            if (!_agent.pathPending && (_agent.destination - dest).sqrMagnitude > 0.1f) _agent.SetDestination(dest);
            if (!_agent.pathPending && _agent.remainingDistance < 0.4f)
            {
                _timer += dt;
                if (_timer > 1.2f)
                {
                    _timer = 0f;
                    _waypoint = (_waypoint + 1) % _patrol.Count;
                }
            }
        }

        void UpdateLookAround(float dt, float duration)
        {
            _agent.isStopped = false;
            _agent.speed = Current == GuardState.Search ? 3f : walkSpeed;
            if (!_agent.pathPending && (_agent.destination - _investigatePos).sqrMagnitude > 0.1f)
                _agent.SetDestination(_investigatePos);
            if (!_agent.pathPending && _agent.remainingDistance < 0.6f)
            {
                _timer += dt;
                transform.Rotate(0f, 120f * dt * Mathf.Sign(Mathf.Sin(_timer * 1.5f)), 0f);
                if (_timer > duration) { _timer = 0f; SetState(GuardState.Patrol); }
            }
        }

        void Face(Vector3 point, float dt)
        {
            var dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 360f * dt);
        }

        void BeginChase()
        {
            SetState(GuardState.Chase);
            _lostTimer = 0f;
            if (_target != null) _target.ToastRpc(Soldier.Value ? "Военный заметил тебя!" : "Охрана заметила тебя! Беги или прячься.");
            // Тревога: ближние посты идут на шум
            EmitNoise(_line, _lastSeen, 12f, this);
        }

        void BeginSearch(Vector3 pos)
        {
            _investigatePos = pos;
            _timer = 0f;
            Awareness.Value = 0f;
            SetState(GuardState.Search);
        }

        public void BeginInvestigate(Vector3 pos)
        {
            _investigatePos = pos;
            _timer = 0f;
            SetState(GuardState.Investigate);
        }

        void SetState(GuardState s)
        {
            if (State.Value != (int)s) State.Value = (int)s;
            if (_agent.enabled) _agent.isStopped = false;
        }

        // Оглушение (пуля, удар): стоит на месте, конус гаснет, потом ищет вокруг.
        public void ServerStun(float seconds)
        {
            if (!IsServer) return;
            _stunUntil = Mathf.Max(_stunUntil, Time.time + seconds);
            Stunned.Value = true;
            Awareness.Value = 0f;
            if (_agent.enabled) _agent.isStopped = true;
        }

        // Шум (брошенный предмет, тревога): охрана на этой линии идёт проверить.
        public static void EmitNoise(int line, Vector3 pos, float radius, Guard except = null)
        {
            foreach (var g in All)
            {
                if (g == except || !g.IsSpawned || g._line != line) continue;
                if (g.Current == GuardState.Chase) continue;
                if (Hero.FlatDistance(g.transform.position, pos) <= radius) g.BeginInvestigate(pos);
            }
        }
    }
}
