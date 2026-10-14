using Unity.Netcode;
using UnityEngine;

namespace Echo.Ending
{
    // Сетевой синглтон концовок. Сервер следит: после сопряжения Герой 2 у аппарата → ему предлагают выбор.
    // Выбор уходит RPC на сервер, результат в NetworkVariable — у обоих игроков показывается экран концовки.
    public class EndingState : NetworkBehaviour
    {
        public const int None = 0;
        public const int Loop = 1;
        public const int OneBody = 2;

        public static EndingState I { get; private set; }

        public readonly NetworkVariable<int> Choice = new NetworkVariable<int>(None);
        // Сопряжение уже произошло (на клиентах GameDirector.Swapped не известен).
        public readonly NetworkVariable<bool> Swapped = new NetworkVariable<bool>(false);
        // Герой 2 стоит у аппарата и ещё не выбрал: владельцу Героя 2 показать выбор.
        public readonly NetworkVariable<bool> Offer = new NetworkVariable<bool>(false);

        MachineMarker[] _machines;

        public override void OnNetworkSpawn()
        {
            I = this;
            if (!IsServer) return;
            Swapped.Value = GameDirector.I != null && GameDirector.I.Swapped;
            _machines = FindObjectsByType<MachineMarker>(FindObjectsSortMode.None);
            GameEvents.ServerSwapped += OnSwapped;
        }

        public override void OnNetworkDespawn()
        {
            GameEvents.ServerSwapped -= OnSwapped;
            if (I == this) I = null;
        }

        void OnSwapped()
        {
            if (this == null || !IsSpawned) return;
            Swapped.Value = true;
            SwapFlashRpc();
        }

        // Вспышка в момент сопряжения: приходит и хосту, и клиентам, интерфейс подписывается на SwapFlash.
        public static event System.Action SwapFlash;

        [Rpc(SendTo.ClientsAndHost)]
        void SwapFlashRpc()
        {
            SwapFlash?.Invoke();
        }

        void Update()
        {
            if (!IsServer || !IsSpawned) return;
            bool offer = false;
            if (Choice.Value == None && Swapped.Value && _machines != null && TimeLines.I != null)
            {
                var h2 = Hero.Get(2);
                if (h2 != null && h2.IsSpawned)
                {
                    foreach (var m in _machines)
                    {
                        if (m == null || TimeLines.I.LineOf(m.transform.position) != h2.Line.Value) continue;
                        if (Hero.FlatDistance(m.transform.position, h2.transform.position) <= m.radius) { offer = true; break; }
                    }
                }
            }
            if (Offer.Value != offer) Offer.Value = offer;
        }

        // Вызывает владелец Героя 2 из интерфейса.
        public void Choose(int choice)
        {
            if (IsSpawned && (choice == Loop || choice == OneBody)) ChooseRpc(choice);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void ChooseRpc(int choice, RpcParams rpcParams = default)
        {
            if (Choice.Value != None || !Offer.Value) return;
            var h2 = Hero.Get(2);
            if (h2 == null || h2.OwnerClientId != rpcParams.Receive.SenderClientId) return;
            Choice.Value = choice;
            Offer.Value = false;
        }

        public static string Title(int choice) =>
            choice == Loop ? "Петля" : choice == OneBody ? "Одно тело" : "";

        // Короткая строка-эпилог под текстом концовки.
        public static string Epilogue(int choice) =>
            choice == Loop ? "…и всё начинается заново." : choice == OneBody ? "Продолжение следует…" : "";

        public static string Text(int choice)
        {
            if (choice == Loop)
                return "Герой 2 шагнул в кольца аппарата. Для Героя 1 он теперь лишь призрачный силуэт у края трапа, " +
                       "а время замкнулось само на себе.";
            if (choice == OneBody)
                return "Два разума — одно тело. Вы проснулись снова, этим же утром, и помните каждый будущий момент. " +
                       "Теперь вы знаете, как остановить катастрофу.";
            return "";
        }
    }
}
