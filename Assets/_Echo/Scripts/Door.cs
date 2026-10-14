using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Echo
{
    // Дверь на ключ. Если открыть её в прошлом (линия 1), в будущем она тоже открыта.
    public class Door : NetworkBehaviour
    {
        public static readonly List<Door> All = new List<Door>();

        public readonly NetworkVariable<int> DoorId = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> KeyId = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Line = new NetworkVariable<int>(1);
        public readonly NetworkVariable<bool> Open = new NetworkVariable<bool>(false);

        public GameObject closedVisual;
        public Collider blocker;
        public UnityEngine.AI.NavMeshObstacle navBlocker;

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            Open.OnValueChanged += (_, __) => Apply();
            Apply();
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        void Apply()
        {
            if (closedVisual) closedVisual.SetActive(!Open.Value);
            if (blocker) blocker.enabled = !Open.Value;
            if (navBlocker) navBlocker.enabled = !Open.Value;
        }

        public void ServerTryOpen(Hero hero)
        {
            if (Open.Value) return;
            if (KeyId.Value != 0)
            {
                var key = hero.Inventory().FirstOrDefault(i => i.ItemType == ItemType.Key && i.KeyId.Value == KeyId.Value);
                if (key == null) { hero.ToastRpc("Заперто. Нужен ключ " + KeyId.Value + "."); return; }
                ItemSystem.Consume(key);
            }

            Open.Value = true;
            if (Line.Value == 1)
                foreach (var d in All)
                    if (d != this && d.IsSpawned && d.Line.Value == 2 && d.DoorId.Value == DoorId.Value)
                        d.Open.Value = true;

            hero.ToastRpc(Line.Value == 1 ? "Дверь открыта — и в будущем тоже." : "Дверь открыта.");
        }
    }
}
