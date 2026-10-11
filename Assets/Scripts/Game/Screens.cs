using UnityEngine;
using UnityEngine.UI;

namespace BlockBlast
{
    /// <summary>Home: background with the baked logo, Adventure / Classic buttons placed where the mockup has them.</summary>
    public sealed class HomeScreen : MonoBehaviour
    {
        public static HomeScreen Create(RectTransform parent)
        {
            var root = UI.Rect("Home", parent).Stretch();
            var s = root.gameObject.AddComponent<HomeScreen>();

            var bg = UI.CoverImage(root, "Bg", Art.Get("home_bg"));
            // Normalized rects measured on the source sheet (home_bg = 1442 x 2959 px).
            var adventure = UI.Btn(bg.transform, "Adventure", Art.Get("btn_adventure"), () => GameApp.I.StartAdventure());
            ((RectTransform)adventure.transform).Anchor(.3155f, .4133f, .7212f, .4860f);

            // Small "Lv N" pill on the Adventure button's top-right corner.
            var pill = UI.Rounded(adventure.transform, "LevelPill", 30, Art.BestYellow);
            pill.rectTransform.Place(new Vector2(1, 1), new Vector2(-20, -6), new Vector2(150, 60));
            pill.Outline(Art.TextNavy, 3);
            s.levelLabel = UI.Txt(pill.transform, "Level", "", 38, Art.TextNavy);
            s.levelLabel.rectTransform.Stretch();

            var classic = UI.Btn(bg.transform, "Classic", Art.Get("btn_classic"), () => GameApp.I.StartClassic());
            ((RectTransform)classic.transform).Anchor(.3155f, .3210f, .7212f, .3961f);

            var best = UI.Txt(bg.transform, "Best", "", 44, Color.white);
            best.rectTransform.Anchor(0, .27f, 1, .31f);
            best.Shadow(new Color(0, 0, 0, .3f), new Vector2(0, -3));
            s.bestLabel = best;
            s.OnEnable();
            return s;
        }

        Text bestLabel, levelLabel;

        void OnEnable()
        {
            if (!bestLabel) return;
            int best = GameApp.BestScore;
            bestLabel.text = best > 0 ? "Best: " + best : "";
            levelLabel.text = "Lv " + GameApp.AdventureLevel;
        }
    }

    /// <summary>Game over: baked "Game Over" + frame background, score drawn into the emptied frame.</summary>
    public sealed class GameOverScreen : MonoBehaviour
    {
        Text label, number;
        int target;

        public static GameOverScreen Create(RectTransform parent)
        {
            var root = UI.Rect("GameOver", parent).Stretch();
            var s = root.gameObject.AddComponent<GameOverScreen>();
            var bg = UI.CoverImage(root, "Bg", Art.Get("gameover_bg"));

            // Inner frame area (gameover_bg = 1440 x 2959 px on the sheet).
            var frame = UI.Rect("ScoreArea", bg.transform).Anchor(.3076f, .5032f, .6833f, .6249f);
            s.label = UI.Txt(frame, "Label", "SCORE", 46, Art.ScoreLabel);
            s.label.rectTransform.Anchor(-.3f, .62f, 1.3f, 1f);
            s.number = UI.Txt(frame, "Score", "0", 150, Color.white);
            s.number.rectTransform.Anchor(-.3f, 0f, 1.3f, .7f);

            var home = UI.Btn(bg.transform, "Home", Art.Get("btn_go_home"), () => GameApp.I.GoHome());
            ((RectTransform)home.transform).Anchor(.1097f, .2893f, .4701f, .3562f);
            var play = UI.Btn(bg.transform, "Play", Art.Get("btn_go_play"), () => GameApp.I.Restart());
            ((RectTransform)play.transform).Anchor(.5160f, .2893f, .8667f, .3562f);
            return s;
        }

        public void Show(int score, bool newBest)
        {
            target = score;
            label.text = newBest ? "NEW BEST SCORE" : "SCORE";
            label.color = newBest ? Art.BestYellow : Art.ScoreLabel;
            number.text = "0";
            Tween.Scale(label.transform, Vector3.zero, Vector3.one, .4f, Ease.OutBack, .2f);
            Tween.Run(number, "count", .9f, k => number.text = Mathf.RoundToInt(target * k).ToString(), Ease.OutCubic, .3f,
                () => Tween.Scale(number.transform, Vector3.one * 1.15f, Vector3.one, .3f));
            // The board already played the game-over sting; only celebrate a new record here.
            if (newBest) Sfx.Play("newbest");
        }
    }

    /// <summary>In-game pause menu: the mockup had no way back home from a match, so it lives here.</summary>
    public sealed class SettingsPopup : MonoBehaviour
    {
        RectTransform panel;
        CanvasGroup group;
        Text soundLabel, musicLabel;

        public static SettingsPopup Create(RectTransform parent)
        {
            var root = UI.Rect("Settings", parent).Stretch();
            var s = root.gameObject.AddComponent<SettingsPopup>();
            s.group = root.gameObject.AddComponent<CanvasGroup>();
            UI.Img(root, "Dim", null, new Color(0, 0, 0, .65f), true).rectTransform.Stretch();

            var border = UI.Rounded(root, "Panel", 48, Art.BoardBorder);
            s.panel = border.rectTransform.Place(new Vector2(.5f, .5f), Vector2.zero, new Vector2(780, 1080));
            UI.Rounded(s.panel, "Fill", 42, Art.PanelFill).rectTransform.Stretch(8);

            var title = UI.Txt(s.panel, "Title", "Settings", 84, Color.white);
            title.rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -105), new Vector2(700, 110));
            title.Shadow(new Color(0, 0, 0, .4f), new Vector2(0, -5));

            var orange = Art.Get("btn_orange");
            Vector2 btnSize = new Vector2(500, 170);
            var resume = UI.Btn(s.panel, "Resume", orange, s.Close, "Resume", 60);
            ((RectTransform)resume.transform).Place(new Vector2(.5f, 1), new Vector2(0, -280), btnSize);

            var sound = UI.Btn(s.panel, "Sound", orange, s.ToggleSound, "", 60);
            ((RectTransform)sound.transform).Place(new Vector2(.5f, 1), new Vector2(0, -460), btnSize);
            s.soundLabel = sound.GetComponentInChildren<Text>();

            var music = UI.Btn(s.panel, "Music", orange, s.ToggleMusic, "", 60);
            ((RectTransform)music.transform).Place(new Vector2(.5f, 1), new Vector2(0, -640), btnSize);
            s.musicLabel = music.GetComponentInChildren<Text>();

            var restart = UI.Btn(s.panel, "Restart", orange, () => { s.Close(); GameApp.I.Restart(); }, "Restart", 60);
            ((RectTransform)restart.transform).Place(new Vector2(.5f, 1), new Vector2(0, -820), btnSize);

            var home = UI.Btn(s.panel, "Home", Art.Get("btn_orange_home"), () => { s.Close(); GameApp.I.GoHome(); });
            ((RectTransform)home.transform).Place(new Vector2(.5f, 1), new Vector2(0, -990), new Vector2(500, 132));
            return s;
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RefreshSound();
            group.alpha = 0;
            Tween.Fade(group, 1, .15f);
            Tween.Scale(panel, Vector3.one * .8f, Vector3.one, .3f);
        }

        public void Close()
        {
            Tween.Fade(group, 0, .12f, 0, () => gameObject.SetActive(false));
        }

        void ToggleSound()
        {
            Sfx.Enabled = !Sfx.Enabled;
            RefreshSound();
        }

        void ToggleMusic()
        {
            Music.Enabled = !Music.Enabled;
            RefreshSound();
        }

        void RefreshSound()
        {
            soundLabel.text = Sfx.Enabled ? "Sound: ON" : "Sound: OFF";
            musicLabel.text = Music.Enabled ? "Music: ON" : "Music: OFF";
        }
    }

    /// <summary>Adventure end-of-level popup: "Level complete" with Next, or "Out of space" with Try Again.</summary>
    public sealed class ResultPopup : MonoBehaviour
    {
        RectTransform panel;
        CanvasGroup group;
        Text title, subtitle, scoreText, primaryLabel;

        public static ResultPopup Create(RectTransform parent)
        {
            var root = UI.Rect("LevelResult", parent).Stretch();
            var s = root.gameObject.AddComponent<ResultPopup>();
            s.group = root.gameObject.AddComponent<CanvasGroup>();
            UI.Img(root, "Dim", null, new Color(0, 0, 0, .65f), true).rectTransform.Stretch();

            var border = UI.Rounded(root, "Panel", 48, Art.BoardBorder);
            s.panel = border.rectTransform.Place(new Vector2(.5f, .5f), Vector2.zero, new Vector2(780, 820));
            UI.Rounded(s.panel, "Fill", 42, Art.PanelFill).rectTransform.Stretch(8);

            s.title = UI.Txt(s.panel, "Title", "", 84, Color.white);
            s.title.rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -100), new Vector2(740, 110));
            s.title.Shadow(new Color(0, 0, 0, .4f), new Vector2(0, -5));
            s.subtitle = UI.Txt(s.panel, "Subtitle", "", 64, Art.ComboYellow, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            s.subtitle.rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -200), new Vector2(740, 90));
            s.subtitle.Outline(Art.ComboOutline, 5);
            s.scoreText = UI.Txt(s.panel, "Score", "", 52, Art.ScoreLabel);
            s.scoreText.rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -300), new Vector2(740, 70));

            var primary = UI.Btn(s.panel, "Primary", Art.Get("btn_orange"), () => { s.Close(); GameApp.I.Restart(); }, "", 60);
            ((RectTransform)primary.transform).Place(new Vector2(.5f, 1), new Vector2(0, -480), new Vector2(500, 170));
            s.primaryLabel = primary.GetComponentInChildren<Text>();

            var home = UI.Btn(s.panel, "Home", Art.Get("btn_orange_home"), () => { s.Close(); GameApp.I.GoHome(); });
            ((RectTransform)home.transform).Place(new Vector2(.5f, 1), new Vector2(0, -660), new Vector2(500, 132));
            root.gameObject.SetActive(false);
            return s;
        }

        public void Open(bool won, int level, int score, int target)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            title.text = "Level " + level;
            subtitle.text = won ? "Complete!" : "Out of space!";
            subtitle.color = won ? Art.ComboYellow : new Color(1f, .55f, .45f);
            scoreText.text = $"Score {score} / {target}";
            primaryLabel.text = won ? "Next Level" : "Try Again";
            group.alpha = 0;
            Tween.Fade(group, 1, .15f);
            Tween.Scale(panel, Vector3.one * .8f, Vector3.one, .3f);
            Tween.Scale(subtitle.transform, Vector3.zero, Vector3.one, .4f, Ease.OutBack, .2f);
        }

        void Close() => Tween.Fade(group, 0, .12f, 0, () => gameObject.SetActive(false));
    }

    /// <summary>Short message pill at the top of the screen.</summary>
    public sealed class Toast : MonoBehaviour
    {
        CanvasGroup group;
        Text text;
        RectTransform pill;

        public static Toast Create(RectTransform parent)
        {
            var root = UI.Rect("Toast", parent).Stretch();
            var t = root.gameObject.AddComponent<Toast>();
            t.group = root.gameObject.AddComponent<CanvasGroup>();
            t.group.blocksRaycasts = false;
            t.group.alpha = 0;
            var bg = UI.Rounded(root, "Pill", 50, new Color(.04f, .08f, .25f, .92f));
            t.pill = bg.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(0, 520), new Vector2(860, 120));
            t.text = UI.Txt(t.pill, "Text", "", 46, Color.white);
            t.text.rectTransform.Stretch();
            return t;
        }

        public void Show(string message)
        {
            transform.SetAsLastSibling();
            text.text = message;
            pill.sizeDelta = new Vector2(Mathf.Max(500, text.preferredWidth + 100), 120);
            group.alpha = 1;
            Tween.Scale(pill, Vector3.one * .7f, Vector3.one, .3f);
            Tween.Fade(group, 0, .35f, 1.6f);
        }
    }
}
