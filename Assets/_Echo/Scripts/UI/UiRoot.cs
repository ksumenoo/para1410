#if ECHO_UGUI
using Echo.Ending;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Echo.UI
{
    // Корень интерфейса на uGUI (Canvas Screen Space Overlay). Создаётся сам при загрузке сцены.
    // Меню, HUD, пауза (Esc), кастомизация и концовки. F9 — показать/скрыть старый IMGUI-интерфейс ядра.
    public class UiRoot : MonoBehaviour
    {
        public static UiRoot I { get; private set; }

        MenuView _menu;
        HudView _hud;
        AppearanceView _look;
        EndingView _ending;
        PauseView _pause;
        IntroView _intro;
        FlashView _flash;
        float _nextLegacy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("EchoUI");
            DontDestroyOnLoad(go);
            go.AddComponent<UiRoot>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();

            // Порядок = порядок отрисовки (последний сверху).
            var root = canvasGo.transform;
            _hud = new HudView(root);
            _look = new AppearanceView(root);
            _intro = new IntroView(root);
            _pause = new PauseView(root);
            _ending = new EndingView(root);
            _flash = new FlashView(root);
            _menu = new MenuView(root);
        }

        void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        void Update()
        {
            bool inGame = CoreBridge.InGame;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f9Key.wasPressedThisFrame) UiSettings.HideLegacyUi = !UiSettings.HideLegacyUi;
                if (inGame && kb.escapeKey.wasPressedThisFrame) _pause.Toggle();
            }
            if (!inGame) _pause.Hide();

            if (Time.unscaledTime >= _nextLegacy)
            {
                _nextLegacy = Time.unscaledTime + 0.5f;
                CoreBridge.ApplyLegacyVisibility();
            }

            _menu.Tick(!inGame);
            _hud.Tick(inGame);
            _look.Tick(inGame);
            _ending.Tick(inGame);
            _flash.Tick();
            _intro.Tick(inGame);
            _pause.Tick();
        }
    }
}
#endif
