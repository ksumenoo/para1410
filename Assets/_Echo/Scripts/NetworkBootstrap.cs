using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Echo
{
    // Главное меню (IMGUI): игра по коду через Relay, по IP или на одном ПК.
    public class NetworkBootstrap : MonoBehaviour
    {
        const ushort Port = 7777;

        public static float GuiScale => Mathf.Max(0.5f, Screen.height / 720f);

        string _ip = "127.0.0.1";
        string _code = "";
        string _status = "";
        bool _busy;
        ISession _session;
        GUIStyle _title, _text;

        // Для нового интерфейса (Чат 4): состояние меню.
        public string Status => _status;
        public bool Busy => _busy;
        // false — не рисовать старое IMGUI-меню (новый uGUI его заменяет). Логика сети работает.
        public bool drawLegacyGui = true;

        NetworkManager NM => NetworkManager.Singleton;
        UnityTransport Transport => NM.GetComponent<UnityTransport>();

        void Start()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("Echo.AutoSinglePc", false))
            {
                UnityEditor.SessionState.SetBool("Echo.AutoSinglePc", false);
                Invoke(nameof(StartSinglePc), 0.2f);
            }
#endif
            NM.OnClientDisconnectCallback += id =>
            {
                if (!NM.IsServer && id == NM.LocalClientId) _status = "Соединение потеряно.";
            };
        }

        void OnGUI()
        {
            if (NM == null || !drawLegacyGui) return;
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _text = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            }
            GUI.matrix = Matrix4x4.Scale(new Vector3(GuiScale, GuiScale, 1f));
            float w = Screen.width / GuiScale;

            if (!NM.IsServer && !NM.IsClient) DrawMenu(w);
            else DrawInGame(w);
        }

        void DrawMenu(float w)
        {
            GUI.Box(new Rect(0f, 0f, w, 720f), GUIContent.none);
            GUILayout.BeginArea(new Rect(w / 2f - 240f, 60f, 480f, 640f));
            GUILayout.Label("Наоборот получилось", _title, GUILayout.Height(50f));

            GUILayout.Label("Каким героем играешь (если создаёшь игру):", _text);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(GameSession.HostHeroChoice == 1, "Герой 1\nнастоящее → будущее", "Button", GUILayout.Height(44f)))
                GameSession.HostHeroChoice = 1;
            if (GUILayout.Toggle(GameSession.HostHeroChoice == 2, "Герой 2\nбудущее → прошлое", "Button", GUILayout.Height(44f)))
                GameSession.HostHeroChoice = 2;
            GUILayout.EndHorizontal();
            GUILayout.Label("Второй игрок получит другого героя.", _text);

            GUI.enabled = !_busy;
            GUILayout.Space(14f);
            GUILayout.Label("Через интернет (код комнаты):", _text);
            if (GUILayout.Button("Создать игру и получить код", GUILayout.Height(30f))) CreateRelay();
            GUILayout.BeginHorizontal();
            _code = GUILayout.TextField(_code, 12, GUILayout.Width(220f), GUILayout.Height(28f));
            if (GUILayout.Button("Войти по коду", GUILayout.Height(28f))) JoinRelay(_code.Trim().ToUpperInvariant());
            GUILayout.EndHorizontal();

            GUILayout.Space(14f);
            GUILayout.Label("По IP (одна сеть или отладка):", _text);
            if (GUILayout.Button("Создать игру по IP (порт " + Port + ")", GUILayout.Height(30f))) HostIp();
            GUILayout.BeginHorizontal();
            _ip = GUILayout.TextField(_ip, 40, GUILayout.Width(220f), GUILayout.Height(28f));
            if (GUILayout.Button("Войти по IP", GUILayout.Height(28f))) JoinIp(_ip.Trim());
            GUILayout.EndHorizontal();

            GUILayout.Space(14f);
            if (GUILayout.Button("Один ПК (оба героя с одной клавиатуры)", GUILayout.Height(34f))) StartSinglePc();
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status, _text);
            GUILayout.EndArea();
        }

        void DrawInGame(float w)
        {
            GUILayout.BeginArea(new Rect(w - 330f, 8f, 320f, 120f));
            GUILayout.BeginVertical(GUI.skin.box);
            if (!string.IsNullOrEmpty(GameSession.JoinInfo)) GUILayout.Label(GameSession.JoinInfo, _text);
            if (NM.IsServer && !GameSession.SinglePc && NM.ConnectedClientsIds.Count < 2)
                GUILayout.Label("Ждём второго игрока…", _text);
            if (GUILayout.Button("Выйти в меню")) Leave();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // ---------- Режимы ----------

        public void StartSinglePc()
        {
            GameSession.SinglePc = true;
            GameSession.HostHeroChoice = 1;
            GameSession.JoinInfo = "Один ПК";
            Transport.SetConnectionData("127.0.0.1", Port, "127.0.0.1");
            if (!NM.StartHost()) { _status = "Не удалось запустить игру."; GameSession.SinglePc = false; }
        }

        public void HostIp()
        {
            GameSession.SinglePc = false;
            Transport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");
            if (NM.StartHost()) GameSession.JoinInfo = "IP: " + LocalIps() + "  порт " + Port;
            else _status = "Не удалось открыть порт " + Port + ".";
        }

        public void JoinIp(string ip)
        {
            GameSession.SinglePc = false;
            Transport.SetConnectionData(ip, Port);
            if (NM.StartClient()) { GameSession.JoinInfo = "Подключено к " + ip; _status = ""; }
            else _status = "Не удалось подключиться к " + ip + ".";
        }

        public async void CreateRelay()
        {
            _busy = true;
            _status = "Подключаюсь к Unity Services…";
            try
            {
                GameSession.SinglePc = false;
                await EnsureSignedIn();
                var options = new SessionOptions { MaxPlayers = 2 }.WithRelayNetwork();
                _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                GameSession.JoinInfo = "Код комнаты: " + _session.Code;
                _status = "";
            }
            catch (Exception e)
            {
                _status = "Ошибка: " + e.Message;
                Debug.LogException(e);
            }
            _busy = false;
        }

        public async void JoinRelay(string code)
        {
            if (string.IsNullOrEmpty(code)) { _status = "Введи код комнаты."; return; }
            _busy = true;
            _status = "Подключаюсь…";
            try
            {
                GameSession.SinglePc = false;
                await EnsureSignedIn();
                _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                GameSession.JoinInfo = "Комната " + code;
                _status = "";
            }
            catch (Exception e)
            {
                _status = "Ошибка: " + e.Message;
                Debug.LogException(e);
            }
            _busy = false;
        }

        static async Task EnsureSignedIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                // Случайный профиль, чтобы два окна на одном ПК не делили один аккаунт.
                var options = new InitializationOptions();
                options.SetProfile("p" + UnityEngine.Random.Range(0, 1000000));
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public async void Leave()
        {
            try
            {
                if (_session != null) await _session.LeaveAsync();
            }
            catch (Exception e) { Debug.LogWarning(e); }
            _session = null;
            if (NM.IsServer || NM.IsClient) NM.Shutdown();
            GameSession.SinglePc = false;
            GameSession.JoinInfo = "";
        }

        static string LocalIps()
        {
            try
            {
                var ips = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.ToString());
                return string.Join(", ", ips);
            }
            catch { return "?"; }
        }
    }
}
