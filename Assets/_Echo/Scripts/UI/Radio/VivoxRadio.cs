#if ECHO_VIVOX
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

namespace Echo.UI
{
    // Голосовая рация на Vivox. Работает только при установленном com.unity.services.vivox (define ECHO_VIVOX).
    // Канал: код комнаты Relay, либо "echo-" + IP. Микрофон закрыт, открывается удержанием V (RadioStatus.Transmitting).
    public class VivoxRadio : MonoBehaviour
    {
        enum Phase { Idle, Starting, Ready, Failed }

        static VivoxRadio _instance;

        Phase _phase = Phase.Idle;
        readonly List<string> _wanted = new List<string>();
        readonly List<VivoxParticipant> _remote = new List<VivoxParticipant>();
        bool _subscribed;
        bool _micOpen;
        float _nextTry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_instance != null) return;
            _instance = new GameObject("EchoVivoxRadio").AddComponent<VivoxRadio>();
        }

        void Update()
        {
            bool inGame = CoreBridge.InGame && !GameSession.SinglePc;

            if (_phase == Phase.Idle || _phase == Phase.Failed)
            {
                if (_phase == Phase.Idle && inGame && Time.time >= _nextTry && TryResolveChannels())
                    StartRadio();
            }
            if (_phase == Phase.Ready && !inGame) StopRadio();
            if (_phase != Phase.Ready) { RadioStatus.PartnerSpeaking = false; return; }

            // Push-to-talk
            bool wantOpen = RadioStatus.Transmitting;
            if (wantOpen != _micOpen && VivoxService.Instance != null)
            {
                _micOpen = wantOpen;
                if (wantOpen) VivoxService.Instance.UnmuteInputDevice();
                else VivoxService.Instance.MuteInputDevice();
            }

            bool speaking = false;
            foreach (var p in _remote) if (p != null && p.SpeechDetected) { speaking = true; break; }
            RadioStatus.PartnerSpeaking = speaking;
        }

        // Имена каналов из текста, который ядро показывает в углу экрана.
        bool TryResolveChannels()
        {
            _wanted.Clear();
            string info = GameSession.JoinInfo ?? "";
            if (info.StartsWith("Код комнаты:")) AddCode(info.Substring("Код комнаты:".Length));
            else if (info.StartsWith("Комната ")) AddCode(info.Substring("Комната ".Length));
            else if (info.StartsWith("IP:"))
            {
                string list = info.Substring(3);
                int cut = list.IndexOf("порт");
                if (cut >= 0) list = list.Substring(0, cut);
                foreach (var ip in list.Split(','))
                    if (!string.IsNullOrWhiteSpace(ip) && _wanted.Count < 4) _wanted.Add("echo-" + ip.Trim());
            }
            else if (info.StartsWith("Подключено к ")) _wanted.Add("echo-" + info.Substring("Подключено к ".Length).Trim());
            return _wanted.Count > 0;
        }

        void AddCode(string code)
        {
            code = code.Trim();
            if (code.Length > 0) _wanted.Add("echo-" + code);
        }

        async void StartRadio()
        {
            _phase = Phase.Starting;
            RadioStatus.Info = "рация: подключаюсь…";
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    var options = new InitializationOptions();
                    options.SetProfile("v" + UnityEngine.Random.Range(0, 1000000));
                    await UnityServices.InitializeAsync(options);
                }
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (!VivoxService.Instance.IsInitialized)
                    await VivoxService.Instance.InitializeAsync();

                Subscribe();
                var hero = SplitCameras.MyHero();
                await VivoxService.Instance.LoginAsync(new LoginOptions { DisplayName = "Hero" + (hero != null ? hero.HeroId : 0) });
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Echo.UI] Рация (Vivox) не запустилась: " + e.Message);
                _phase = Phase.Failed;
                _nextTry = Time.time + 15f;
                RadioStatus.VoiceReady = false;
                RadioStatus.Info = "рация (без голоса)";
                _phase = Phase.Idle;
            }
        }

        void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            var v = VivoxService.Instance;
            v.LoggedIn += OnLoggedIn;
            v.LoggedOut += OnLoggedOut;
            v.ChannelJoined += OnChannelJoined;
            v.ParticipantAddedToChannel += OnParticipantAdded;
            v.ParticipantRemovedFromChannel += OnParticipantRemoved;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            var v = VivoxService.Instance;
            if (v == null) return;
            v.LoggedIn -= OnLoggedIn;
            v.LoggedOut -= OnLoggedOut;
            v.ChannelJoined -= OnChannelJoined;
            v.ParticipantAddedToChannel -= OnParticipantAdded;
            v.ParticipantRemovedFromChannel -= OnParticipantRemoved;
        }

        async void OnLoggedIn()
        {
            try
            {
                foreach (var channel in _wanted.ToArray())
                    await VivoxService.Instance.JoinGroupChannelAsync(channel, ChatCapability.AudioOnly);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Echo.UI] Рация: не удалось войти в канал: " + e.Message);
            }
        }

        void OnLoggedOut()
        {
            _remote.Clear();
            RadioStatus.VoiceReady = false;
        }

        void OnChannelJoined(string channelName)
        {
            _micOpen = false;
            VivoxService.Instance.MuteInputDevice(); // говорим только по удержанию V
            _phase = Phase.Ready;
            RadioStatus.VoiceReady = true;
            RadioStatus.Info = "рация: " + channelName;
        }

        void OnParticipantAdded(VivoxParticipant p)
        {
            if (!p.IsSelf && !_remote.Contains(p)) _remote.Add(p);
        }

        void OnParticipantRemoved(VivoxParticipant p) => _remote.Remove(p);

        async void StopRadio()
        {
            _phase = Phase.Idle;
            _remote.Clear();
            RadioStatus.VoiceReady = false;
            RadioStatus.PartnerSpeaking = false;
            _nextTry = Time.time + 2f;
            try
            {
                var v = VivoxService.Instance;
                if (v != null)
                {
                    await v.LeaveAllChannelsAsync();
                    await v.LogoutAsync();
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Echo.UI] Рация: выход: " + e.Message); }
            Unsubscribe();
        }

        void OnDestroy()
        {
            Unsubscribe();
            if (_instance == this) _instance = null;
        }
    }
}
#endif
