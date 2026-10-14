using Echo.Audio;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Echo.UI
{
    // Клавиши рации: V — говорить (удерживать), M — микрофон вкл/выкл.
    // Создаётся сам при загрузке сцены; звук помех — через AudioManager.
    public class RadioInput : MonoBehaviour
    {
        static RadioInput _instance;
        bool _wasTransmitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("EchoRadioInput");
            _instance = go.AddComponent<RadioInput>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !CoreBridge.InGame)
            {
                RadioStatus.Transmitting = false;
                _wasTransmitting = false;
                return;
            }

            if (kb.mKey.wasPressedThisFrame)
            {
                RadioStatus.MicOn = !RadioStatus.MicOn;
                AudioManager.Play(Sfx.Click, 0.6f);
            }

            bool talk = RadioStatus.MicOn && kb.vKey.isPressed;
            RadioStatus.Transmitting = talk;
            if (talk != _wasTransmitting)
            {
                _wasTransmitting = talk;
                AudioManager.Play(talk ? Sfx.RadioOn : Sfx.RadioOff, 0.7f);
                // Без голоса рация только шумит помехами.
                if (talk && !RadioStatus.VoiceReady) AudioManager.Play(Sfx.RadioNoise, 0.5f);
            }

            if (string.IsNullOrEmpty(RadioStatus.Info) || !RadioStatus.VoiceReady)
                RadioStatus.Info = RadioStatus.VoiceReady ? "рация" : "рация (без голоса)";
        }
    }
}
