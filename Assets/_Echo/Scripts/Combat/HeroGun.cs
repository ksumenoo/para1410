using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Echo.Combat
{
    // Стрельба героя. Нужен ItemType.Pistol в инвентаре.
    // Герой 2 — инверсный пистолет: обойма 6, пуля застревает в точке попадания и через 4 с летит обратно в обойму.
    // Герой 1 — обычный пистолет: пачка ItemType.Ammo = 6 выстрелов, пачка тратится через ItemSystem.Consume.
    // Управление: по сети ЛКМ + прицел мышью (своя верхняя половина), «Один ПК» — Space (Герой 1) / Numpad0 (Герой 2) по взгляду.
    [AddToHeroPrefab]
    public class HeroGun : NetworkBehaviour
    {
        public const int ClipSize = 6;
        public const int ShotsPerPack = 6;
        public const float StuckSeconds = 4f;

        public readonly NetworkVariable<int> InverseRounds = new NetworkVariable<int>(ClipSize);
        public readonly NetworkVariable<int> LoadedRounds = new NetworkVariable<int>(0);

        [Header("Стрельба")]
        public float range = 30f;
        public float cooldown = 0.3f;
        public float returnSpeed = 16f;
        public float guardStunSeconds = 5f;

        class Bullet
        {
            public Vector3 Pos;
            public float Timer;
            public GameObject Go;
            public bool Returning;
        }

        readonly List<Bullet> _serverBullets = new List<Bullet>();
        readonly List<Bullet> _fxBullets = new List<Bullet>();
        static readonly Plane AimPlane = new Plane(Vector3.up, new Vector3(0f, 1f, 0f));

        Hero _hero;
        float _serverCooldown;
        float _localCooldown;
        float _noGunToast;

        public bool IsInverse => _hero != null && _hero.HeroId == 2;
        public bool HasPistol => Count(ItemType.Pistol) > 0;
        public int AmmoPacks => Count(ItemType.Ammo);
        public int ShotsLeft => IsInverse ? InverseRounds.Value : LoadedRounds.Value + AmmoPacks * ShotsPerPack;

        void Awake() => _hero = GetComponent<Hero>();

        int Count(ItemType type)
        {
            if (_hero == null || !_hero.IsSpawned) return 0;
            int n = 0;
            ulong id = _hero.NetworkObjectId;
            foreach (var it in Item.All)
                if (it.IsSpawned && it.Carrier.Value == id && it.ItemType == type) n++;
            return n;
        }

        Item FindCarried(ItemType type)
        {
            ulong id = _hero.NetworkObjectId;
            foreach (var it in Item.All)
                if (it.IsSpawned && it.Carrier.Value == id && it.ItemType == type) return it;
            return null;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            GameEvents.ServerRoomRestart += ServerRefill;
            GameEvents.ServerSwapped += ServerRefill;
        }

        public override void OnNetworkDespawn()
        {
            GameEvents.ServerRoomRestart -= ServerRefill;
            GameEvents.ServerSwapped -= ServerRefill;
            ClearFx();
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner) HandleInput();
            if (IsServer) ServerTickBullets(Time.deltaTime);
            TickFx(Time.deltaTime);
        }

        // ---------- Ввод (владелец) ----------

        void HandleInput()
        {
            var kb = Keyboard.current;
            Vector3 dir;
            if (GameSession.SinglePc)
            {
                if (kb == null) return;
                bool fire = _hero.HeroId == 1 ? kb.spaceKey.wasPressedThisFrame : kb.numpad0Key.wasPressedThisFrame;
                if (!fire) return;
                dir = transform.forward;
            }
            else
            {
                if (_hero != SplitCameras.MyHero()) return;
                var mouse = Mouse.current;
                if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
                if (!AimDirection(mouse.position.ReadValue(), out dir)) return;
            }

            if (_hero.IsHiding || Time.time < _localCooldown) return;
            if (!HasPistol)
            {
                if (Time.time > _noGunToast) { _noGunToast = Time.time + 3f; Hud.Toast(_hero.HeroId, "Нечем стрелять — нужен пистолет."); }
                return;
            }
            _localCooldown = Time.time + cooldown;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir.normalized);
            ShootRpc(dir);
        }

        bool AimDirection(Vector2 mousePos, out Vector3 dir)
        {
            dir = transform.forward;
            var cam = SplitCameras.I != null ? SplitCameras.I.top : null;
            if (cam == null || !cam.pixelRect.Contains(mousePos)) return false;
            var ray = cam.ScreenPointToRay(mousePos);
            if (!AimPlane.Raycast(ray, out float enter)) return false;
            var point = ray.GetPoint(enter);
            dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            return true;
        }

        // ---------- Сервер ----------

        [Rpc(SendTo.Server)]
        void ShootRpc(Vector3 dir) => ServerShoot(dir);

        void ServerShoot(Vector3 dir)
        {
            if (GameDirector.I == null || GameDirector.I.Restarting) return;
            if (_hero.IsHiding || Time.time < _serverCooldown || !HasPistol) return;

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
            dir.Normalize();

            bool inverse = IsInverse;
            if (inverse)
            {
                if (InverseRounds.Value <= 0) { _hero.ToastRpc("Обойма пуста — пули ещё летят обратно."); return; }
                InverseRounds.Value--;
            }
            else
            {
                if (LoadedRounds.Value <= 0)
                {
                    var pack = FindCarried(ItemType.Ammo);
                    if (pack == null) { _hero.ToastRpc("Нет патронов."); return; }
                    ItemSystem.Consume(pack);
                    LoadedRounds.Value = ShotsPerPack;
                }
                LoadedRounds.Value--;
            }
            _serverCooldown = Time.time + cooldown * 0.8f;

            int line = _hero.Line.Value;
            Vector3 origin = transform.position + Vector3.up * 1f;
            Vector3 end = Trace(origin, dir, line, inverse, out var enemy, out var guard);

            if (enemy != null) enemy.ServerKill(_hero);
            if (guard != null && CombatDirector.I != null && CombatDirector.I.ServerStunGuard(guard, guardStunSeconds))
                StunFxRpc(guard.NetworkObjectId, guardStunSeconds);

            if (inverse) _serverBullets.Add(new Bullet { Pos = end, Timer = StuckSeconds });
            ShotFxRpc(origin, end, inverse);
        }

        // Попадание считает сервер: стены по физике (стекло пропускает), враги своей линии — по радиусу.
        Vector3 Trace(Vector3 origin, Vector3 dir, int line, bool inverse, out Enemy enemy, out Guard guard)
        {
            enemy = null;
            guard = null;
            float best = range;
            bool wall = false;
            var hits = Physics.RaycastAll(origin, dir, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<Hero>() != null) continue;
                if (hit.distance < best) { best = hit.distance; wall = true; }
            }

            foreach (var e in Enemy.All)
            {
                if (!e.Alive || e.Line != line) continue;
                float t = RayHit(origin, dir, e.Position, e.hitRadius, best);
                if (t >= 0f) { best = t; enemy = e; guard = null; wall = false; }
            }

            // Охрану и военных оглушает только инверсная пуля Героя 2 (после сопряжения он на линии 1).
            if (inverse && TimeLines.I != null)
                foreach (var g in Guard.All)
                {
                    if (!g.IsSpawned || TimeLines.I.LineOf(g.transform.position) != line) continue;
                    float t = RayHit(origin, dir, g.transform.position, 0.6f, best);
                    if (t >= 0f) { best = t; guard = g; enemy = null; wall = false; }
                }

            if (wall) best = Mathf.Max(0.3f, best - 0.12f);
            return origin + dir * best;
        }

        static float RayHit(Vector3 origin, Vector3 dir, Vector3 target, float radius, float maxDist)
        {
            var to = target - origin;
            to.y = 0f;
            float t = Vector3.Dot(to, dir);
            if (t < 0f || t > maxDist) return -1f;
            var perp = to - dir * t;
            return perp.sqrMagnitude <= radius * radius ? t : -1f;
        }

        void ServerTickBullets(float dt)
        {
            if (_serverBullets.Count == 0) return;
            Vector3 gun = transform.position + Vector3.up * 1f;
            for (int i = _serverBullets.Count - 1; i >= 0; i--)
            {
                var b = _serverBullets[i];
                b.Timer -= dt;
                if (b.Timer > 0f) continue;
                b.Pos = Vector3.MoveTowards(b.Pos, gun, returnSpeed * dt);
                bool far = (b.Pos - gun).sqrMagnitude > 200f * 200f; // другая линия — сразу домой
                if (far || (b.Pos - gun).sqrMagnitude < 0.25f)
                {
                    _serverBullets.RemoveAt(i);
                    if (InverseRounds.Value < ClipSize) InverseRounds.Value++;
                }
            }
        }

        void ServerRefill()
        {
            _serverBullets.Clear();
            InverseRounds.Value = ClipSize;
            ResetFxRpc();
        }

        // ---------- Эффекты (все клиенты) ----------

        [Rpc(SendTo.ClientsAndHost)]
        void ShotFxRpc(Vector3 origin, Vector3 end, bool inverse)
        {
            CombatFx.Tracer(origin, end, inverse);
            if (_hero.rigAnimator && _hero.rigAnimator.runtimeAnimatorController != null)
                _hero.rigAnimator.SetTrigger("Shoot");
            if (inverse) _fxBullets.Add(new Bullet { Pos = end, Timer = StuckSeconds, Go = CombatFx.Bullet(end) });
        }

        [Rpc(SendTo.ClientsAndHost)]
        void StunFxRpc(ulong guardId, float seconds)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.SpawnManager.SpawnedObjects.TryGetValue(guardId, out var no))
                CombatFx.Stun(no.transform, seconds);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void ResetFxRpc()
        {
            ClearFx();
            CombatFx.ClearStuns();
        }

        void TickFx(float dt)
        {
            if (_fxBullets.Count == 0) return;
            Vector3 gun = transform.position + Vector3.up * 1f;
            for (int i = _fxBullets.Count - 1; i >= 0; i--)
            {
                var b = _fxBullets[i];
                if (b.Go == null) { _fxBullets.RemoveAt(i); continue; }
                b.Timer -= dt;
                if (b.Timer > 0f)
                {
                    // Застрявшая пуля подрагивает, перед возвратом — сильнее.
                    float shake = b.Timer < 0.8f ? 0.05f : 0.015f;
                    b.Go.transform.position = b.Pos + Random.insideUnitSphere * shake;
                    continue;
                }
                if (!b.Returning)
                {
                    b.Returning = true;
                    var trail = b.Go.GetComponent<TrailRenderer>();
                    if (trail) { trail.Clear(); trail.emitting = true; }
                }
                b.Pos = Vector3.MoveTowards(b.Pos, gun, returnSpeed * dt);
                b.Go.transform.position = b.Pos;
                float d2 = (b.Pos - gun).sqrMagnitude;
                if (d2 < 0.25f || d2 > 200f * 200f)
                {
                    Destroy(b.Go);
                    _fxBullets.RemoveAt(i);
                }
            }
        }

        void ClearFx()
        {
            foreach (var b in _fxBullets) if (b.Go != null) Destroy(b.Go);
            _fxBullets.Clear();
        }
    }
}
