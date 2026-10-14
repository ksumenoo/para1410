using System;
using Unity.Netcode;
using UnityEngine;

namespace Echo.UI
{
    // Шесть чисел внешности героя (один герой на двоих). Индексы с нуля.
    public struct AppearanceData : INetworkSerializable, IEquatable<AppearanceData>
    {
        public int hair, eyes, face, skin, hairColor, suit;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref hair);
            serializer.SerializeValue(ref eyes);
            serializer.SerializeValue(ref face);
            serializer.SerializeValue(ref skin);
            serializer.SerializeValue(ref hairColor);
            serializer.SerializeValue(ref suit);
        }

        public bool Equals(AppearanceData o) =>
            hair == o.hair && eyes == o.eyes && face == o.face && skin == o.skin && hairColor == o.hairColor && suit == o.suit;

        public override bool Equals(object obj) => obj is AppearanceData d && Equals(d);
        public override int GetHashCode() => hair * 31 + eyes * 17 + face * 13 + skin * 7 + hairColor * 5 + suit;
    }

    // Сетевой синглтон кастомизации: хранит внешность и готовность игроков, применяет её ко всем героям.
    public class AppearanceState : NetworkBehaviour
    {
        public static AppearanceState I { get; private set; }

        // Части: 0 причёска, 1 глаза, 2 мимика, 3 кожа, 4 цвет волос, 5 комбинезон
        public static readonly string[] PartNames = { "Причёска", "Глаза", "Мимика", "Цвет кожи", "Цвет волос", "Комбинезон" };
        static readonly int[] DefaultCounts = { 6, 4, 4, 5, 6, 4 };

        public readonly NetworkVariable<AppearanceData> Data = new NetworkVariable<AppearanceData>();
        // Бит 0 — Герой 1 готов, бит 1 — Герой 2 готов.
        public readonly NetworkVariable<int> ReadyMask = new NetworkVariable<int>(0);

        public bool AllReady => ReadyMask.Value == 3;

        // Бит 0 — Герой 1 нажал «Начать» на вступительном экране, бит 1 — Герой 2.
        public readonly NetworkVariable<int> StartMask = new NetworkVariable<int>(0);

        // Пока оба не нажали «Начать», враги стоят (GameEvents.EnemiesPaused, только сервер).
        bool _pausedEnemies;

        public override void OnNetworkSpawn()
        {
            I = this;
            if (!IsServer) return;
            var d = new AppearanceData
            {
                hair = UnityEngine.Random.Range(0, Count(0)),
                eyes = UnityEngine.Random.Range(0, Count(1)),
                face = UnityEngine.Random.Range(0, Count(2)),
                skin = UnityEngine.Random.Range(0, Count(3)),
                hairColor = UnityEngine.Random.Range(0, Count(4)),
                suit = UnityEngine.Random.Range(0, Count(5)),
            };
            Data.Value = d;

            if (!UiSettings.SkipIntro)
            {
                _pausedEnemies = true;
                GameEvents.EnemiesPaused = true;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (I == this) I = null;
            if (_pausedEnemies) { _pausedEnemies = false; GameEvents.EnemiesPaused = false; }
        }

        // Локальный игрок нажал «Начать»: сообщаем серверу за каждого своего героя
        // (в режиме «Один ПК» оба героя у одного игрока — хватает одного нажатия).
        // true — сообщение ушло.
        public bool SendStart()
        {
            if (!IsSpawned) return false;
            bool sent = false;
            foreach (var h in Hero.All)
            {
                if (!h.IsSpawned || !h.IsOwner) continue;
                StartRpc(h.HeroId);
                sent = true;
            }
            return sent;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void StartRpc(int heroId, RpcParams rpcParams = default)
        {
            if (heroId < 1 || heroId > 2) return;
            var hero = Hero.Get(heroId);
            if (hero == null || hero.OwnerClientId != rpcParams.Receive.SenderClientId) return;
            StartMask.Value |= 1 << (heroId - 1);
            if (StartMask.Value == 3 && _pausedEnemies)
            {
                _pausedEnemies = false;
                GameEvents.EnemiesPaused = false;
            }
        }

        // Сколько вариантов у части (из риг художника, иначе числа из плана).
        public static int Count(int part)
        {
            foreach (var h in Hero.All)
            {
                var a = h.rigAppearance;
                if (a == null) continue;
                int n = 0;
                switch (part)
                {
                    case 0: n = a.hairSprites != null ? a.hairSprites.Length : 0; break;
                    case 1: n = a.eyeSprites != null ? a.eyeSprites.Length : 0; break;
                    case 2: n = a.faceSprites != null ? a.faceSprites.Length : 0; break;
                    case 3: n = a.skinColors != null ? a.skinColors.Length : 0; break;
                    case 4: n = a.hairColors != null ? a.hairColors.Length : 0; break;
                    case 5: n = a.suitColors != null ? a.suitColors.Length : 0; break;
                }
                if (n > 0) return n;
            }
            return DefaultCounts[Mathf.Clamp(part, 0, DefaultCounts.Length - 1)];
        }

        public static int Get(AppearanceData d, int part)
        {
            switch (part)
            {
                case 0: return d.hair;
                case 1: return d.eyes;
                case 2: return d.face;
                case 3: return d.skin;
                case 4: return d.hairColor;
                default: return d.suit;
            }
        }

        // Листать часть на шаг вперёд/назад (вызывает любой игрок).
        public void Step(int part, int delta)
        {
            if (!IsSpawned || AllReady) return;
            int n = Count(part);
            int v = ((Get(Data.Value, part) + delta) % n + n) % n;
            SetPartRpc(part, v);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void SetPartRpc(int part, int value)
        {
            if (AllReady || part < 0 || part > 5) return;
            int n = Count(part);
            value = ((value % n) + n) % n;
            var d = Data.Value;
            switch (part)
            {
                case 0: d.hair = value; break;
                case 1: d.eyes = value; break;
                case 2: d.face = value; break;
                case 3: d.skin = value; break;
                case 4: d.hairColor = value; break;
                default: d.suit = value; break;
            }
            Data.Value = d;
        }

        public void SetReady(int heroId, bool ready)
        {
            if (IsSpawned) ReadyRpc(heroId, ready);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void ReadyRpc(int heroId, bool ready, RpcParams rpcParams = default)
        {
            if (AllReady || heroId < 1 || heroId > 2) return;
            var hero = Hero.Get(heroId);
            if (hero == null || hero.OwnerClientId != rpcParams.Receive.SenderClientId) return;
            int bit = 1 << (heroId - 1);
            ReadyMask.Value = ready ? (ReadyMask.Value | bit) : (ReadyMask.Value & ~bit);
        }

        // Применяем ко всем героям на каждой машине.
        void LateUpdate()
        {
            if (!IsSpawned) return;
            var d = Data.Value;
            foreach (var h in Hero.All)
            {
                var a = h.rigAppearance;
                if (a == null) continue;
                if (a.hairIndex != d.hair || a.eyesIndex != d.eyes || a.faceIndex != d.face ||
                    a.skinIndex != d.skin || a.hairColorIndex != d.hairColor || a.suitIndex != d.suit)
                    a.Set(d.hair, d.eyes, d.face, d.skin, d.hairColor, d.suit);
            }
        }
    }
}
