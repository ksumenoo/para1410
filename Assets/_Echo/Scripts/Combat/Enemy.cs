using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Echo.Combat
{
    // Общая часть мутанта и сгустка: линия, касание (урон), смерть. Сгусток воскресает через 4 с,
    // мутант, убитый героем, лежит до рестарта комнаты (см. Mutant).
    // Логика только на сервере, клиенты видят позицию через NetworkTransform и состояние через Dead.
    public abstract class Enemy : NetworkBehaviour
    {
        public static readonly List<Enemy> All = new List<Enemy>();

        public readonly NetworkVariable<bool> Dead = new NetworkVariable<bool>(false);

        // Баланс — в коде, не в префабе (префабы пересоздаёт шаг сборки, правки в инспекторе всё равно пропадут).
        [System.NonSerialized] public float reviveSeconds = 4f;   // сколько лежит убитым (если встаёт сам)
        [System.NonSerialized] public float riseSeconds = 0.8f;   // встаёт (стоит на месте, не бьёт)
        [System.NonSerialized] public float hitRadius = 0.6f;     // радиус попадания пули по XZ
        [System.NonSerialized] public float touchRadius = 1.0f;   // касание героя = удар
        [System.NonSerialized] public float touchCooldown = 1.5f;
        [System.NonSerialized] public float afterHitPause = 0.6f; // после удара замирает — герой успевает отскочить
        [System.NonSerialized] public float windupSeconds = 0.3f; // замах: стоит перед ударом, бьёт, только если герой ещё рядом

        protected int _line = 2;
        protected Vector3 _spawnPos;
        protected Quaternion _spawnRot = Quaternion.identity;
        protected float _rise;
        protected Hero Target;
        protected int KilledByHeroId;     // кто убил (0 — никто / «мёртвый до нас»)
        float _deadTimer;
        float _touchTimer;
        float _windup;
        Hero _windupTarget;

        // Только на сервере.
        public int Line => _line;
        public bool Alive => IsSpawned && !Dead.Value;
        public Vector3 Position => transform.position;

        protected virtual string DamageMessage => "Тебя ранили!";
        // Встаёт ли сам через reviveSeconds после убийства.
        protected virtual bool RevivesAfterKill => true;

        public override void OnNetworkSpawn() => All.Add(this);
        public override void OnNetworkDespawn() => All.Remove(this);

        // ---------- Сервер ----------

        public void ServerInit(int line)
        {
            _line = line;
            _spawnPos = transform.position;
            _spawnRot = transform.rotation;
            OnServerInit();
            ServerReset();
        }

        public void ServerReset()
        {
            if (!IsServer) return;
            Dead.Value = false;
            _deadTimer = 0f;
            _touchTimer = 0f;
            _rise = 0f;
            _windup = 0f;
            _windupTarget = null;
            Target = null;
            KilledByHeroId = 0;
            OnServerReset();
            var nt = GetComponent<NetworkTransform>();
            if (nt != null) nt.Teleport(transform.position, transform.rotation, transform.localScale);
        }

        public bool ServerKill(Hero by)
        {
            if (!IsServer || Dead.Value) return false;
            Dead.Value = true;
            _deadTimer = reviveSeconds;
            Target = null;
            KilledByHeroId = by != null ? by.HeroId : 0;
            OnServerDied();
            return true;
        }

        // Инверсная пуля попала в убитого: остаётся в нём? (мутант — да, сгусток — нет, пуля вернётся сама)
        public virtual bool ServerLodgeBullet() => false;

        protected void ServerRevive(float rise)
        {
            Dead.Value = false;
            _rise = rise;
            OnServerRevived();
        }

        protected virtual void ServerDeadTick(float dt)
        {
            if (!RevivesAfterKill) return;
            _deadTimer -= dt;
            if (_deadTimer <= 0f) ServerRevive(riseSeconds);
        }

        protected virtual void OnServerInit() { }
        protected virtual void OnServerReset() => transform.SetPositionAndRotation(_spawnPos, _spawnRot);
        protected virtual void OnServerDied() { }
        protected virtual void OnServerRevived() { }
        protected virtual void OnServerFrozen() { }
        protected virtual void OnServerHitHero(Hero h) { }

        // Сопряжение (вызывает CombatDirector). Передышка: пара секунд не замечает героев.
        public const float SwapCalmSeconds = 2f;
        public virtual void ServerOnSwapped() { }
        protected abstract void ServerTick(float dt);

        protected virtual void Update()
        {
            if (!IsServer || !IsSpawned) return;
            // Рестарт комнаты или пауза врагов (интро, GameEvents.EnemiesPaused): стоят, не бьют, трупы не просыпаются.
            if (GameEvents.EnemiesPaused || (GameDirector.I != null && GameDirector.I.Restarting)) { OnServerFrozen(); return; }
            float dt = Time.deltaTime;
            if (Dead.Value) { ServerDeadTick(dt); return; }
            if (_rise > 0f) { _rise -= dt; return; }
            if (_windup > 0f)
            {
                _windup -= dt;
                if (_windup <= 0f) ServerStrike();
                return;
            }
            ServerTick(dt);
            ServerTouch(dt);
        }

        bool InReach(Hero h) =>
            h != null && h.IsSpawned && h.Line.Value == _line && !h.IsHiding &&
            Mathf.Abs(h.transform.position.y - transform.position.y) <= 2f &&
            Hero.FlatDistance(h.transform.position, transform.position) <= touchRadius;

        // Герой вплотную (по серверным позициям) — замах: стоит windupSeconds, потом бьёт, если герой ещё рядом.
        void ServerTouch(float dt)
        {
            if (_touchTimer > 0f) { _touchTimer -= dt; return; }
            foreach (var h in Hero.All)
            {
                if (!InReach(h)) continue;
                _windupTarget = h;
                _windup = windupSeconds;
                OnServerHitHero(h); // остановиться на замах
                break;
            }
        }

        void ServerStrike()
        {
            var h = _windupTarget;
            _windupTarget = null;
            if (!InReach(h)) { _touchTimer = 0.2f; return; } // увернулся
            var hp = h.GetComponent<HeroHealth>();
            if (hp != null && hp.ServerDamage(1, DamageMessage, transform.position))
            {
                _touchTimer = touchCooldown;
                _rise = Mathf.Max(_rise, afterHitPause);
                OnServerHitHero(h);
            }
            else _touchTimer = 0.2f;
        }

        // Ближайший герой своей линии в радиусе, которого видно (стены заслоняют, стекло — нет).
        protected Hero FindVisibleHero(float range)
        {
            Hero best = null;
            float bestDist = float.MaxValue;
            Vector3 eye = transform.position + Vector3.up * 1.4f;
            foreach (var h in Hero.All)
            {
                if (!h.IsSpawned || h.Line.Value != _line || h.IsHiding) continue;
                Vector3 to = h.transform.position + Vector3.up * 1.1f - eye;
                float d = to.magnitude;
                if (d > range || d >= bestDist) continue;
                if (!ClearLine(eye, to, d)) continue;
                best = h;
                bestDist = d;
            }
            return best;
        }

        public static bool ClearLine(Vector3 from, Vector3 to, float dist)
        {
            if (dist < 0.3f) return true;
            var hits = Physics.RaycastAll(from, to / dist, dist - 0.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<Hero>() != null) continue;
                return false;
            }
            return true;
        }
    }
}
