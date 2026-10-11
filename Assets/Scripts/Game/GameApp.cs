using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BlockBlast
{
    /// <summary>Entry point: builds the canvas and all screens from code, then routes between them.
    /// Boots automatically in any scene, so pressing Play in SampleScene is enough.</summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp I { get; private set; }

        public RectTransform Root { get; private set; }

        HomeScreen home;
        GameScreen game;
        GameOverScreen over;
        SettingsPopup settings;
        ResultPopup result;
        Toast toast;

        public static int BestScore
        {
            get => PlayerPrefs.GetInt("best", 0);
            set { PlayerPrefs.SetInt("best", value); PlayerPrefs.Save(); }
        }

        /// <summary>Next adventure level to play (1-based).</summary>
        public static int AdventureLevel
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt("adv_level", 1));
            set { PlayerPrefs.SetInt("adv_level", value); PlayerPrefs.Save(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameApp>()) return;
            new GameObject("GameApp").AddComponent<GameApp>();
        }

        void Awake()
        {
            if (I && I != this) { Destroy(gameObject); return; }
            I = this;
            Application.targetFrameRate = 60;
            BuildCanvas();

            home = HomeScreen.Create(Root);
            game = GameScreen.Create(Root);
            over = GameOverScreen.Create(Root);
            settings = SettingsPopup.Create(Root);
            result = ResultPopup.Create(Root);
            toast = Toast.Create(Root);

            ShowOnly(home);
            ShowSplash();
            Sfx.Preload();
            Music.Play();
        }

        void BuildCanvas()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.layer = 5;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100;
            canvasGo.AddComponent<GraphicRaycaster>();
            Root = (RectTransform)canvasGo.transform;

            // Black backdrop behind letterboxed screens.
            UI.Img(Root, "Backdrop", null, Color.black).rectTransform.Stretch();

            if (!FindAnyObjectByType<EventSystem>())
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }
        }

        void ShowSplash()
        {
            var splash = UI.Rect("Splash", Root).Stretch();
            var group = splash.gameObject.AddComponent<CanvasGroup>();
            UI.CoverImage(splash, "Bg", Art.Get("splash_bg"));
            UI.Img(splash, "Blocker", null, Color.clear, true).rectTransform.Stretch();
            Tween.Fade(group, 0, .45f, 1.3f, () => Destroy(splash.gameObject));
        }

        void ShowOnly(MonoBehaviour screen)
        {
            home.gameObject.SetActive(screen == home);
            game.gameObject.SetActive(screen == game);
            over.gameObject.SetActive(screen == over);
            settings.gameObject.SetActive(false);
            result.gameObject.SetActive(false);
        }

        // ---- navigation ----

        public void GoHome() => Transition(() => ShowOnly(home));

        public void StartClassic() => StartGame(false);

        public void StartAdventure() => StartGame(true);

        void StartGame(bool adventure) => Transition(() =>
        {
            ShowOnly(game);
            game.NewGame(adventure);
        });

        public void ShowLevelResult(bool won, int level, int score, int target) => result.Open(won, level, score, target);

        public void ShowGameOver(int score, bool newBest) => Transition(() =>
        {
            ShowOnly(over);
            over.Show(score, newBest);
        });

        public void OpenSettings() => settings.Open();

        /// <summary>Restarts the current mode (in adventure: the next unbeaten level).</summary>
        public void Restart() => StartGame(game.IsAdventure);

        public void ShowToast(string message) => toast.Show(message);

        /// <summary>Quick fade-through-black between screens.</summary>
        void Transition(System.Action swap)
        {
            var fade = UI.Img(Root, "Fade", null, new Color(0, 0, 0, 0), true);
            fade.rectTransform.Stretch();
            Tween.Run(fade, null, .15f, k => fade.color = new Color(0, 0, 0, k), Ease.Linear, 0, () =>
            {
                swap();
                fade.transform.SetAsLastSibling();
                Tween.Run(fade, null, .2f, k => fade.color = new Color(0, 0, 0, 1 - k), Ease.Linear, 0,
                    () => Destroy(fade.gameObject));
            });
        }
    }
}
