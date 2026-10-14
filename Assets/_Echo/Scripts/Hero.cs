using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Echo
{
    [RequireComponent(typeof(CharacterController))]
    public class Hero : NetworkBehaviour
    {
        public static readonly List<Hero> All = new List<Hero>();

        public readonly NetworkVariable<int> HeroIdVar = new NetworkVariable<int>(1);
        public readonly NetworkVariable<int> Line = new NetworkVariable<int>(1);
        public readonly NetworkVariable<bool> HasBackpack = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<float> ReactorProgress = new NetworkVariable<float>(0f);
        public readonly NetworkVariable<int> HidingIn = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<bool> Sneaking = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        [Header("Движение")]
        public float walkSpeed = 5f;
        public float sneakSpeed = 2.2f;
        public float interactRadius = 2f;
        public float throwDistance = 7f;

        [Header("Вид (заглушки)")]
        public GameObject visualRoot;
        public Renderer bodyRenderer;
        public Material hero1Material;
        public Material hero2Material;
        public GameObject aura;

        [Header("Скелетный риг (если собран)")]
        public Transform billboard;
        public Animator rigAnimator;
        public HeroAura rigAura;
        public HeroAppearance rigAppearance;
        public float cameraPitch = 50f;

        public const int BaseSlots = 6;
        public const int BackpackSlots = 8;

        CharacterController _cc;
        float _yVelocity;
        Vector3 _lastPos;
        float _speed;
        float _gunCheck;
        bool _lastHeld;

        public int HeroId => HeroIdVar.Value;
        public int Capacity => BaseSlots + (HasBackpack.Value ? BackpackSlots : 0);
        public int SelectedSlot { get; private set; }
        public bool IsSneaking => Sneaking.Value;
        public bool IsHiding => HidingIn.Value >= 0;
        public bool InventoryOpen { get; private set; }

        // Только на сервере: держит ли игрок кнопку действия (для реактора).
        public bool Holding { get; private set; }

        public Vector3 LocalPos => TimeLines.I.ToLocal(transform.position, Line.Value);

        public static Hero Get(int heroId) => All.FirstOrDefault(h => h.HeroId == heroId);

        void Awake() => _cc = GetComponent<CharacterController>();

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            HeroIdVar.OnValueChanged += (_, __) => ApplyLook();
            ApplyLook();
            _cc.enabled = IsOwner;
        }

        public override void OnNetworkDespawn() => All.Remove(this);
        public override void OnGainedOwnership() => _cc.enabled = true;
        public override void OnLostOwnership() => _cc.enabled = false;

        void ApplyLook()
        {
            if (bodyRenderer) bodyRenderer.sharedMaterial = HeroId == 1 ? hero1Material : hero2Material;
            if (aura) aura.SetActive(HeroId == 2);
            if (rigAura) rigAura.SetAura(HeroId == 2);
        }

        public List<Item> Inventory() =>
            Item.All.Where(i => i.IsSpawned && i.Carrier.Value == NetworkObjectId)
                    .OrderBy(i => i.NetworkObjectId)
                    .ToList();

        void Update()
        {
            if (!IsSpawned || !IsOwner || TimeLines.I == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            // Движение (в шкафчике стоим на месте)
            Vector2 m = IsHiding ? Vector2.zero : InputScheme.Move(this, kb);
            bool sneak = InputScheme.Sneak(this, kb);
            if (Sneaking.Value != sneak) Sneaking.Value = sneak;
            var dir = new Vector3(m.x, 0f, m.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);
            _yVelocity = _cc.isGrounded ? -1f : _yVelocity + Physics.gravity.y * Time.deltaTime;
            float speed = IsSneaking ? sneakSpeed : walkSpeed;
            _cc.Move((dir * speed + Vector3.up * _yVelocity) * Time.deltaTime);

            // Инвентарь
            if (InputScheme.InventoryToggle(this, kb)) InventoryOpen = !InventoryOpen;
            int digit = InputScheme.DigitSlot(this, kb);
            if (digit >= 0) SelectedSlot = digit;
            if (InputScheme.NextSlot(this, kb)) SelectedSlot++;
            if (SelectedSlot >= Capacity) SelectedSlot = 0;

            // Действия
            if (InputScheme.InteractDown(this, kb))
            {
                InteractRpc();
                if (rigAnimator) rigAnimator.SetTrigger("Interact");
            }
            bool held = InputScheme.InteractHeld(this, kb);
            if (held != _lastHeld)
            {
                _lastHeld = held;
                SetHoldingRpc(held);
            }
            bool drop = InputScheme.Drop(this, kb);
            bool toss = InputScheme.Throw(this, kb);
            if ((drop || toss) && !IsHiding)
            {
                var inv = Inventory();
                if (SelectedSlot < inv.Count) DropRpc(inv[SelectedSlot].NetworkObjectId, toss);
            }
        }

        // ---------- RPC: клиент -> сервер ----------

        [Rpc(SendTo.Server)]
        void InteractRpc() => ServerInteract();

        [Rpc(SendTo.Server)]
        void SetHoldingRpc(bool held) => Holding = held;

        [Rpc(SendTo.Server)]
        void DropRpc(ulong itemId, bool toss)
        {
            var item = ItemSystem.Find(itemId);
            if (item == null) return;
            if (toss) ItemSystem.ServerThrow(this, item, throwDistance);
            else ItemSystem.ServerDrop(this, item);
        }

        // ---------- RPC: сервер -> владелец ----------

        [Rpc(SendTo.Owner)]
        public void ToastRpc(string message) => Hud.Toast(HeroId, message);

        [Rpc(SendTo.Owner)]
        public void TeleportRpc(Vector3 worldPos)
        {
            _cc.enabled = false;
            transform.position = worldPos;
            var nt = GetComponent<NetworkTransform>();
            if (nt != null) nt.Teleport(worldPos, transform.rotation, transform.localScale);
            _cc.enabled = true;
        }

        // ---------- Логика сервера ----------

        void ServerInteract()
        {
            if (IsHiding)
            {
                HidingIn.Value = -1;
                ToastRpc("Ты вышел из шкафчика.");
                return;
            }
            int line = Line.Value;
            Vector3 here = transform.position;

            Item bestItem = null;
            float bestItemDist = interactRadius;
            foreach (var it in Item.All)
            {
                if (!it.IsSpawned || !it.InWorld || it.Line.Value != line) continue;
                float d = FlatDistance(it.WorldPos, here);
                if (d < bestItemDist) { bestItemDist = d; bestItem = it; }
            }
            if (bestItem != null) { ItemSystem.ServerPickup(this, bestItem); return; }

            Door bestDoor = null;
            float bestDoorDist = interactRadius + 1f;
            foreach (var door in Door.All)
            {
                if (!door.IsSpawned || door.Open.Value || door.Line.Value != line) continue;
                float d = FlatDistance(door.transform.position, here);
                if (d < bestDoorDist) { bestDoorDist = d; bestDoor = door; }
            }
            if (bestDoor != null) { bestDoor.ServerTryOpen(this); return; }

            var lockers = LockerSpot.All;
            int bestLocker = -1;
            float bestLockerDist = 1.8f;
            for (int i = 0; i < lockers.Count; i++)
            {
                if (TimeLines.I.LineOf(lockers[i].transform.position) != line) continue;
                float d = FlatDistance(lockers[i].transform.position, here);
                if (d < bestLockerDist) { bestLockerDist = d; bestLocker = i; }
            }
            if (bestLocker >= 0)
            {
                HidingIn.Value = bestLocker;
                ToastRpc("Ты спрятался. E — выйти.");
            }
        }

        void LateUpdate()
        {
            if (visualRoot != null)
            {
                bool show = !IsSpawned || !IsHiding;
                if (visualRoot.activeSelf != show) visualRoot.SetActive(show);
            }
            UpdateRig();
        }

        // Риг рисуется в плоскости XY: держим его лицом к камере и отражаем по направлению.
        void UpdateRig()
        {
            if (billboard == null) return;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            var delta = transform.position - _lastPos;
            delta.y = 0f;
            _lastPos = transform.position;
            float instant = delta.magnitude / dt;
            if (instant > 20f) instant = 0f; // телепорт
            _speed = Mathf.Lerp(_speed, instant, 1f - Mathf.Exp(-12f * dt));

            billboard.rotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            float fx = transform.forward.x;
            if (Mathf.Abs(fx) > 0.1f)
            {
                var sc = billboard.localScale;
                sc.x = Mathf.Abs(sc.x) * (fx < 0f ? -1f : 1f);
                billboard.localScale = sc;
            }

            if (rigAnimator && rigAnimator.runtimeAnimatorController != null)
            {
                rigAnimator.SetFloat("Speed", _speed);
                rigAnimator.SetBool("Crouch", IsSpawned && IsSneaking);
            }

            if (rigAppearance && IsSpawned && Time.time > _gunCheck)
            {
                _gunCheck = Time.time + 0.25f;
                bool gun = false;
                foreach (var it in Item.All)
                    if (it.IsSpawned && it.Carrier.Value == NetworkObjectId && it.ItemType == ItemType.Pistol) { gun = true; break; }
                rigAppearance.SetGunVisible(gun);
            }
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
