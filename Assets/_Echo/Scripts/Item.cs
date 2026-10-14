using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Echo
{
    public enum ItemType { Key = 0, Barrel = 1, Backpack = 2, Ammo = 3, Pistol = 4 }

    // Предмет. Положение задаётся линией и локальной позицией, без NetworkTransform.
    // Предмет, оставленный на линии 1, создаёт "оттиск" (эхо) на том же месте линии 2.
    public class Item : NetworkBehaviour
    {
        public static readonly List<Item> All = new List<Item>();

        public readonly NetworkVariable<int> Type = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> KeyId = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Line = new NetworkVariable<int>(1);
        public readonly NetworkVariable<Vector3> Local = new NetworkVariable<Vector3>(Vector3.zero);
        public readonly NetworkVariable<ulong> Carrier = new NetworkVariable<ulong>(0);
        public readonly NetworkVariable<bool> Hidden = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> IsEcho = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<ulong> EchoOf = new NetworkVariable<ulong>(0);

        // Только на сервере: эхо уже забрали в будущем, новых оттисков не будет.
        [System.NonSerialized] public bool EchoSpent;

        [Tooltip("Визуалы по порядку ItemType: Key, Barrel, Backpack, Ammo, Pistol")]
        public GameObject[] visuals;
        public GameObject aura;

        public ItemType ItemType => (ItemType)Type.Value;
        public bool InWorld => Carrier.Value == 0 && !Hidden.Value;
        public Vector3 WorldPos => TimeLines.I.ToWorld(Local.Value, Line.Value);

        public string DisplayName
        {
            get
            {
                switch (ItemType)
                {
                    case ItemType.Key: return "Ключ " + KeyId.Value;
                    case ItemType.Barrel: return "Бочка";
                    case ItemType.Backpack: return "Рюкзак";
                    case ItemType.Ammo: return "Патроны";
                    case ItemType.Pistol: return "Пистолет";
                    default: return "?";
                }
            }
        }

        public override void OnNetworkSpawn() => All.Add(this);
        public override void OnNetworkDespawn() => All.Remove(this);

        void LateUpdate()
        {
            if (!IsSpawned || TimeLines.I == null) return;
            bool show = InWorld;
            int t = Type.Value;
            for (int i = 0; i < visuals.Length; i++)
            {
                bool on = show && i == t;
                if (visuals[i] && visuals[i].activeSelf != on) visuals[i].SetActive(on);
            }
            bool auraOn = show && IsEcho.Value;
            if (aura)
            {
                if (aura.activeSelf != auraOn) aura.SetActive(auraOn);
                if (auraOn) aura.transform.localScale = Vector3.one * (1.3f + 0.15f * Mathf.Sin(Time.time * 4f));
            }
            if (show) transform.position = WorldPos;
        }
    }

    // Серверные правила предметов и оттисков.
    public static class ItemSystem
    {
        public static Item Find(ulong id)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.SpawnManager.SpawnedObjects.TryGetValue(id, out var no)) return null;
            return no.GetComponent<Item>();
        }

        public static Item AttachedEcho(Item source)
        {
            foreach (var it in Item.All)
                if (it.IsSpawned && it.EchoOf.Value == source.NetworkObjectId) return it;
            return null;
        }

        public static Item Spawn(ItemType type, int keyId, int line, Vector3 local, bool isEcho, ulong echoOf)
        {
            var no = Object.Instantiate(GameDirector.I.itemPrefab, TimeLines.I.ToWorld(local, line), Quaternion.identity);
            no.Spawn(true);
            var it = no.GetComponent<Item>();
            it.Type.Value = (int)type;
            it.KeyId.Value = keyId;
            it.Line.Value = line;
            it.Local.Value = local;
            it.IsEcho.Value = isEcho;
            it.EchoOf.Value = echoOf;
            return it;
        }

        public static void ServerPickup(Hero hero, Item it)
        {
            if (it.ItemType == ItemType.Backpack)
            {
                if (hero.HasBackpack.Value) { hero.ToastRpc("Рюкзак уже есть."); return; }
                hero.HasBackpack.Value = true;
                hero.ToastRpc("Рюкзак: +8 ячеек.");
                Consume(it);
                return;
            }

            if (hero.Inventory().Count >= hero.Capacity) { hero.ToastRpc("Инвентарь полон."); return; }

            // Забрали оттиск в будущем: он становится самостоятельным, источник больше не "эхает".
            if (it.EchoOf.Value != 0)
            {
                var source = Find(it.EchoOf.Value);
                if (source != null) source.EchoSpent = true;
                it.EchoOf.Value = 0;
            }

            it.Carrier.Value = hero.NetworkObjectId;

            // Предмет унесли в прошлом: где он окажется в будущем, пока неизвестно.
            var echo = AttachedEcho(it);
            if (echo != null) echo.Hidden.Value = true;

            hero.ToastRpc("Взято: " + it.DisplayName);
        }

        public static void ServerDrop(Hero hero, Item it)
        {
            var local = TimeLines.I.ToLocal(hero.transform.position + hero.transform.forward * 0.9f, hero.Line.Value);
            Place(hero, it, local, "Положено: ");
        }

        // Бросок: предмет летит вперёд до стены, шум привлекает охрану.
        public static void ServerThrow(Hero hero, Item it, float distance)
        {
            var origin = hero.transform.position + Vector3.up * 1f;
            var dir = hero.transform.forward;
            float d = distance;
            if (Physics.Raycast(origin, dir, out var hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                d = Mathf.Max(0.6f, hit.distance - 0.5f);
            var world = origin + dir * d;
            var local = TimeLines.I.ToLocal(world, hero.Line.Value);
            Place(hero, it, local, "Брошено: ");
            Guard.EmitNoise(hero.Line.Value, world, 9f);
        }

        static void Place(Hero hero, Item it, Vector3 local, string verb)
        {
            if (it.Carrier.Value != hero.NetworkObjectId) return;
            local.y = 0f;
            it.Carrier.Value = 0;
            it.Line.Value = hero.Line.Value;
            it.Local.Value = local;

            var echo = AttachedEcho(it);
            if (it.Line.Value == 1 && !it.EchoSpent)
            {
                if (echo == null) echo = Spawn(it.ItemType, it.KeyId.Value, 2, local, true, it.NetworkObjectId);
                echo.Local.Value = local;
                echo.Hidden.Value = false;
            }
            else if (echo != null)
            {
                // Сам предмет попал в будущее: его оттиск больше не нужен.
                echo.NetworkObject.Despawn(true);
                it.EchoSpent = true;
            }

            hero.ToastRpc(verb + it.DisplayName + (it.Line.Value == 1 && !it.EchoSpent ? " (оттиск в будущем)" : ""));
        }

        public static void Consume(Item it)
        {
            var echo = AttachedEcho(it);
            if (echo != null) echo.NetworkObject.Despawn(true);
            it.NetworkObject.Despawn(true);
        }
    }
}
