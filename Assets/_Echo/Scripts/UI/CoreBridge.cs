using Unity.Netcode;
using UnityEngine;

namespace Echo.UI
{
    // Точка связи интерфейса с ядром: меню (NetworkBootstrap), сообщения героям (Hud) и старый IMGUI.
    // Все обращения к ядру только здесь.
    public static class CoreBridge
    {
        static NetworkBootstrap _boot;

        static NetworkBootstrap Boot
        {
            get
            {
                if (_boot == null) _boot = UnityEngine.Object.FindFirstObjectByType<NetworkBootstrap>();
                return _boot;
            }
        }

        public static bool InGame
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm != null && (nm.IsServer || nm.IsClient);
            }
        }

        public static string Status => Boot != null ? Boot.Status : "";
        public static bool Busy => Boot != null && Boot.Busy;

        // ---------- Действия меню ----------

        public static void StartSinglePc() { if (Boot != null) Boot.StartSinglePc(); }
        public static void HostIp() { if (Boot != null) Boot.HostIp(); }
        public static void JoinIp(string ip) { if (Boot != null) Boot.JoinIp(ip); }
        public static void CreateRelay() { if (Boot != null) Boot.CreateRelay(); }
        public static void JoinRelay(string code) { if (Boot != null) Boot.JoinRelay(code); }
        public static void Leave() { if (Boot != null) Boot.Leave(); }

        // ---------- Сообщения героям ----------

        public static bool TryGetToast(int heroId, out string text) => Hud.TryGetToast(heroId, out text);

        // ---------- Старый интерфейс ядра ----------

        public static void ApplyLegacyVisibility()
        {
            bool show = !UiSettings.HideLegacyUi;
            var hud = UnityEngine.Object.FindFirstObjectByType<Hud>();
            if (hud != null && hud.enabled != show) hud.enabled = show;
            var boot = Boot;
            if (boot != null && boot.enabled != show) boot.enabled = show;
        }
    }
}
