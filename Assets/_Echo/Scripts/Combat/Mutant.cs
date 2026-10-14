using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Echo.Combat
{
    // Мутант (бывший сотрудник): бродит у своего места, замечает героя своей линии (сквозь стены не видит),
    // бежит к нему и бьёт при касании. Убит — лежит 4 с и встаёт.
    [RequireComponent(typeof(NavMeshAgent))]
    public class Mutant : Enemy
    {
        public readonly NetworkVariable<bool> Chasing = new NetworkVariable<bool>(false);

        [Header("Мутант")]
        public float sightRange = 7f;
        public float walkSpeed = 1.3f;
        public float runSpeed = 4f;
        public float wanderRadius = 3.5f;
        public float loseSeconds = 3f;

        [Header("Вид (заглушка)")]
        public Transform pivot;
        public Renderer bodyRenderer;
        public Material calmMaterial;
        public Material angryMaterial;

        NavMeshAgent _agent;
        float _wanderTimer;
        float _lostTimer;
        Vector3 _lastSeen;
        float _fall;
        float _wobble;
        bool _angryShown;

        protected override string DamageMessage => "Мутант ранил тебя!";

        void Awake() => _agent = GetComponent<NavMeshAgent>();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) _agent.enabled = false;
        }

        // ---------- Сервер ----------

        protected override void OnServerInit() => _agent.enabled = true;

        protected override void OnServerReset()
        {
            Chasing.Value = false;
            _lostTimer = 0f;
            _wanderTimer = Random.Range(1f, 3f);
            if (_agent.enabled)
            {
                _agent.Warp(_spawnPos);
                if (_agent.isOnNavMesh) _agent.ResetPath();
            }
            else transform.position = _spawnPos;
            transform.rotation = _spawnRot;
        }

        protected override void OnServerDied()
        {
            Chasing.Value = false;
            Stop();
        }

        protected override void OnServerFrozen() => Stop();

        void Stop()
        {
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
        }

        protected override void ServerTick(float dt)
        {
            if (!_agent.enabled || !_agent.isOnNavMesh) return;

            var seen = FindVisibleHero(sightRange * (Chasing.Value ? 1.5f : 1f));
            if (seen != null)
            {
                Target = seen;
                _lastSeen = seen.transform.position;
                _lostTimer = 0f;
                if (!Chasing.Value) Chasing.Value = true;
            }

            if (Chasing.Value)
            {
                _agent.isStopped = false;
                _agent.speed = runSpeed;
                _agent.SetDestination(_lastSeen);
                if (seen == null)
                {
                    _lostTimer += dt;
                    bool arrived = !_agent.pathPending && _agent.remainingDistance < 0.6f;
                    if (_lostTimer > loseSeconds || (arrived && _lostTimer > 1f))
                    {
                        Chasing.Value = false;
                        Target = null;
                        _wanderTimer = 1.5f;
                    }
                }
                return;
            }

            // Бродит рядом с местом появления, иногда стоит.
            _agent.speed = walkSpeed;
            if (_agent.pathPending || _agent.remainingDistance > 0.3f) return;
            _wanderTimer -= dt;
            if (_wanderTimer > 0f) return;
            _wanderTimer = Random.Range(1.5f, 4f);
            var p = _spawnPos + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * wanderRadius;
            if (NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas))
            {
                _agent.isStopped = false;
                _agent.SetDestination(hit.position);
            }
        }

        // ---------- Вид (все клиенты) ----------

        void LateUpdate()
        {
            if (!IsSpawned) return;
            float dt = Time.deltaTime;
            bool dead = Dead.Value;
            bool angry = Chasing.Value && !dead;

            // Падает быстро, встаёт медленно — как смерть задом наперёд.
            _fall = Mathf.MoveTowards(_fall, dead ? 1f : 0f, dt * (dead ? 3f : 1f / Mathf.Max(0.1f, riseSeconds)));
            float eased = 1f - (1f - _fall) * (1f - _fall);
            _wobble += dt * (angry ? 10f : 3f);
            float sway = dead ? 0f : Mathf.Sin(_wobble) * (angry ? 7f : 4f);
            if (pivot) pivot.localRotation = Quaternion.Euler(-85f * eased, 0f, sway * (1f - _fall));

            if (bodyRenderer && angry != _angryShown && calmMaterial && angryMaterial)
            {
                _angryShown = angry;
                bodyRenderer.sharedMaterial = angry ? angryMaterial : calmMaterial;
            }
        }
    }
}
