#if ECHO_UGUI
using Echo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Ending
{
    // Интерфейс концовок: выбор у аппарата (только у владельца Героя 2) и полноэкранный экран концовки у обоих.
    public class EndingView
    {
        readonly GameObject _offerRoot;
        readonly RectTransform _offerRect;

        readonly GameObject _screenRoot;
        readonly CanvasGroup _screenGroup;
        readonly Image _background;
        readonly Text _title, _body;
        int _shownChoice;
        float _shownAt;

        public EndingView(Transform parent)
        {
            // ---- Выбор ----
            var offer = UiKit.Box("EndingOffer", parent, UiKit.PanelDark, true, true);
            _offerRect = offer.rectTransform;
            _offerRoot = offer.gameObject;
            var head = UiKit.Label("Title", offer.transform, "Аппарат", 30, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(700f, 40f));
            var hint = UiKit.Label("Hint", offer.transform, "Ты у начала трапа. Реши, чем всё закончится.", 18, UiKit.Soft, TextAnchor.MiddleCenter);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(700f, 30f));
            var loop = UiKit.MakeButton("Loop", offer.transform, "Войти в кольца (петля)", UiKit.Button, () => Choose(EndingState.Loop), 22);
            UiKit.Place(loop.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-175f, 20f), new Vector2(340f, 60f));
            var one = UiKit.MakeButton("OneBody", offer.transform, "Синхронизироваться (одно тело)", UiKit.Button, () => Choose(EndingState.OneBody), 20);
            UiKit.Place(one.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(175f, 20f), new Vector2(340f, 60f));
            _offerRoot.SetActive(false);

            // ---- Экран концовки ----
            _background = UiKit.Box("EndingScreen", parent, Color.black, false, true);
            UiKit.Stretch(_background.rectTransform);
            _screenRoot = _background.gameObject;
            _screenGroup = _screenRoot.AddComponent<CanvasGroup>();

            var strip = UiKit.Box("Strip", _background.transform, new Color(0f, 0f, 0f, 0.62f), false);
            strip.rectTransform.anchorMin = new Vector2(0f, 0f);
            strip.rectTransform.anchorMax = new Vector2(1f, 0.4f);
            strip.rectTransform.offsetMin = Vector2.zero;
            strip.rectTransform.offsetMax = Vector2.zero;

            _title = UiKit.Label("Title", strip.transform, "", 54, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1000f, 70f));
            _body = UiKit.Label("Body", strip.transform, "", 24, Color.white, TextAnchor.UpperCenter);
            UiKit.Place(_body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(980f, 100f));
            var menu = UiKit.MakeButton("Menu", strip.transform, "В меню", UiKit.Button, () => CoreBridge.Leave(), 24);
            UiKit.Place(menu.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(240f, 54f));
            _screenRoot.SetActive(false);
        }

        static void Choose(int choice)
        {
            if (EndingState.I != null) EndingState.I.Choose(choice);
        }

        public void Tick(bool inGame)
        {
            var es = inGame ? EndingState.I : null;
            bool spawned = es != null && es.IsSpawned;
            TickOffer(spawned && es.Offer.Value && es.Choice.Value == EndingState.None);
            TickScreen(spawned ? es.Choice.Value : EndingState.None);
        }

        void TickOffer(bool wanted)
        {
            var h2 = Hero.Get(2);
            bool show = wanted && h2 != null && h2.IsOwner;
            UiKit.SetActive(_offerRoot, show);
            if (!show) return;

            // Панель в той половине экрана, где играет Герой 2.
            var cams = SplitCameras.I;
            bool top = cams != null && cams.TopHero == h2;
            UiKit.Place(_offerRect, new Vector2(0.5f, top ? 0.75f : 0.25f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 170f));
        }

        void TickScreen(int choice)
        {
            if (choice == EndingState.None)
            {
                _shownChoice = EndingState.None;
                UiKit.SetActive(_screenRoot, false);
                return;
            }
            if (choice != _shownChoice)
            {
                _shownChoice = choice;
                _shownAt = Time.unscaledTime;
                var assets = EchoUiAssets.I;
                Sprite art = assets != null ? (choice == EndingState.Loop ? assets.endLoop : assets.endOneBody) : null;
                if (art != null)
                {
                    _background.sprite = art;
                    _background.color = Color.white;
                }
                else
                {
                    // Заглушка, пока нет end_loop.png / end_onebody.png.
                    _background.sprite = UiKit.White;
                    _background.color = choice == EndingState.Loop ? new Color(0.16f, 0.07f, 0.3f, 1f) : new Color(0.04f, 0.22f, 0.24f, 1f);
                }
                UiKit.Set(_title, EndingState.Title(choice));
                UiKit.Set(_body, EndingState.Text(choice));
                UiKit.SetActive(_screenRoot, true);
            }
            _screenGroup.alpha = Mathf.Clamp01((Time.unscaledTime - _shownAt) / 1.5f);
        }
    }
}
#endif
