using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Echo.Combat
{
    // Общая часть мутанта и сгустка: линия, касание (урон), смерть и воскрешение через 4 с.
    // Логика только на сервере, клиенты видят позицию через NetworkTransform и состояние через Dead.
    public abstract class Enemy : NetworkBehaviour
    {
        public static readonly List<Enemy> All = new List<Enemy>();

        public readonly NetworkVariable<bool> Dead = new NetworkVariable<bool>(false);

        [Header("Общее")]
        public float reviveSeconds = 4f;   // сколько лежит убитым
        public float riseSeconds = 0.8f;   // встаёт (стоит на месте, не бьёт)
        public float hitRadius = 0.6f;     // радиус попадания пули по XZ
        public float touchRadius = 1.0f;   // касание героя = удар
        public float touchCooldown = 1.2f;

        protected int _line = 2;
        protected Vector3 _spawnPos;
        protected Quaternion _spawnRot = Quaternion.identity;
        protected float _rise;
        protected Hero Target;
        float _deadTimer;
        float _touchTimer;

        // Только на сервере.
        public int Line => _line;
        public bool Alive => IsSpawned && !Dead.Value;
        public Vector3 Position => transform.position;

        protected virtual string DamageMessage => "Тебя ранили!";

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
            Target = null;
            OnServerReset();
            var nt = GetComponent<NetworkTransform>();
            if (nt != null) nt.Teleport(transform.position, transform.rotation, transform.localScale);
        }

        public void ServerKill(Hero by)
        {
            if (!IsServer || Dead.Value) return;
            Dead.Value = true;
            _deadTimer = reviveSeconds;
            Target = null;
            OnServerDied();
        }

        protected virtual void OnServerInit() { }
        protected virtual void OnServerReset() => transform.SetPositionAndRotation(_spawnPos, _spawnRot);
        protected virtual void OnServerDied() { }
        protected virtual void OnServerRevived() { }
        protected virtual void OnServerFrozen() { }
        protected abstract void ServerTick(float dt);

        protected virtual void Update()
        {
            if (!IsServer || !IsSpawned) return;
            if (GameDirector.I != null && GameDirector.I.Restarting) { OnServerFrozen(); return; }
            float dt = Time.deltaTime;
            if (Dead.Value)
            {
                _deadTimer -= dt;
                if (_deadTimer <= 0f)
                {
                    Dead.Value = false;
                    _rise = riseSeconds;
                    OnServerRevived();
                }
                return;
            }
            if (_rise > 0f) { _rise -= dt; return; }
            ServerTick(dt);
            ServerTouch(dt);
        }

        void ServerTouch(float dt)
        {
            if (_touchTimer > 0f) { _touchTimer -= dt; return; }
            foreach (var h in Hero.All)
            {
                if (!h.IsSpawned || h.Line.Value != _line || h.IsHiding) continue;
                if (Mathf.Abs(h.transform.position.y - transform.position.y) > 2f) continue;
                if (Hero.FlatDistance(h.transform.position, transform.position) > touchRadius) continue;
                var hp = h.GetComponent<HeroHealth>();
                if (hp != null && hp.ServerDamage(1, DamageMessage))
                {
                    _touchTimer = touchCooldown;
                    break;
                }
            }
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
