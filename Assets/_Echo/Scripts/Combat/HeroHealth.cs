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

        [Tooltip("Неуязвимость после удара, с")]
        public float invulnSeconds = 1f;

        Hero _hero;
        float _invulnUntil;

        // Локально: до какого времени мигать красным (для интерфейса).
        public float FlashUntil { get; private set; }

        void Awake() => _hero = GetComponent<Hero>();

        public override void OnNetworkSpawn()
        {
            Hp.OnValueChanged += OnHpChanged;
            if (IsServer) GameEvents.ServerRoomRestart += ServerRestore;
        }

        public override void OnNetworkDespawn()
        {
            Hp.OnValueChanged -= OnHpChanged;
            GameEvents.ServerRoomRestart -= ServerRestore;
        }

        void OnHpChanged(int previous, int current)
        {
            if (current < previous) FlashUntil = Time.time + 0.35f;
        }

        // Сервер: нанести урон. Возвращает true, если удар прошёл.
        public bool ServerDamage(int amount, string message = "Тебя ранили!")
        {
            if (!IsServer || !IsSpawned || Hp.Value <= 0) return false;
            if (GameDirector.I == null || GameDirector.I.Restarting) return false;
            if (Time.time < _invulnUntil) return false;

            _invulnUntil = Time.time + invulnSeconds;
            Hp.Value = Mathf.Max(0, Hp.Value - amount);
            if (Hp.Value <= 0) GameDirector.I.ServerKilled(_hero);
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
