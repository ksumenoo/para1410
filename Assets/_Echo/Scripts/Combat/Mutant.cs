using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Echo.Combat
{
    // Мутант (бывший сотрудник): бродит у своего места, замечает героя своей линии (сквозь стены не видит),
    // бежит к нему и бьёт при касании.
    // Убитый героем лежит насовсем (до рестарта комнаты); инверсная пуля остаётся в трупе.
    // «Мёртвый до нас» (startDead): лежит с пулей; Герой 2 с пистолетом подходит ближе wakeRadius —
    // пуля вылетает в его обойму, мутант встаёт (смерть задом наперёд) и нападает.
    [RequireComponent(typeof(NavMeshAgent))]
    public class Mutant : Enemy
    {
        public readonly NetworkVariable<bool> Chasing = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> HoldsBullet = new NetworkVariable<bool>(false);

        // Баланс (в коде, см. Enemy). Герой ходит 5 м/с — от бегущего мутанта можно уйти.
        [System.NonSerialized] public float sightRange = 6f;
        [System.NonSerialized] public float walkSpeed = 1.2f;
        [System.NonSerialized] public float runSpeed = 3.6f;
        [System.NonSerialized] public float wanderRadius = 2f;
        [System.NonSerialized] public float loseSeconds = 3f;
        [System.NonSerialized] public float calmSeconds = 2f;       // после старта/рестарта не замечает героев
        [System.NonSerialized] public float swapRiseSeconds = 1.5f; // встаёт при сопряжении (убит Героем 2)

        // «Мёртвый до нас»
        [System.NonSerialized] public bool startDead;               // ставит CombatDirector из MutantMarker
        [System.NonSerialized] public float wakeRadius = 5f;
        [System.NonSerialized] public int wakeHeroId = 2;
        [System.NonSerialized] public float wakeRiseSeconds = 1.5f;

        [Header("Вид (заглушка-капсула)")]
        public Transform pivot;
        public Renderer bodyRenderer;
        public Material calmMaterial;
        public Material angryMaterial;

        [Header("Спрайты художника (если есть — вместо капсулы)")]
        public SpriteRenderer spriteRenderer;
        public Sprite aliveSprite;     // enm_mutant: лицом вправо, ноги у нижнего края
        public Sprite deadSprite;      // enm_mutant_dead: лежащий труп
        public float cameraPitch = 50f;

        static readonly Color AngryTint = new Color(1f, 0.62f, 0.55f);

        NavMeshAgent _agent;
        bool _dormant;
        float _wanderTimer;
        float _calm;
        float _lostTimer;
        Vector3 _lastSeen;
        float _fall;
        float _riseSpeed = 1f;
        float _wobble;
        float _spawnTime;
        bool _angryShown;
        bool _useSprite;
        GameObject _bulletVisual;

        protected override string DamageMessage => "Мутант ранил тебя!";
        protected override bool RevivesAfterKill => false;
        public bool Dormant => _dormant; // сервер

        // Где сидит пуля: грудь (у капсулы — вместе с телом поворачивается при падении).
        public Vector3 BulletPos => _useSprite
            ? spriteRenderer.transform.TransformPoint(SpriteBulletLocal(_fall > 0.5f))
            : pivot != null ? pivot.TransformPoint(new Vector3(0f, 1.15f, 0.36f)) : transform.position + Vector3.up;

        // Грудь в координатах спрайта (1 м = 110 px от пивота внизу по центру), чуть ближе к камере.
        // Стоит: грудь над пивотом; лежит (enm_mutant_dead, голова вправо): правее центра у пола.
        static Vector3 SpriteBulletLocal(bool lying) => lying ? new Vector3(0.2f, 0.36f, -0.06f) : new Vector3(0f, 1.15f, -0.06f);

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _useSprite = spriteRenderer != null && aliveSprite != null;
            if (spriteRenderer) spriteRenderer.enabled = _useSprite;
            // Со спрайтом капсулу прячем, но pivot оставляем: по нему считается анимация падения.
            if (_useSprite && pivot != null)
                foreach (var r in pivot.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _spawnTime = Time.time;
            if (!IsServer) _agent.enabled = false;
        }

        // ---------- Сервер ----------

        protected override void OnServerInit() => _agent.enabled = true;

        protected override void OnServerReset()
        {
            Chasing.Value = false;
            _lostTimer = 0f;
            _calm = calmSeconds;
            _wanderTimer = Random.Range(1f, 3f);
            if (_agent.enabled)
            {
                _agent.Warp(_spawnPos);
                if (_agent.isOnNavMesh) _agent.ResetPath();
            }
            else transform.position = _spawnPos;
            transform.rotation = _spawnRot;

            _dormant = startDead;
            HoldsBullet.Value = startDead;
            if (startDead)
            {
                Dead.Value = true;
                Stop();
            }
        }

        protected override void OnServerDied()
        {
            Chasing.Value = false;
            Stop();
        }

        public override bool ServerLodgeBullet()
        {
            if (!Dead.Value) return false;
            HoldsBullet.Value = true;
            return true;
        }

        protected override void OnServerFrozen() => Stop();

        protected override void OnServerHitHero(Hero h)
        {
            if (_agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
        }

        // Сопряжение: на линию 2 приходит Герой 1 (вперёд во времени). Для него убитые Героем 2 ещё живы —
        // встают и ходят как обычные. «Мёртвые до нас» остаются трупами (будят их только пули Героя 2).
        public override void ServerOnSwapped()
        {
            if (!IsServer) return;
            if (!Dead.Value)
            {
                // Живые не набрасываются на только что пришедшего героя.
                Chasing.Value = false;
                Target = null;
                _calm = Mathf.Max(_calm, SwapCalmSeconds);
                return;
            }
            if (_dormant || KilledByHeroId != wakeHeroId) return;
            HoldsBullet.Value = false; // пулю вернул HeroGun.ServerOnSwapped
            KilledByHeroId = 0;
            Chasing.Value = false;
            Target = null;
            _calm = swapRiseSeconds + 1f;
            _wanderTimer = 0.5f;
            ServerRevive(swapRiseSeconds);
            WakeFxRpc(swapRiseSeconds);
        }

        void Stop()
        {
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
        }

        // Труп «мёртвого до нас» ждёт Героя 2.
        protected override void ServerDeadTick(float dt)
        {
            if (!_dormant) return;
            foreach (var h in Hero.All)
            {
                if (!h.IsSpawned || h.HeroId != wakeHeroId || h.Line.Value != _line || h.IsHiding) continue;
                if (Hero.FlatDistance(h.transform.position, transform.position) > wakeRadius) continue;
                var gun = h.GetComponent<HeroGun>();
                if (gun == null || !gun.HasPistol) continue;
                ServerWake(h, gun);
                return;
            }
        }

        void ServerWake(Hero hero, HeroGun gun)
        {
            _dormant = false;
            HoldsBullet.Value = false;
            gun.ServerCatchBullet(BulletPos);
            ServerRevive(wakeRiseSeconds);
            WakeFxRpc(wakeRiseSeconds);
            // Встал — сразу знает, где герой.
            _calm = 0f;
            Target = hero;
            _lastSeen = hero.transform.position;
            _lostTimer = 0f;
            Chasing.Value = true;
        }

        // Отладка (пульт боя): превратить мутанта в «мёртвого до нас».
        public void DebugMakeDormant()
        {
            if (!IsServer) return;
            startDead = true;
            ServerReset();
        }

        protected override void ServerTick(float dt)
        {
            if (!_agent.enabled || !_agent.isOnNavMesh) return;
            if (_calm > 0f) _calm -= dt;

            var seen = _calm > 0f ? null : FindVisibleHero(sightRange * (Chasing.Value ? 1.5f : 1f));
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

        [Rpc(SendTo.ClientsAndHost)]
        void WakeFxRpc(float rise) => _riseSpeed = 1f / Mathf.Max(0.1f, rise);

        void LateUpdate()
        {
            if (!IsSpawned) return;
            float dt = Time.deltaTime;
            bool dead = Dead.Value;
            bool angry = Chasing.Value && !dead;

            // Падает быстро, встаёт медленно — смерть задом наперёд. Сразу после появления — без анимации.
            if (Time.time - _spawnTime < 0.5f) _fall = dead ? 1f : 0f;
            if (!dead && _fall <= 0f) _riseSpeed = 1f / Mathf.Max(0.1f, riseSeconds);
            _fall = Mathf.MoveTowards(_fall, dead ? 1f : 0f, dt * (dead ? 3f : _riseSpeed));
            float eased = 1f - (1f - _fall) * (1f - _fall);
            _wobble += dt * (angry ? 10f : 3f);
            float sway = dead ? 0f : Mathf.Sin(_wobble) * (angry ? 7f : 4f);
            if (pivot) pivot.localRotation = Quaternion.Euler(-85f * eased, 0f, sway * (1f - _fall));

            if (_useSprite) UpdateSprite(angry, sway);
            else if (bodyRenderer && angry != _angryShown && calmMaterial && angryMaterial)
            {
                _angryShown = angry;
                bodyRenderer.sharedMaterial = angry ? angryMaterial : calmMaterial;
            }

            UpdateBulletVisual();
        }

        // Спрайт: к камере, отражение по направлению, труп — enm_mutant_dead.
        // Встаёт «задом наперёд»: сплющенный труп вытягивается в стоящего.
        void UpdateSprite(bool angry, float sway)
        {
            var t = spriteRenderer.transform;
            bool lying = _fall > 0.5f;
            var sprite = lying && deadSprite != null ? deadSprite : aliveSprite;
            if (spriteRenderer.sprite != sprite) spriteRenderer.sprite = sprite;

            float roll = lying && deadSprite == null ? 90f : sway;    // нет спрайта трупа — кладём живого на бок
            t.rotation = Quaternion.Euler(cameraPitch, 0f, 0f) * Quaternion.Euler(0f, 0f, roll);
            float squash = lying ? 1f : 1f - 0.5f * Mathf.Clamp01(_fall * 2f);
            float fx = transform.forward.x;
            float sx = Mathf.Abs(t.localScale.x);
            if (Mathf.Abs(fx) > 0.1f) sx *= fx < 0f ? -1f : 1f;
            else sx *= Mathf.Sign(t.localScale.x);
            t.localScale = new Vector3(sx, squash, 1f);
            spriteRenderer.color = angry ? AngryTint : Color.white;
        }

        // Пуля в трупе: светится и подрагивает.
        void UpdateBulletVisual()
        {
            bool show = HoldsBullet.Value;
            if (show && _bulletVisual == null && (pivot != null || _useSprite))
            {
                _bulletVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _bulletVisual.name = "LodgedBullet";
                Destroy(_bulletVisual.GetComponent<Collider>());
                _bulletVisual.transform.SetParent(_useSprite ? spriteRenderer.transform : pivot, false);
                _bulletVisual.transform.localPosition = _useSprite ? SpriteBulletLocal(_fall > 0.5f) : new Vector3(0f, 1.15f, 0.36f);
                var r = _bulletVisual.GetComponent<Renderer>();
                r.sharedMaterial = CombatFx.BulletMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (_bulletVisual == null) return;
            if (_bulletVisual.activeSelf != show) _bulletVisual.SetActive(show);
            if (_useSprite) _bulletVisual.transform.localPosition = SpriteBulletLocal(_fall > 0.5f);
            if (show) _bulletVisual.transform.localScale = Vector3.one * (0.17f + 0.03f * Mathf.Sin(Time.time * 9f));
        }
    }
}
