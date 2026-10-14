using Echo.Audio;
using Unity.Netcode;
using UnityEngine;

namespace Echo.Combat
{
    // Здоровье героя: 3 удара — смерть и общий рестарт комнаты. Восстанавливается при рестарте.
    [AddToHeroPrefab]
    public class HeroHealth : NetworkBehaviour
    {
        public const int MaxHp = 3;

        public readonly NetworkVariable<int> Hp = new NetworkVariable<int>(MaxHp);

        [System.NonSerialized] public float invulnSeconds = 1f;   // неуязвимость после удара
        [System.NonSerialized] public float knockSpeed = 9f;      // толчок назад: начальная скорость, м/с
        [System.NonSerialized] public float knockSeconds = 0.2f;

        public const float FlashSeconds = 0.4f;

        Hero _hero;
        CharacterController _cc;
        float _invulnUntil;
        Vector3 _knockDir;
        float _knockTimer;

        // Локально: до какого времени мигать красным (для интерфейса).
        public float FlashUntil { get; private set; }

        void Awake()
        {
            _hero = GetComponent<Hero>();
            _cc = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            Hp.OnValueChanged += OnHpChanged;
            if (IsServer)
            {
                GameEvents.ServerRoomRestart += ServerRestore;
                GameEvents.ServerSwapped += ServerOnSwapped;
            }
        }

        public override void OnNetworkDespawn()
        {
            Hp.OnValueChanged -= OnHpChanged;
            GameEvents.ServerRoomRestart -= ServerRestore;
            GameEvents.ServerSwapped -= ServerOnSwapped;
        }

        // После сопряжения пару секунд неуязвим — герой осматривается на новой линии.
        void ServerOnSwapped() => _invulnUntil = Time.time + Enemy.SwapCalmSeconds;

        void OnHpChanged(int previous, int current)
        {
            if (current >= previous) return;
            FlashUntil = Time.time + FlashSeconds;
            AudioManager.Play(Sfx.Hit);
        }

        // Толчок двигает владелец (позицию героя ведёт он).
        [Rpc(SendTo.Owner)]
        void KnockbackRpc(Vector3 dir)
        {
            _knockDir = dir;
            _knockTimer = knockSeconds;
        }

        void Update()
        {
            if (_knockTimer <= 0f || !IsOwner || _cc == null || !_cc.enabled) return;
            float k = _knockTimer / knockSeconds; // затухает к концу
            _knockTimer -= Time.deltaTime;
            _cc.Move(_knockDir * (knockSpeed * k * Time.deltaTime));
        }

        // Сервер: нанести урон. Возвращает true, если удар прошёл.
        public bool ServerDamage(int amount, string message = "Тебя ранили!") =>
            ServerDamage(amount, message, transform.position - transform.forward);

        // from — откуда пришёл удар: героя толкает в обратную сторону.
        public bool ServerDamage(int amount, string message, Vector3 from)
        {
            if (!IsServer || !IsSpawned || Hp.Value <= 0) return false;
            if (GameDirector.I == null || GameDirector.I.Restarting || GameEvents.EnemiesPaused) return false;
            if (Time.time < _invulnUntil) return false;

            _invulnUntil = Time.time + invulnSeconds;
            Hp.Value = Mathf.Max(0, Hp.Value - amount);
            var away = transform.position - from;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            KnockbackRpc(away.normalized);
            if (Hp.Value <= 0)
            {
                GameDirector.I.ServerKilled(_hero);
                // Режим бога (пульт): рестарта не будет — сердца сразу назад.
                if (GameDirector.DebugGodMode) Hp.Value = MaxHp;
            }
            else _hero.ToastRpc(message + " Сердец: " + Hp.Value);
            return true;
        }

        void ServerRestore()
        {
            Hp.Value = MaxHp;
            _invulnUntil = Time.time + 1.5f;
        }
    }
}
