using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlockBlast
{
    /// <summary>Classic endless mode: 8x8 board, three pieces per deal, line clears, combos and boosters.</summary>
    public sealed class GameScreen : MonoBehaviour
    {
        const int N = BoardModel.Size;
        const float Cell = 122f;
        const float TrayScale = .5f;
        const float BoardY = 110f;
        const int BombRadius = 1;          // plus-shaped blast, as drawn in the mockup
        const int RewardEvery = 1000;      // free random booster every N points
        const int ComboResetMoves = 3;     // placements without a clear before the combo drops

        enum Booster { Bomb, Destroy, Refresh, Plus1 }

        static readonly string[] BoosterSprites = { "badge_bomb", "badge_destroy", "badge_refresh", "badge_plus1" };
        static readonly string[] BoosterNames = { "Bomb", "Destroy 1 block", "Refresh", "+1 Block" };
        static readonly string[] BoosterHints =
        {
            "Tap the board to drop the bomb", "Tap a block to destroy it", "", "Tap an empty cell to add a rainbow block"
        };
        static readonly int[] StartCounts = { 1, 2, 2, 2 };

        readonly BoardModel model = new BoardModel();
        readonly System.Random rng = new System.Random();
        readonly List<int> rows = new List<int>(), cols = new List<int>();
        readonly Image[,] blocks = new Image[N, N];
        readonly Image[,] ghosts = new Image[N, N];
        readonly List<Vector2Int> highlighted = new List<Vector2Int>();
        readonly PieceSlot[] slots = new PieceSlot[3];
        readonly Image[] boosterIcons = new Image[4];
        readonly Text[] boosterCounts = new Text[4];
        readonly int[] boosterLeft = new int[4];

        RectTransform board, boardFx, dragLayer, progressBar, progressFill;
        Text scoreText, bestText, scoreLabel, targetText;
        Image crown;

        int score, shownScore, best, startBest, combo, movesSinceClear, nextReward;
        bool busy, gameOver, warnedStuck;

        // adventure: reach the target score on a board that starts with obstacles
        bool adventure;
        int level, target;

        public bool IsAdventure => adventure;
        Booster? armed;

        // drag state
        PieceSlot dragging;
        int dragPointer;
        bool previewValid;
        Vector2Int previewAt;

        // ------------------------------------------------------------------ build

        public static GameScreen Create(RectTransform parent)
        {
            var root = UI.Rect("Game", parent).Stretch();
            var s = root.gameObject.AddComponent<GameScreen>();
            s.Build(root);
            return s;
        }

        void Build(RectTransform root)
        {
            UI.Img(root, "Bg", Art.Get("game_bg")).rectTransform.Stretch();
            var content = UI.Rect("Content", root).Stretch();
            content.gameObject.AddComponent<SafeArea>();

            BuildHeader(content);
            BuildBoard(content);
            BuildBoosters(content);

            for (int i = 0; i < 3; i++)
            {
                var rt = UI.Img(content, "Slot" + i, null, Color.clear, true).rectTransform;
                rt.Place(new Vector2(.5f, .5f), new Vector2((i - 1) * 340, BoardY - 845), new Vector2(330, 330));
                slots[i] = rt.gameObject.AddComponent<PieceSlot>();
                slots[i].Init(this);
            }

            dragLayer = UI.Rect("DragLayer", content).Stretch();
        }

        void BuildHeader(RectTransform content)
        {
            crown = UI.Img(content, "Crown", Art.Get("icon_crown"));
            crown.preserveAspect = true;
            crown.rectTransform.Place(new Vector2(0, 1), new Vector2(110, -70), new Vector2(84, 62));
            bestText = UI.Txt(content, "Best", "0", 58, Art.BestYellow);
            bestText.rectTransform.Place(new Vector2(0, 1), new Vector2(110, -135), new Vector2(300, 70));
            bestText.Shadow(new Color(0, 0, 0, .35f), new Vector2(0, -4));

            var gear = UI.Btn(content, "Settings", Art.Get("icon_gear"), () => GameApp.I.OpenSettings());
            ((RectTransform)gear.transform).Place(new Vector2(1, 1), new Vector2(-90, -85), new Vector2(92, 94));

            scoreLabel = UI.Txt(content, "ScoreLabel", "SCORE", 52, Art.ScoreLabel);
            scoreLabel.rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -150), new Vector2(400, 60));
            scoreText = UI.Txt(content, "Score", "0", 118, new Color(.9f, .94f, 1f));
            scoreText.rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -238), new Vector2(700, 130));
            scoreText.Shadow(new Color(0, 0, 0, .35f), new Vector2(0, -6));

            // Adventure-only target bar between the score and the board.
            var barBg = UI.Rounded(content, "TargetBar", 14, Art.BoardGrid);
            progressBar = barBg.rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -322), new Vector2(560, 30));
            barBg.Outline(Art.BoardBorder, 3);
            progressFill = UI.Rounded(progressBar, "Fill", 11, Art.BestYellow).rectTransform;
            progressFill.anchorMin = Vector2.zero;
            progressFill.offsetMin = new Vector2(4, 4);
            progressFill.offsetMax = new Vector2(-4, -4);
            targetText = UI.Txt(progressBar, "Target", "", 40, Color.white);
            targetText.rectTransform.Place(new Vector2(1, .5f), new Vector2(110, 0), new Vector2(200, 50));
            targetText.Outline(Art.TextNavy, 3);
        }

        void RefreshProgress()
        {
            if (!adventure) return;
            float k = Mathf.Clamp01((float)shownScore / target);
            progressFill.anchorMax = new Vector2(Mathf.Max(k, .04f), 1);
        }

        void BuildBoard(RectTransform content)
        {
            float inner = N * Cell;
            var frame = UI.Rounded(content, "BoardFrame", 22, Art.BoardBorder, true);
            frame.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(0, BoardY), new Vector2(inner + 30, inner + 30));
            frame.gameObject.AddComponent<BoardInput>().Init(this);
            UI.Rounded(frame.transform, "Grid", 18, Art.BoardGrid).rectTransform.Stretch(5);

            board = UI.Rect("Board", frame.transform);
            board.Place(new Vector2(.5f, .5f), Vector2.zero, new Vector2(inner, inner));

            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                    UI.Rounded(board, $"Cell{x}{y}", 6, Art.BoardCell).rectTransform
                        .Place(new Vector2(.5f, .5f), CellPos(x, y), Vector2.one * (Cell - 6));

            var blockLayer = UI.Rect("Blocks", board).Stretch();
            var ghostLayer = UI.Rect("Ghosts", board).Stretch();
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                {
                    blocks[x, y] = UI.Img(blockLayer, $"B{x}{y}", null);
                    blocks[x, y].rectTransform.Place(new Vector2(.5f, .5f), CellPos(x, y), Vector2.one * (Cell - 2));
                    blocks[x, y].gameObject.SetActive(false);
                    ghosts[x, y] = UI.Img(ghostLayer, $"G{x}{y}", null, new Color(1, 1, 1, .45f));
                    ghosts[x, y].rectTransform.Place(new Vector2(.5f, .5f), CellPos(x, y), Vector2.one * (Cell - 2));
                    ghosts[x, y].gameObject.SetActive(false);
                }
            boardFx = UI.Rect("Fx", board).Stretch();
        }

        void BuildBoosters(RectTransform content)
        {
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var b = UI.Btn(content, BoosterSprites[i], Art.Get(BoosterSprites[i]), () => OnBoosterTap((Booster)index));
                var rt = (RectTransform)b.transform;
                rt.Place(new Vector2(.5f, .5f), new Vector2(-390 + i * 260, BoardY - 590), new Vector2(218, 113));
                boosterIcons[i] = b.GetComponent<Image>();
                // Number sits in the empty right half of the pill.
                var t = UI.Txt(rt, "Count", "0", 60, Color.white);
                t.rectTransform.Anchor(.55f, 0, .9f, 1);
                t.Outline(Art.TextNavy, 3);
                boosterCounts[i] = t;
            }
        }

        static Vector2 CellPos(int x, int y) => new Vector2((x - (N - 1) / 2f) * Cell, ((N - 1) / 2f - y) * Cell);

        // ------------------------------------------------------------------ game flow

        public void NewGame(bool adventureMode = false)
        {
            Tween.Kill(this, "gameover");
            Tween.Kill(this, "levelwin");
            adventure = adventureMode;
            model.Reset();
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                {
                    Tween.Kill(blocks[x, y], "grey");
                    blocks[x, y].gameObject.SetActive(false);
                    blocks[x, y].color = Color.white;
                    ghosts[x, y].gameObject.SetActive(false);
                }
            for (int i = boardFx.childCount - 1; i >= 0; i--) Destroy(boardFx.GetChild(i).gameObject);
            for (int i = dragLayer.childCount - 1; i >= 0; i--) Destroy(dragLayer.GetChild(i).gameObject);
            highlighted.Clear();
            dragging = null;

            score = shownScore = combo = movesSinceClear = 0;
            startBest = best = GameApp.BestScore;
            nextReward = RewardEvery;
            busy = gameOver = warnedStuck = false;
            Disarm();
            for (int i = 0; i < 4; i++) boosterLeft[i] = StartCounts[i];
            RefreshBoosters();
            scoreText.text = "0";
            bestText.text = best.ToString();

            // Header: classic shows the best score, adventure shows the level and its target bar.
            crown.gameObject.SetActive(!adventure);
            bestText.gameObject.SetActive(!adventure);
            progressBar.gameObject.SetActive(adventure);
            scoreLabel.text = "SCORE";
            if (adventure)
            {
                level = GameApp.AdventureLevel;
                target = TargetFor(level);
                scoreLabel.text = "LEVEL " + level;
                targetText.text = target.ToString();
                RefreshProgress();
                BuildLevel(level);
            }

            // The previous match's tray pieces must go, otherwise the new deal is drawn on top of them.
            foreach (var s in slots)
            {
                if (s.View) Destroy(s.View.gameObject);
                s.Clear();
                s.ResetInput();
            }
            Deal();
        }

        static int TargetFor(int lvl) => Mathf.Min(150 + lvl * 100, 3000);

        /// <summary>Deterministic, mirror-symmetric obstacle layout for a level; never contains a full line.</summary>
        void BuildLevel(int lvl)
        {
            var r = new System.Random(lvl * 7919 + 13);
            int pairs = Mathf.Min(3 + lvl, 13);
            for (int attempt = 0; attempt < 50; attempt++)
            {
                model.Reset();
                int a = Art.PieceColors[r.Next(Art.PieceColors.Length)];
                int b = Art.PieceColors[r.Next(Art.PieceColors.Length)];
                for (int i = 0; i < pairs; i++)
                {
                    // Keep the top two rows open so the first deal always has room.
                    int x = r.Next(N / 2), y = 2 + r.Next(N - 2);
                    int color = y % 2 == 0 ? a : b;
                    model.Set(x, y, color);
                    model.Set(N - 1 - x, y, color);
                }
                model.FindFullLines(rows, cols);
                if (rows.Count + cols.Count == 0) break;
            }
            if (rows.Count + cols.Count > 0) model.ClearLines(rows, cols);

            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                {
                    if (!model.IsFilled(x, y)) continue;
                    ShowBlock(x, y);
                    Tween.Scale(blocks[x, y].transform, Vector3.zero, Vector3.one, .3f, Ease.OutBack, .25f + (x + y) * .025f);
                }
        }

        void Update()
        {
            if (shownScore != score)
            {
                int step = Mathf.Max(1, Mathf.CeilToInt((score - shownScore) * 12f * Time.unscaledDeltaTime));
                shownScore = Mathf.Min(score, shownScore + step);
                scoreText.text = shownScore.ToString();
                RefreshProgress();
            }
            if (armed.HasValue)
            {
                float k = 1 + .07f * Mathf.Sin(Time.unscaledTime * 9f);
                boosterIcons[(int)armed.Value].transform.localScale = Vector3.one * k;
            }
        }

        void OnDisable()
        {
            if (!adventure && score > GameApp.BestScore) GameApp.BestScore = score;
        }

        void AddScore(int amount)
        {
            score += amount;
            if (!adventure && score > best)
            {
                best = score;
                bestText.text = best.ToString();
            }
            if (adventure && !gameOver && score >= target)
            {
                LevelComplete();
                return;
            }
            while (score >= nextReward)
            {
                nextReward += RewardEvery;
                var b = (Booster)rng.Next(4);
                boosterLeft[(int)b]++;
                RefreshBoosters();
                Tween.Scale(boosterIcons[(int)b].transform, Vector3.one * 1.4f, Vector3.one, .5f);
                GameApp.I.ShowToast($"Bonus: +1 {BoosterNames[(int)b]}!");
                Sfx.Play("bonus");
            }
        }

        // ------------------------------------------------------------------ dealing

        void Deal()
        {
            var shapes = PickFairSet();
            for (int i = 0; i < 3; i++)
            {
                int color = Art.PieceColors[rng.Next(Art.PieceColors.Length)];
                var view = BuildPiece(shapes[i], color, slots[i].transform);
                slots[i].Set(shapes[i], color, view);
                view.localScale = Vector3.one * TrayScale;
                view.anchoredPosition = new Vector2(700, 0);
                Tween.Run(view, "move", .35f, k => view.anchoredPosition = new Vector2(700 * (1 - k), 0), Ease.OutCubic, i * .06f);
            }
            RefreshPieceStates();
        }

        /// <summary>Prefer a set that can be fully placed (like the original), fall back to one that has at least one move.</summary>
        PieceShape[] PickFairSet()
        {
            var set = new PieceShape[3];
            for (int attempt = 0; attempt < 60; attempt++)
            {
                for (int i = 0; i < 3; i++) set[i] = ShapeLibrary.Random(rng);
                if (attempt < 30 ? AllPlaceable(set) : AnyPlaceable(set)) return set;
            }
            return set;
        }

        bool AnyPlaceable(PieceShape[] set)
        {
            foreach (var s in set) if (model.CanPlaceAnywhere(s)) return true;
            return false;
        }

        static readonly int[][] Orders = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };

        bool AllPlaceable(PieceShape[] set)
        {
            var sim = new BoardModel();
            foreach (var order in Orders)
            {
                for (int x = 0; x < N; x++)
                    for (int y = 0; y < N; y++)
                        sim.Set(x, y, model[x, y]);
                bool ok = true;
                foreach (int i in order)
                {
                    if (!PlaceFirstFit(sim, set[i])) { ok = false; break; }
                    sim.FindFullLines(rows, cols);
                    sim.ClearLines(rows, cols);
                }
                if (ok) return true;
            }
            return false;
        }

        static bool PlaceFirstFit(BoardModel b, PieceShape s)
        {
            for (int y = 0; y <= N - s.Height; y++)
                for (int x = 0; x <= N - s.Width; x++)
                    if (b.CanPlace(s, x, y)) { b.Place(s, x, y, 0); return true; }
            return false;
        }

        RectTransform BuildPiece(PieceShape shape, int color, Transform parent)
        {
            var root = UI.Rect("Piece", parent);
            root.Place(new Vector2(.5f, .5f), Vector2.zero, new Vector2(shape.Width * Cell, shape.Height * Cell));
            foreach (var c in shape.Cells)
                UI.Img(root, "C", Art.Block(color)).rectTransform
                    .Place(new Vector2(.5f, .5f), PieceCellPos(shape, c), Vector2.one * (Cell - 2));
            return root;
        }

        static Vector2 PieceCellPos(PieceShape s, Vector2Int c) =>
            new Vector2((c.x - (s.Width - 1) / 2f) * Cell, ((s.Height - 1) / 2f - c.y) * Cell);

        /// <summary>Dims tray pieces that no longer fit anywhere and detects stuck / game-over states.</summary>
        void RefreshPieceStates()
        {
            if (gameOver) return;
            bool anyPiece = false, anyMove = false;
            foreach (var s in slots)
            {
                if (s.Shape == null) continue;
                anyPiece = true;
                bool fits = model.CanPlaceAnywhere(s.Shape);
                anyMove |= fits;
                foreach (var img in s.View.GetComponentsInChildren<Image>())
                    img.color = fits ? Color.white : new Color(.45f, .45f, .5f, .9f);
            }
            if (!anyPiece || anyMove) { warnedStuck = false; return; }

            int boostersLeft = 0;
            foreach (int n in boosterLeft) boostersLeft += n;
            if (boostersLeft > 0)
            {
                if (!warnedStuck) GameApp.I.ShowToast("No space left! Use a booster");
                warnedStuck = true;
                return;
            }
            GameOver();
        }

        void GameOver()
        {
            gameOver = busy = true;
            Disarm();
            bool newBest = !adventure && score > startBest;
            if (!adventure && score > GameApp.BestScore) GameApp.BestScore = score;
            Sfx.Play("gameover");
            Music.Duck(1.5f);
            // Grey the board out row by row, then hand over to the Game Over screen.
            for (int y = N - 1; y >= 0; y--)
                for (int x = 0; x < N; x++)
                {
                    var img = blocks[x, y];
                    if (!img.gameObject.activeSelf) continue;
                    Tween.Run(img, "grey", .2f, k => img.color = Color.Lerp(Color.white, new Color(.4f, .4f, .45f), k),
                        Ease.Linear, (N - 1 - y) * .07f);
                }
            Tween.Run(this, "gameover", 1.5f, null, Ease.Linear, 0, () =>
            {
                if (adventure) GameApp.I.ShowLevelResult(false, level, score, target);
                else GameApp.I.ShowGameOver(score, newBest);
            });
        }

        void LevelComplete()
        {
            gameOver = busy = true;
            Disarm();
            GameApp.AdventureLevel = level + 1;
            Tween.Run(this, "levelwin", .5f, null, Ease.Linear, 0, () =>
            {
                Praise("Level Complete!");
                Sfx.Play("win");
                Music.Duck(2f);
            });
            Tween.Run(this, "gameover", 1.9f, null, Ease.Linear, 0, () => GameApp.I.ShowLevelResult(true, level, score, target));
        }

        // ------------------------------------------------------------------ dragging

        public bool BeginDrag(PieceSlot slot, PointerEventData e)
        {
            if (busy || gameOver || dragging || slot.Shape == null) return false;
            Disarm();
            dragging = slot;
            dragPointer = e.pointerId;
            slot.View.SetParent(dragLayer, true);
            Tween.Kill(slot.View, "move");
            var view = slot.View;
            Vector3 from = view.localScale;
            Tween.Run(view, "scale", .12f, k => view.localScale = Vector3.Lerp(from, Vector3.one, k));
            Sfx.Play("pick");
            Drag(slot, e);
            return true;
        }

        public void Drag(PieceSlot slot, PointerEventData e)
        {
            if (dragging != slot || e.pointerId != dragPointer) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, e.position, e.pressEventCamera, out var lp);
            var shape = slot.Shape;
            // Lift the piece above the finger so it stays visible.
            var pos = lp + new Vector2(0, Cell * 1.3f + shape.Height * Cell * .5f);
            slot.View.anchoredPosition = pos;

            // Board cell under the piece's top-left bounding-box cell (computed at full scale, not the animating one).
            Vector3 world = dragLayer.TransformPoint(pos + PieceCellPos(shape, Vector2Int.zero));
            Vector2 bl = board.InverseTransformPoint(world);
            int ox = Mathf.RoundToInt(bl.x / Cell + (N - 1) / 2f);
            int oy = Mathf.RoundToInt((N - 1) / 2f - bl.y / Cell);

            bool valid = model.CanPlace(shape, ox, oy);
            var at = new Vector2Int(ox, oy);
            if (valid == previewValid && at == previewAt) return;
            ClearPreview();
            if (valid) ShowPreview(shape, slot.ColorId, ox, oy);
        }

        public void EndDrag(PieceSlot slot, PointerEventData e)
        {
            if (dragging != slot || e.pointerId != dragPointer) return;
            dragging = null;
            bool place = previewValid;
            var at = previewAt;
            ClearPreview();
            if (place)
            {
                Place(slot, at.x, at.y);
                return;
            }
            // Snap back to the tray.
            Sfx.Play("drop");
            var view = slot.View;
            view.SetParent(slot.transform, true);
            Tween.Move(view, Vector2.zero, .2f, Ease.OutCubic);
            Vector3 from = view.localScale;
            Tween.Run(view, "scale", .2f, k => view.localScale = Vector3.Lerp(from, Vector3.one * TrayScale, k));
        }

        void ShowPreview(PieceShape shape, int color, int ox, int oy)
        {
            previewValid = true;
            previewAt = new Vector2Int(ox, oy);
            var sprite = Art.Block(color);
            foreach (var c in shape.Cells)
            {
                var g = ghosts[ox + c.x, oy + c.y];
                g.sprite = sprite;
                g.gameObject.SetActive(true);
            }
            // Lines that would clear take the piece colour, like the original.
            model.FindFullLines(rows, cols, shape, ox, oy);
            foreach (int y in rows) for (int x = 0; x < N; x++) Highlight(x, y, sprite);
            foreach (int x in cols) for (int y = 0; y < N; y++) Highlight(x, y, sprite);
        }

        void Highlight(int x, int y, Sprite sprite)
        {
            if (!model.IsFilled(x, y)) { ghosts[x, y].color = Color.white; return; }
            blocks[x, y].sprite = sprite;
            highlighted.Add(new Vector2Int(x, y));
        }

        void ClearPreview()
        {
            previewValid = false;
            previewAt = new Vector2Int(-99, -99);
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                {
                    ghosts[x, y].gameObject.SetActive(false);
                    ghosts[x, y].color = new Color(1, 1, 1, .45f);
                }
            foreach (var c in highlighted)
                if (model.IsFilled(c.x, c.y)) blocks[c.x, c.y].sprite = Art.Block(model[c.x, c.y]);
            highlighted.Clear();
        }

        // ------------------------------------------------------------------ placing & clearing

        void Place(PieceSlot slot, int ox, int oy)
        {
            var shape = slot.Shape;
            model.Place(shape, ox, oy, slot.ColorId);
            Vector2 center = Vector2.zero;
            foreach (var c in shape.Cells)
            {
                int x = ox + c.x, y = oy + c.y;
                ShowBlock(x, y);
                Tween.Scale(blocks[x, y].transform, Vector3.one * .75f, Vector3.one, .22f);
                center += CellPos(x, y);
            }
            center /= shape.Cells.Length;
            Destroy(slot.View.gameObject);
            slot.Clear();
            Sfx.Play("place");

            AddScore(shape.Cells.Length);
            ResolveLines(center, true);

            bool trayEmpty = true;
            foreach (var s in slots) trayEmpty &= s.Shape == null;
            if (trayEmpty) Deal();
            else RefreshPieceStates();
        }

        void ShowBlock(int x, int y)
        {
            var img = blocks[x, y];
            img.sprite = Art.Block(model[x, y]);
            img.color = Color.white;
            img.gameObject.SetActive(true);
        }

        void ResolveLines(Vector2 origin, bool countsAsMove)
        {
            model.FindFullLines(rows, cols);
            int lines = rows.Count + cols.Count;
            if (lines == 0)
            {
                if (countsAsMove && ++movesSinceClear >= ComboResetMoves) combo = 0;
                return;
            }

            combo++;
            movesSinceClear = 0;
            var cleared = model.ClearLines(rows, cols);
            int gained = 10 * lines * lines * Mathf.Max(1, combo);
            bool allClear = model.IsEmptyBoard();
            if (allClear) gained += 300;

            foreach (var c in cleared) Explode(c.x, c.y, Vector2.Distance(CellPos(c.x, c.y), origin) / Cell * .035f);
            AddScore(gained);
            FloatText("+" + gained, origin, 72);
            Sfx.Play("clear", 1 + .06f * Mathf.Min(combo, 10));

            if (allClear) Praise("Board Clear!");
            else if (lines >= 2) Praise(lines >= 5 ? "Unbelievable!" : lines == 4 ? "Excellent!" : lines == 3 ? "Great!" : "Good!");
            if (combo >= 2) ShowCombo(combo);
        }

        /// <summary>Removes the visual block at (x,y) with a shrink-and-spin burst plus a few sparkles.</summary>
        void Explode(int x, int y, float delay)
        {
            var src = blocks[x, y];
            if (!src.gameObject.activeSelf) return;
            src.gameObject.SetActive(false);

            var fx = UI.Img(boardFx, "Burst", src.sprite);
            fx.rectTransform.Place(new Vector2(.5f, .5f), CellPos(x, y), Vector2.one * (Cell - 2));
            float spin = rng.Next(2) == 0 ? -1 : 1;
            Tween.Run(fx, null, .38f, k =>
            {
                float s = k < .25f ? 1 + k * .8f : 1.2f * (1 - (k - .25f) / .75f);
                fx.rectTransform.localScale = Vector3.one * s;
                fx.rectTransform.localRotation = Quaternion.Euler(0, 0, spin * 90 * k);
                fx.color = new Color(1, 1, 1, 1 - k * k);
            }, Ease.Linear, delay, () => Destroy(fx.gameObject));

            if (rng.Next(3) != 0) return;
            var spark = UI.Img(boardFx, "Spark", Art.Get("diamond"));
            var rt = spark.rectTransform.Place(new Vector2(.5f, .5f), CellPos(x, y), Vector2.one * 46);
            spark.color = Color.clear;
            float ang = (float)rng.NextDouble() * Mathf.PI * 2;
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (120 + rng.Next(140));
            Vector2 start = rt.anchoredPosition;
            Tween.Run(spark, null, .55f, k =>
            {
                rt.anchoredPosition = start + dir * k;
                rt.localScale = Vector3.one * (1.2f - k);
                spark.color = new Color(1, 1, 1, 1 - k);
            }, Ease.OutQuad, delay, () => Destroy(spark.gameObject));
        }

        void FloatText(string text, Vector2 pos, int size)
        {
            var t = UI.Txt(boardFx, "Float", text, size, Color.white).Outline(Art.TextNavy, 4);
            var rt = t.rectTransform.Place(new Vector2(.5f, .5f), pos, new Vector2(400, 100));
            Tween.Run(t, null, .9f, k =>
            {
                rt.anchoredPosition = pos + new Vector2(0, 110 * k);
                t.color = new Color(1, 1, 1, k < .6f ? 1 : 1 - (k - .6f) / .4f);
            }, Ease.OutQuad, 0, () => Destroy(t.gameObject));
        }

        void Praise(string text)
        {
            var t = UI.Txt(boardFx, "Praise", text, 96, Art.ComboYellow, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic)
                .Outline(Art.ComboOutline, 6);
            var rt = t.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(0, 150), new Vector2(900, 130));
            PopAndFade(rt, t.gameObject.AddComponent<CanvasGroup>(), 1.0f);
        }

        void ShowCombo(int n)
        {
            var root = UI.Rect("Combo", boardFx).Place(new Vector2(.5f, .5f), Vector2.zero, new Vector2(600, 160));
            var word = UI.Img(root, "Word", Art.Get("combo_word"));
            word.preserveAspect = true;
            word.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(-70, 0), new Vector2(380, 103));
            var num = UI.Txt(root, "N", n.ToString(), 150, Art.ComboYellow, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic)
                .Outline(Art.ComboOutline, 7);
            num.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(230, 6), new Vector2(240, 180));
            PopAndFade(root, root.gameObject.AddComponent<CanvasGroup>(), .9f);
            Sfx.Play("combo", 1 + .05f * Mathf.Min(n, 10));
        }

        void PopAndFade(RectTransform rt, CanvasGroup g, float hold)
        {
            Tween.Scale(rt, Vector3.zero, Vector3.one, .35f);
            Tween.Fade(g, 0, .3f, hold, () => Destroy(rt.gameObject));
        }

        // ------------------------------------------------------------------ boosters

        void RefreshBoosters()
        {
            for (int i = 0; i < 4; i++)
            {
                boosterCounts[i].text = boosterLeft[i].ToString();
                boosterIcons[i].color = boosterLeft[i] > 0 ? Color.white : new Color(1, 1, 1, .45f);
            }
        }

        void Disarm()
        {
            if (armed.HasValue) boosterIcons[(int)armed.Value].transform.localScale = Vector3.one;
            armed = null;
        }

        void OnBoosterTap(Booster b)
        {
            if (busy || gameOver || dragging) return;
            if (armed == b) { Disarm(); return; }
            Disarm();
            if (boosterLeft[(int)b] <= 0)
            {
                GameApp.I.ShowToast($"No {BoosterNames[(int)b]} left - score {RewardEvery} points to earn more");
                return;
            }
            if (b == Booster.Refresh)
            {
                Consume(b);
                foreach (var s in slots)
                {
                    if (s.View) Destroy(s.View.gameObject);
                    s.Clear();
                }
                Deal();
                return;
            }
            armed = b;
            GameApp.I.ShowToast(BoosterHints[(int)b]);
        }

        void Consume(Booster b)
        {
            boosterLeft[(int)b]--;
            RefreshBoosters();
        }

        public void OnBoardTap(Vector2 screenPos, Camera cam)
        {
            if (!armed.HasValue || busy || gameOver) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(board, screenPos, cam, out var lp);
            int x = Mathf.RoundToInt(lp.x / Cell + (N - 1) / 2f);
            int y = Mathf.RoundToInt((N - 1) / 2f - lp.y / Cell);
            if (!BoardModel.InBounds(x, y)) return;

            switch (armed.Value)
            {
                case Booster.Bomb: UseBomb(x, y); break;
                case Booster.Destroy:
                    if (!model.IsFilled(x, y)) return;
                    Consume(Booster.Destroy);
                    model.Set(x, y, BoardModel.Empty);
                    Explode(x, y, 0);
                    Sfx.Play("bomb");
                    break;
                case Booster.Plus1:
                    if (model.IsFilled(x, y)) return;
                    Consume(Booster.Plus1);
                    model.Set(x, y, Art.Rainbow);
                    ShowBlock(x, y);
                    Tween.Scale(blocks[x, y].transform, Vector3.zero, Vector3.one, .3f);
                    Sfx.Play("place");
                    ResolveLines(CellPos(x, y), false);
                    break;
            }
            Disarm();
            RefreshPieceStates();
        }

        void UseBomb(int cx, int cy)
        {
            Consume(Booster.Bomb);
            Sfx.Play("bomb");
            var bomb = UI.Img(boardFx, "Bomb", Art.Get("bomb"));
            bomb.preserveAspect = true;
            bomb.rectTransform.Place(new Vector2(.5f, .5f), CellPos(cx, cy), Vector2.one * Cell * 1.1f);
            Tween.Scale(bomb.transform, Vector3.zero, Vector3.one, .25f, Ease.OutBack, 0,
                () => Tween.Run(bomb, null, .15f, k => bomb.transform.localScale = Vector3.one * (1 + k * .5f),
                    Ease.InQuad, 0, () => Destroy(bomb.gameObject)));

            var cross = UI.Img(boardFx, "Cross", Art.Get("cross"));
            cross.rectTransform.Place(new Vector2(.5f, .5f), CellPos(cx, cy), Vector2.one * Cell * (BombRadius * 2 + 1));
            Tween.Run(cross, null, .6f, k => cross.color = new Color(1, 1, 1, k < .3f ? k / .3f : 1 - (k - .3f) / .7f),
                Ease.Linear, .1f, () => Destroy(cross.gameObject));

            for (int d = -BombRadius; d <= BombRadius; d++)
            {
                BlastCell(cx + d, cy);
                if (d != 0) BlastCell(cx, cy + d);
            }
        }

        void BlastCell(int x, int y)
        {
            if (!model.IsFilled(x, y)) return;
            model.Set(x, y, BoardModel.Empty);
            Explode(x, y, .3f);
        }
    }

    /// <summary>Tray slot: owns one piece and forwards pointer events to the GameScreen.</summary>
    public sealed class PieceSlot : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const int None = int.MinValue;

        GameScreen owner;
        int pointer = None;

        public PieceShape Shape { get; private set; }
        public int ColorId { get; private set; }
        public RectTransform View { get; private set; }

        public void Init(GameScreen screen) => owner = screen;

        public void Set(PieceShape shape, int color, RectTransform view)
        {
            Shape = shape;
            ColorId = color;
            View = view;
        }

        public void Clear()
        {
            Shape = null;
            View = null;
        }

        public void ResetInput() => pointer = None;

        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != None) return; // ignore a second finger on the same slot
            if (owner.BeginDrag(this, e)) pointer = e.pointerId;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == pointer) owner.Drag(this, e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            pointer = None;
            owner.EndDrag(this, e);
        }
    }

    /// <summary>Forwards taps on the board to the GameScreen (used by targeted boosters).</summary>
    public sealed class BoardInput : MonoBehaviour, IPointerClickHandler
    {
        GameScreen owner;

        public void Init(GameScreen screen) => owner = screen;

        public void OnPointerClick(PointerEventData e) => owner.OnBoardTap(e.position, e.pressEventCamera);
    }
}
