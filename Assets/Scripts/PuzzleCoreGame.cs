using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GroundZero.PuzzleCore
{
    public sealed class PuzzleCoreGame : MonoBehaviour
    {
        private const string SaveKey = "GroundZero.PuzzleCore.Save";
        private const int DailySeedOffset = 73471;

        private static readonly Color Background = Rgb(15, 18, 24);
        private static readonly Color Panel = Rgb(29, 34, 45);
        private static readonly Color PanelSoft = Rgb(38, 45, 58);
        private static readonly Color Tile = Rgb(54, 62, 78);
        private static readonly Color TileDark = Rgb(21, 25, 33);
        private static readonly Color TextMain = Rgb(238, 241, 245);
        private static readonly Color TextMuted = Rgb(167, 176, 190);
        private static readonly Color Success = Rgb(82, 196, 126);
        private static readonly Color Warning = Rgb(238, 180, 79);
        private static readonly Color Danger = Rgb(224, 90, 90);

        private static readonly Color[] Palette =
        {
            Rgb(229, 81, 94),
            Rgb(78, 166, 255),
            Rgb(92, 204, 139),
            Rgb(244, 190, 78),
            Rgb(171, 117, 255),
            Rgb(56, 210, 204),
            Rgb(246, 132, 77),
            Rgb(235, 110, 184),
            Rgb(142, 218, 92)
        };

        private static readonly Vector2Int GridUp = new Vector2Int(0, -1);
        private static readonly Vector2Int GridDown = new Vector2Int(0, 1);
        private static readonly Vector2Int GridLeft = new Vector2Int(-1, 0);
        private static readonly Vector2Int GridRight = new Vector2Int(1, 0);

        private readonly PuzzleType[] rotation =
        {
            PuzzleType.Sliding,
            PuzzleType.Maze,
            PuzzleType.LightsOut,
            PuzzleType.Memory,
            PuzzleType.Flow,
            PuzzleType.WaterSort,
            PuzzleType.Nonogram,
            PuzzleType.Match3,
            PuzzleType.Sudoku,
            PuzzleType.Sokoban
        };

        private Font defaultFont;
        private RectTransform root;
        private SaveData save;
        private IPuzzle activePuzzle;
        private int masterSeed;
        private int levelIndex;
        private bool levelSolved;
        private bool runActive;
        private bool endlessMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<PuzzleCoreGame>() != null)
            {
                return;
            }

            var host = new GameObject("Puzzle Core Game");
            host.AddComponent<PuzzleCoreGame>();
        }

        private void Awake()
        {
            if (FindObjectsByType<PuzzleCoreGame>(FindObjectsInactive.Exclude).Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null)
            {
                defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            save = LoadSave();
            BuildCanvas();
            ShowMainMenu();
        }

        private void Update()
        {
            if (!runActive || activePuzzle == null || levelSolved)
            {
                return;
            }

            if (!TryReadPuzzleKey(out var key))
            {
                return;
            }

            if (key == KeyCode.Escape)
            {
                ShowMainMenu();
                return;
            }

            if (key == KeyCode.R)
            {
                activePuzzle.Reset();
                RenderGame();
                return;
            }

            if (activePuzzle.HandleKey(key))
            {
                AfterPuzzleChanged();
            }
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("Puzzle Core Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var eventObject = new GameObject("EventSystem", typeof(EventSystem));
                eventObject.transform.SetParent(transform, false);
                eventSystem = eventObject.GetComponent<EventSystem>();
            }

            ConfigureEventSystem(eventSystem.gameObject);

            root = canvasObject.GetComponent<RectTransform>();
            Stretch(root);

            var image = canvasObject.AddComponent<Image>();
            image.color = Background;
        }

        private void ShowMainMenu()
        {
            runActive = false;
            endlessMode = false;
            activePuzzle = null;
            Clear(root);

            var page = Stack(root, "Main Menu", 34, TextAnchor.UpperCenter);
            page.padding = new RectOffset(80, 80, 72, 72);

            Label(page.transform, "Puzzle Core: Ground Zero", 58, FontStyle.Bold, TextAnchor.MiddleCenter, TextMain, 84);
            Label(page.transform, "Endless verified puzzle runs for desktop and Steam.", 24, FontStyle.Normal, TextAnchor.MiddleCenter, TextMuted, 42);

            var stats = PanelBox(page.transform, "Stats", PanelSoft);
            var statsStack = Stack(stats, "Stats Stack", 12, TextAnchor.MiddleCenter);
            statsStack.padding = new RectOffset(28, 28, 22, 22);
            var saveSummary = save.HasRun
                ? "Saved run: level " + (save.LevelIndex + 1) + " / seed " + save.MasterSeed
                : "No active endless run yet";
            Label(statsStack.transform, saveSummary, 24, FontStyle.Bold, TextAnchor.MiddleCenter, TextMain, 34);
            Label(statsStack.transform, "Solved total: " + save.CompletedLevels + "    Best streak: " + save.BestStreak, 20, FontStyle.Normal, TextAnchor.MiddleCenter, TextMuted, 30);
            AddLayout(stats, -1, 118);

            var buttons = Stack(page.transform, "Menu Buttons", 14, TextAnchor.MiddleCenter);
            buttons.padding = new RectOffset(580, 580, 0, 0);

            if (save.HasRun)
            {
                Button(buttons.transform, "Continue Endless Run", ContinueRun, Success, TextMain, 24, 64);
            }

            Button(buttons.transform, "New Endless Run", () =>
            {
                var seed = unchecked((int)(DateTime.UtcNow.Ticks & 0x7fffffff));
                StartRun(seed, 0);
            }, Rgb(72, 116, 220), TextMain, 24, 64);

            Button(buttons.transform, "Daily Run", () =>
            {
                var today = DateTime.UtcNow.Date;
                var seed = today.Year * 10000 + today.Month * 100 + today.Day + DailySeedOffset;
                StartRun(seed, 0);
            }, Rgb(102, 80, 188), TextMain, 24, 64);

            Button(buttons.transform, "Puzzle Lab", ShowPuzzleLab, PanelSoft, TextMain, 24, 64);
            Button(buttons.transform, "Quit", Application.Quit, TileDark, TextMuted, 22, 56);

            var footer = Label(page.transform, "Keyboard: WASD/arrows where movement applies, R resets, Esc returns to menu.", 18, FontStyle.Normal, TextAnchor.LowerCenter, TextMuted, 42);
            footer.resizeTextForBestFit = true;
        }

        private void ShowPuzzleLab()
        {
            runActive = false;
            endlessMode = false;
            activePuzzle = null;
            Clear(root);

            var page = Stack(root, "Puzzle Lab", 24, TextAnchor.UpperCenter);
            page.padding = new RectOffset(72, 72, 56, 56);
            Label(page.transform, "Puzzle Lab", 46, FontStyle.Bold, TextAnchor.MiddleCenter, TextMain, 70);
            Label(page.transform, "Start a single generated puzzle at any difficulty.", 22, FontStyle.Normal, TextAnchor.MiddleCenter, TextMuted, 38);

            foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
            {
                var rowPanel = PanelBox(page.transform, difficulty.ToString(), Panel);
                var row = Row(rowPanel, "Row", 10, TextAnchor.MiddleCenter);
                row.padding = new RectOffset(12, 12, 10, 10);
                Label(row.transform, difficulty.ToString(), 20, FontStyle.Bold, TextAnchor.MiddleCenter, TextMain, 160, 52);

                foreach (PuzzleType type in rotation)
                {
                    var capturedType = type;
                    var capturedDifficulty = difficulty;
                    Button(row.transform, PuzzleTitle(type), () =>
                    {
                        masterSeed = Hash(unchecked((int)DateTime.UtcNow.Ticks), (int)capturedType * 97 + (int)capturedDifficulty);
                        levelIndex = 0;
                        activePuzzle = CreatePuzzle(capturedType, capturedDifficulty, Hash(masterSeed, 1));
                        levelSolved = false;
                        runActive = true;
                        endlessMode = false;
                        RenderGame();
                    }, Tile, TextMain, 16, 52, 140);
                }

                AddLayout(rowPanel, -1, 74);
            }

            Button(page.transform, "Back", ShowMainMenu, TileDark, TextMuted, 22, 58, 280);
        }

        private void ContinueRun()
        {
            if (!save.HasRun)
            {
                ShowMainMenu();
                return;
            }

            StartRun(save.MasterSeed, save.LevelIndex);
        }

        private void StartRun(int seed, int index)
        {
            masterSeed = seed;
            levelIndex = Math.Max(0, index);
            endlessMode = true;
            save.HasRun = true;
            save.MasterSeed = masterSeed;
            save.LevelIndex = levelIndex;
            Save();
            CreateCurrentLevel();
        }

        private void CreateCurrentLevel()
        {
            var type = rotation[PositiveMod(levelIndex + masterSeed, rotation.Length)];
            var difficulty = DifficultyForLevel(levelIndex);
            var seed = Hash(masterSeed, levelIndex + 1);
            activePuzzle = CreatePuzzle(type, difficulty, seed);
            levelSolved = false;
            runActive = true;
            RenderGame();
        }

        private IPuzzle CreatePuzzle(PuzzleType type, Difficulty difficulty, int seed)
        {
            switch (type)
            {
                case PuzzleType.Sliding:
                    return new SlidingPuzzle(difficulty, seed);
                case PuzzleType.Maze:
                    return new MazePuzzle(difficulty, seed);
                case PuzzleType.Sokoban:
                    return new SokobanPuzzle(difficulty, seed);
                case PuzzleType.Nonogram:
                    return new NonogramPuzzle(difficulty, seed);
                case PuzzleType.LightsOut:
                    return new LightsOutPuzzle(difficulty, seed);
                case PuzzleType.Match3:
                    return new Match3Puzzle(difficulty, seed);
                case PuzzleType.Flow:
                    return new FlowPuzzle(difficulty, seed);
                case PuzzleType.Sudoku:
                    return new SudokuPuzzle(difficulty, seed);
                case PuzzleType.Memory:
                    return new MemoryPuzzle(difficulty, seed);
                case PuzzleType.WaterSort:
                    return new WaterSortPuzzle(difficulty, seed);
                default:
                    throw new ArgumentOutOfRangeException("type", type, null);
            }
        }

        private void RenderGame()
        {
            Clear(root);

            var page = Stack(root, "Game", 18, TextAnchor.UpperCenter);
            page.padding = new RectOffset(42, 42, 34, 30);

            var header = Row(page.transform, "Header", 12, TextAnchor.MiddleLeft);
            header.padding = new RectOffset(22, 22, 14, 14);
            header.GetComponent<Image>().color = Panel;
            AddLayout(header.GetComponent<RectTransform>(), -1, 92);

            var titleStack = Stack(header.transform, "Header Copy", 2, TextAnchor.MiddleLeft);
            AddLayout(titleStack.GetComponent<RectTransform>(), 900, 68, 1);
            Label(titleStack.transform, activePuzzle.Title, 30, FontStyle.Bold, TextAnchor.MiddleLeft, TextMain, 38);
            Label(titleStack.transform, activePuzzle.Objective, 17, FontStyle.Normal, TextAnchor.MiddleLeft, TextMuted, 28);

            var meta = endlessMode
                ? "Level " + (levelIndex + 1) + " / " + activePuzzle.Difficulty + " / seed " + masterSeed
                : activePuzzle.Difficulty + " lab seed";
            Label(header.transform, meta, 19, FontStyle.Normal, TextAnchor.MiddleRight, TextMuted, 440, 52);

            var movesText = activePuzzle is Match3Puzzle match3
                ? "Score " + match3.Score + "/" + match3.TargetScore + "    Moves left " + match3.MovesLeft
                : "Moves " + activePuzzle.Moves;
            Label(header.transform, movesText, 19, FontStyle.Bold, TextAnchor.MiddleRight, TextMain, 310, 52);

            var content = Stack(page.transform, "Content", 16, TextAnchor.UpperCenter);
            content.padding = new RectOffset(18, 18, 18, 18);
            content.GetComponent<Image>().color = PanelSoft;
            AddLayout(content.GetComponent<RectTransform>(), -1, 760, 1);
            activePuzzle.Render(this, content.transform);

            if (activePuzzle.IsSolved)
            {
                levelSolved = true;
                var solved = Row(content.transform, "Solved", 12, TextAnchor.MiddleCenter);
                solved.padding = new RectOffset(20, 20, 12, 12);
                solved.GetComponent<Image>().color = Rgb(35, 74, 50);
                AddLayout(solved.GetComponent<RectTransform>(), -1, 74);
                Label(solved.transform, "Verified clear. The level is complete.", 23, FontStyle.Bold, TextAnchor.MiddleCenter, TextMain, 520, 48);
                if (endlessMode)
                {
                    Button(solved.transform, "Next Level", CompleteAndAdvance, Success, TextMain, 22, 50, 220);
                }
            }
            else if (activePuzzle.IsFailed)
            {
                var failed = Row(content.transform, "Failed", 12, TextAnchor.MiddleCenter);
                failed.padding = new RectOffset(20, 20, 12, 12);
                failed.GetComponent<Image>().color = Rgb(86, 45, 45);
                AddLayout(failed.GetComponent<RectTransform>(), -1, 74);
                Label(failed.transform, "Run failed. Reset this puzzle or skip to another seed.", 22, FontStyle.Bold, TextAnchor.MiddleCenter, TextMain, 620, 48);
                Button(failed.transform, "Reset Puzzle", () =>
                {
                    activePuzzle.Reset();
                    RenderGame();
                }, Warning, TextMain, 22, 50, 220);
            }

            var footer = Row(page.transform, "Footer", 14, TextAnchor.MiddleCenter);
            footer.padding = new RectOffset(18, 18, 10, 10);
            footer.GetComponent<Image>().color = Panel;
            AddLayout(footer.GetComponent<RectTransform>(), -1, 80);
            Button(footer.transform, "Menu", ShowMainMenu, TileDark, TextMuted, 20, 54, 180);
            Button(footer.transform, "Reset", () =>
            {
                activePuzzle.Reset();
                levelSolved = false;
                RenderGame();
            }, Tile, TextMain, 20, 54, 180);
            Button(footer.transform, "Skip", () =>
            {
                if (endlessMode)
                {
                    levelIndex++;
                    save.LevelIndex = levelIndex;
                    Save();
                    CreateCurrentLevel();
                }
                else
                {
                    ShowPuzzleLab();
                }
            }, Tile, TextMain, 20, 54, 180);
            if (endlessMode && activePuzzle.IsSolved)
            {
                Button(footer.transform, "Next", CompleteAndAdvance, Success, TextMain, 20, 54, 180);
            }
        }

        private void AfterPuzzleChanged()
        {
            if (activePuzzle.IsSolved && !levelSolved)
            {
                levelSolved = true;
                if (endlessMode)
                {
                    save.CompletedLevels++;
                    save.CurrentStreak++;
                    save.BestStreak = Math.Max(save.BestStreak, save.CurrentStreak);
                    Save();
                }
            }

            RenderGame();
        }

        private void CompleteAndAdvance()
        {
            if (!endlessMode)
            {
                ShowMainMenu();
                return;
            }

            levelIndex++;
            save.LevelIndex = levelIndex;
            Save();
            CreateCurrentLevel();
        }

        private SaveData LoadSave()
        {
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                return new SaveData();
            }

            try
            {
                return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)) ?? new SaveData();
            }
            catch
            {
                return new SaveData();
            }
        }

        private void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        internal VerticalLayoutGroup Stack(Transform parent, string name, int spacing, TextAnchor alignment)
        {
            var panel = PanelBox(parent, name, Color.clear);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childAlignment = alignment;
            return layout;
        }

        internal HorizontalLayoutGroup Row(Transform parent, string name, int spacing, TextAnchor alignment)
        {
            var panel = PanelBox(parent, name, Color.clear);
            var layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.childAlignment = alignment;
            return layout;
        }

        internal RectTransform PanelBox(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            var image = go.GetComponent<Image>();
            image.color = color;
            return rect;
        }

        internal Text Label(Transform parent, string text, int size, FontStyle style, TextAnchor anchor, Color color, float minHeight)
        {
            return Label(parent, text, size, style, anchor, color, -1, minHeight);
        }

        internal Text Label(Transform parent, string text, int size, FontStyle style, TextAnchor anchor, Color color, float preferredWidth, float minHeight)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = defaultFont;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = false;
            AddLayout(go.GetComponent<RectTransform>(), preferredWidth, minHeight);
            return label;
        }

        internal Button Button(Transform parent, string text, Action onClick, Color background, Color foreground, int fontSize, float height, float preferredWidth = -1)
        {
            var go = new GameObject("Button " + text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = background;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());
            var colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = Lighten(background, 0.12f);
            colors.pressedColor = Darken(background, 0.12f);
            colors.selectedColor = Lighten(background, 0.08f);
            colors.disabledColor = Darken(background, 0.2f);
            button.colors = colors;

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(go.transform, false);
            var rect = textObject.GetComponent<RectTransform>();
            Stretch(rect);
            rect.offsetMin = new Vector2(10, 4);
            rect.offsetMax = new Vector2(-10, -4);
            var label = textObject.GetComponent<Text>();
            label.text = text;
            label.font = defaultFont;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.color = foreground;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = fontSize;
            AddLayout(go.GetComponent<RectTransform>(), preferredWidth, height);
            return button;
        }

        internal RectTransform Grid(Transform parent, string name, int columns, int rows, float cellSize, float spacing = 6)
        {
            var grid = PanelBox(parent, name, Color.clear);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            layout.cellSize = new Vector2(cellSize, cellSize);
            layout.spacing = new Vector2(spacing, spacing);
            layout.childAlignment = TextAnchor.MiddleCenter;
            var width = columns * cellSize + Math.Max(0, columns - 1) * spacing;
            var height = rows * cellSize + Math.Max(0, rows - 1) * spacing;
            AddLayout(grid, width, height);
            return grid;
        }

        internal void TileButton(Transform parent, string text, Action onClick, Color background, Color foreground, int fontSize = 22)
        {
            if (onClick == null)
            {
                Button(parent, text, null, background, foreground, fontSize, -1, -1);
                return;
            }

            Button(parent, text, () => PuzzleAction(onClick), background, foreground, fontSize, -1, -1);
        }

        internal void PuzzleAction(Action action)
        {
            action?.Invoke();
            AfterPuzzleChanged();
        }

        internal void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }

        internal static Color ColorFor(int index)
        {
            if (index < 0)
            {
                return TileDark;
            }

            return Palette[index % Palette.Length];
        }

        internal static string ColorName(int index)
        {
            return ((char)('A' + PositiveMod(index, 26))).ToString();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AddLayout(RectTransform rect, float preferredWidth, float minHeight, float flexibleWidth = 0)
        {
            var layout = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
            if (preferredWidth >= 0)
            {
                layout.preferredWidth = preferredWidth;
            }

            if (minHeight >= 0)
            {
                layout.minHeight = minHeight;
                layout.preferredHeight = minHeight;
            }

            layout.flexibleWidth = flexibleWidth;
        }

        private static Difficulty DifficultyForLevel(int index)
        {
            if (index < 3)
            {
                return Difficulty.Easy;
            }

            if (index < 8)
            {
                return Difficulty.Normal;
            }

            if (index < 15)
            {
                return Difficulty.Hard;
            }

            if (index < 25)
            {
                return Difficulty.Expert;
            }

            return Difficulty.Master;
        }

        private static string PuzzleTitle(PuzzleType type)
        {
            switch (type)
            {
                case PuzzleType.Sliding:
                    return "Sliding";
                case PuzzleType.Maze:
                    return "Maze";
                case PuzzleType.Sokoban:
                    return "Sokoban";
                case PuzzleType.Nonogram:
                    return "Nonogram";
                case PuzzleType.LightsOut:
                    return "Lights Out";
                case PuzzleType.Match3:
                    return "Match-3";
                case PuzzleType.Flow:
                    return "Flow";
                case PuzzleType.Sudoku:
                    return "Sudoku";
                case PuzzleType.Memory:
                    return "Memory";
                case PuzzleType.WaterSort:
                    return "Water Sort";
                default:
                    return type.ToString();
            }
        }

        private static int Hash(int a, int b)
        {
            unchecked
            {
                var h = 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h ^= h >> 13;
                h *= 1274126177u;
                return (int)(h & 0x7fffffff);
            }
        }

        private static int PositiveMod(int value, int modulo)
        {
            var result = value % modulo;
            return result < 0 ? result + modulo : result;
        }

        private static int DiffIndex(Difficulty difficulty)
        {
            return (int)difficulty;
        }

        private static Color Rgb(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        private static Color Lighten(Color color, float amount)
        {
            return Color.Lerp(color, Color.white, amount);
        }

        private static Color Darken(Color color, float amount)
        {
            return Color.Lerp(color, Color.black, amount);
        }

        private static bool DirectionFromKey(KeyCode key, out Vector2Int direction)
        {
            if (key == KeyCode.UpArrow || key == KeyCode.W)
            {
                direction = GridUp;
                return true;
            }

            if (key == KeyCode.DownArrow || key == KeyCode.S)
            {
                direction = GridDown;
                return true;
            }

            if (key == KeyCode.LeftArrow || key == KeyCode.A)
            {
                direction = GridLeft;
                return true;
            }

            if (key == KeyCode.RightArrow || key == KeyCode.D)
            {
                direction = GridRight;
                return true;
            }

            direction = Vector2Int.zero;
            return false;
        }

        private static bool TryReadPuzzleKey(out KeyCode key)
        {
            var keyboard = Keyboard.current;
            key = KeyCode.None;

            if (keyboard == null)
            {
                return false;
            }

            if (keyboard.upArrowKey.wasPressedThisFrame)
            {
                key = KeyCode.UpArrow;
                return true;
            }

            if (keyboard.wKey.wasPressedThisFrame)
            {
                key = KeyCode.W;
                return true;
            }

            if (keyboard.downArrowKey.wasPressedThisFrame)
            {
                key = KeyCode.DownArrow;
                return true;
            }

            if (keyboard.sKey.wasPressedThisFrame)
            {
                key = KeyCode.S;
                return true;
            }

            if (keyboard.leftArrowKey.wasPressedThisFrame)
            {
                key = KeyCode.LeftArrow;
                return true;
            }

            if (keyboard.aKey.wasPressedThisFrame)
            {
                key = KeyCode.A;
                return true;
            }

            if (keyboard.rightArrowKey.wasPressedThisFrame)
            {
                key = KeyCode.RightArrow;
                return true;
            }

            if (keyboard.dKey.wasPressedThisFrame)
            {
                key = KeyCode.D;
                return true;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                key = KeyCode.R;
                return true;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                key = KeyCode.Escape;
                return true;
            }

            return false;
        }

        private static void ConfigureEventSystem(GameObject eventObject)
        {
            foreach (var standaloneInput in eventObject.GetComponents<StandaloneInputModule>())
            {
                standaloneInput.enabled = false;
                Destroy(standaloneInput);
            }

            var inputModule = eventObject.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventObject.AddComponent<InputSystemUIInputModule>();
            }

            if (inputModule.actionsAsset == null)
            {
                inputModule.AssignDefaultActions();
            }

            inputModule.enabled = true;
        }

        [Serializable]
        private sealed class SaveData
        {
            public bool HasRun;
            public int MasterSeed;
            public int LevelIndex;
            public int CompletedLevels;
            public int CurrentStreak;
            public int BestStreak;
        }

        private enum PuzzleType
        {
            Sliding,
            Maze,
            Sokoban,
            Nonogram,
            LightsOut,
            Match3,
            Flow,
            Sudoku,
            Memory,
            WaterSort
        }

        private enum Difficulty
        {
            Easy,
            Normal,
            Hard,
            Expert,
            Master
        }

        private interface IPuzzle
        {
            string Title { get; }
            string Objective { get; }
            Difficulty Difficulty { get; }
            int Moves { get; }
            bool IsSolved { get; }
            bool IsFailed { get; }
            void Render(PuzzleCoreGame ui, Transform parent);
            bool HandleKey(KeyCode key);
            void Reset();
        }

        private abstract class PuzzleBase : IPuzzle
        {
            protected readonly int seed;
            protected readonly SeededRandom rng;

            protected PuzzleBase(Difficulty difficulty, int seed)
            {
                Difficulty = difficulty;
                this.seed = seed;
                rng = new SeededRandom(seed);
            }

            public abstract string Title { get; }
            public abstract string Objective { get; }
            public Difficulty Difficulty { get; private set; }
            public int Moves { get; protected set; }
            public virtual bool IsFailed { get { return false; } }
            public abstract bool IsSolved { get; }
            public abstract void Render(PuzzleCoreGame ui, Transform parent);
            public virtual bool HandleKey(KeyCode key) { return false; }
            public abstract void Reset();
        }

        private sealed class SeededRandom
        {
            private uint state;

            public SeededRandom(int seed)
            {
                state = (uint)seed;
                if (state == 0)
                {
                    state = 0x6d2b79f5u;
                }
            }

            public int Range(int minInclusive, int maxExclusive)
            {
                if (maxExclusive <= minInclusive)
                {
                    return minInclusive;
                }

                return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
            }

            public bool Chance(float probability)
            {
                return Value() < probability;
            }

            public float Value()
            {
                return (NextUInt() & 0xffffff) / (float)0x1000000;
            }

            public void Shuffle<T>(IList<T> values)
            {
                for (var i = values.Count - 1; i > 0; i--)
                {
                    var j = Range(0, i + 1);
                    var temp = values[i];
                    values[i] = values[j];
                    values[j] = temp;
                }
            }

            private uint NextUInt()
            {
                state = unchecked(state * 1664525u + 1013904223u);
                var x = state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                return x;
            }
        }

        private sealed class SlidingPuzzle : PuzzleBase
        {
            private readonly int size;
            private readonly int scrambleMoves;
            private int[] initial;
            private int[] cells;

            public SlidingPuzzle(Difficulty difficulty, int seed) : base(difficulty, seed)
            {
                size = DiffIndex(difficulty) < 2 ? 3 : 4;
                scrambleMoves = new[] { 12, 20, 34, 48, 64 }[DiffIndex(difficulty)];
                Generate();
            }

            public override string Title { get { return "Sliding Tiles"; } }
            public override string Objective { get { return "Slide numbered tiles into ascending order."; } }

            public override bool IsSolved
            {
                get
                {
                    for (var i = 0; i < cells.Length - 1; i++)
                    {
                        if (cells[i] != i + 1)
                        {
                            return false;
                        }
                    }

                    return cells[cells.Length - 1] == 0;
                }
            }

            public override void Render(PuzzleCoreGame ui, Transform parent)
            {
                var grid = ui.Grid(parent, "Sliding Grid", size, size, 92);
                for (var i = 0; i < cells.Length; i++)
                {
                    var index = i;
                    var value = cells[i];
                    ui.TileButton(grid, value == 0 ? "" : value.ToString(), () => TryMove(index), value == 0 ? TileDark : Tile, TextMain, 28);
                }
            }

            public override bool HandleKey(KeyCode key)
            {
                if (!DirectionFromKey(key, out var direction))
                {
                    return false;
                }

                var blank = Array.IndexOf(cells, 0);
                var blankPosition = new Vector2Int(blank % size, blank / size);
                var tilePosition = blankPosition - direction;
                if (tilePosition.x < 0 || tilePosition.x >= size || tilePosition.y < 0 || tilePosition.y >= size)
                {
                    return false;
                }

                TryMove(tilePosition.y * size + tilePosition.x);
                return true;
            }

            public override void Reset()
            {
                cells = (int[])initial.Clone();
                Moves = 0;
            }

            private void Generate()
            {
                cells = Enumerable.Range(1, size * size - 1).Concat(new[] { 0 }).ToArray();
                var previousBlank = -1;
                for (var i = 0; i < scrambleMoves; i++)
                {
                    var blank = Array.IndexOf(cells, 0);
                    var options = AdjacentIndexes(blank).Where(index => index != previousBlank).ToList();
                    if (options.Count == 0)
                    {
                        options = AdjacentIndexes(blank).ToList();
                    }

                    var next = options[rng.Range(0, options.Count)];
                    Swap(blank, next);
                    previousBlank = blank;
                }

                if (IsSolved)
                {
                    Swap(cells.Length - 1, cells.Length - 2);
                }

                initial = (int[])cells.Clone();
                Moves = 0;
            }

            private IEnumerable<int> AdjacentIndexes(int index)
            {
                var x = index % size;
                var y = index / size;
                if (x > 0) yield return index - 1;
                if (x < size - 1) yield return index + 1;
                if (y > 0) yield return index - size;
                if (y < size - 1) yield return index + size;
            }

            private void TryMove(int index)
            {
                var blank = Array.IndexOf(cells, 0);
                if (!AdjacentIndexes(blank).Contains(index))
                {
                    return;
                }

                Swap(blank, index);
                Moves++;
            }

            private void Swap(int a, int b)
            {
                var temp = cells[a];
                cells[a] = cells[b];
                cells[b] = temp;
            }
        }

        private sealed class LightsOutPuzzle : PuzzleBase
        {
            private readonly int size;
            private bool[,] initial;
            private bool[,] cells;

            public LightsOutPuzzle(Difficulty difficulty, int seed) : base(difficulty, seed)
            {
                size = new[] { 3, 4, 5, 5, 6 }[DiffIndex(difficulty)];
                Generate();
            }

            public override string Title { get { return "Lights Out"; } }
            public override string Objective { get { return "Turn every light off. Each press flips a cross."; } }

            public override bool IsSolved
            {
                get
                {
                    for (var y = 0; y < size; y++)
                    {
                        for (var x = 0; x < size; x++)
                        {
                            if (cells[x, y])
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }
            }

            public override void Render(PuzzleCoreGame ui, Transform parent)
            {
                var grid = ui.Grid(parent, "Lights Grid", size, size, 78);
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var cx = x;
                        var cy = y;
                        ui.TileButton(grid, cells[x, y] ? "ON" : "", () => Press(cx, cy), cells[x, y] ? Warning : TileDark, cells[x, y] ? Color.black : TextMuted, 18);
                    }
                }
            }

            public override void Reset()
            {
                cells = Copy(initial);
                Moves = 0;
            }

            private void Generate()
            {
                cells = new bool[size, size];
                var presses = new HashSet<Vector2Int>();
                var targetPresses = new[] { 4, 7, 11, 15, 22 }[DiffIndex(Difficulty)];
                while (presses.Count < targetPresses)
                {
                    presses.Add(new Vector2Int(rng.Range(0, size), rng.Range(0, size)));
                }

                foreach (var press in presses)
                {
                    ToggleCross(press.x, press.y);
                }

                initial = Copy(cells);
                Moves = 0;
            }

            private void Press(int x, int y)
            {
                ToggleCross(x, y);
                Moves++;
            }

            private void ToggleCross(int x, int y)
            {
                Toggle(x, y);
                Toggle(x + 1, y);
                Toggle(x - 1, y);
                Toggle(x, y + 1);
                Toggle(x, y - 1);
            }

            private void Toggle(int x, int y)
            {
                if (x < 0 || x >= size || y < 0 || y >= size)
                {
                    return;
                }

                cells[x, y] = !cells[x, y];
            }

            private bool[,] Copy(bool[,] source)
            {
                var copy = new bool[size, size];
                Array.Copy(source, copy, source.Length);
                return copy;
            }
        }

        private sealed class MazePuzzle : PuzzleBase
        {
            private readonly int size;
            private readonly bool[,,] walls;
            private Vector2Int player;
            private Vector2Int initialPlayer;

            public MazePuzzle(Difficulty difficulty, int seed) : base(difficulty, seed)
            {
                size = new[] { 5, 6, 8, 10, 12 }[DiffIndex(difficulty)];
                walls = new bool[size, size, 4];
                Generate();
            }

            public override string Title { get { return "Maze"; } }
            public override string Objective { get { return "Reach the exit. Walls are enforced by movement."; } }
            public override bool IsSolved { get { return player.x == size - 1 && player.y == size - 1; } }

            public override void Render(PuzzleCoreGame ui, Transform parent)
            {
                var grid = ui.Grid(parent, "Maze Grid", size, size, Mathf.Clamp(520f / size, 40f, 80f), 4);
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var position = new Vector2Int(x, y);
                        var isPlayer = position == player;
                        var isExit = x == size - 1 && y == size - 1;
                        var label = isPlayer ? "P" : isExit ? "X" : "";
                        var color = isPlayer ? Success : isExit ? Warning : Tile;
                        ui.TileButton(grid, label, () => TryStepToward(position), color, isPlayer ? Color.black : TextMain, 20);
                    }
                }

                var controls = ui.Row(parent, "Maze Controls", 8, TextAnchor.MiddleCenter);
                AddLayout(controls.GetComponent<RectTransform>(), -1, 60);
                ui.Button(controls.transform, "Up", () => ui.PuzzleAction(() => Move(GridUp)), Tile, TextMain, 18, 48, 110);
                ui.Button(controls.transform, "Left", () => ui.PuzzleAction(() => Move(GridLeft)), Tile, TextMain, 18, 48, 110);
                ui.Button(controls.transform, "Down", () => ui.PuzzleAction(() => Move(GridDown)), Tile, TextMain, 18, 48, 110);
                ui.Button(controls.transform, "Right", () => ui.PuzzleAction(() => Move(GridRight)), Tile, TextMain, 18, 48, 110);
            }

            public override bool HandleKey(KeyCode key)
            {
                if (!DirectionFromKey(key, out var direction))
                {
                    return false;
                }

                return Move(direction);
            }

            public override void Reset()
            {
                player = initialPlayer;
                Moves = 0;
            }

            private void Generate()
            {
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        for (var d = 0; d < 4; d++)
                        {
                            walls[x, y, d] = true;
                        }
                    }
                }

                var visited = new bool[size, size];
                Carve(0, 0, visited);
                player = Vector2Int.zero;
                initialPlayer = player;
                Moves = 0;
            }

            private void Carve(int x, int y, bool[,] visited)
            {
                visited[x, y] = true;
                var directions = new List<Vector2Int> { GridUp, GridRight, GridDown, GridLeft };
                rng.Shuffle(directions);

                foreach (var direction in directions)
                {
                    var nx = x + direction.x;
                    var ny = y + direction.y;
                    if (nx < 0 || nx >= size || ny < 0 || ny >= size || visited[nx, ny])
                    {
                        continue;
                    }

                    OpenWall(new Vector2Int(x, y), direction);
                    Carve(nx, ny, visited);
                }
            }

            private bool Move(Vector2Int direction)
            {
                if (IsBlocked(player, direction))
                {
                    return false;
                }

                var next = player + direction;
                if (next.x < 0 || next.x >= size || next.y < 0 || next.y >= size)
                {
                    return false;
                }

                player = next;
                Moves++;
                return true;
            }

            private void TryStepToward(Vector2Int position)
            {
                var delta = position - player;
                if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
                {
                    return;
                }

                Move(delta);
            }

            private void OpenWall(Vector2Int cell, Vector2Int direction)
            {
                walls[cell.x, cell.y, DirectionIndex(direction)] = false;
                var other = cell + direction;
                walls[other.x, other.y, DirectionIndex(-direction)] = false;
            }

            private bool IsBlocked(Vector2Int cell, Vector2Int direction)
            {
                return walls[cell.x, cell.y, DirectionIndex(direction)];
            }

            private int DirectionIndex(Vector2Int direction)
            {
                if (direction == GridUp) return 0;
                if (direction == GridRight) return 1;
                if (direction == GridDown) return 2;
                return 3;
            }
        }

        private sealed class MemoryPuzzle : PuzzleBase
        {
            private readonly int pairCount;
            private readonly int columns;
            private int[] cards;
            private bool[] matched;
            private bool[] revealed;
            private int first = -1;
            private int second = -1;

            public MemoryPuzzle(Difficulty difficulty, int seed) : base(difficulty, seed)
            {
                pairCount = new[] { 4, 6, 8, 10, 12 }[DiffIndex(difficulty)];
                columns = pairCount <= 6 ? 4 : 6;
                Generate();
            }

            public override string Title { get { return "Memory"; } }
            public override string Objective { get { return "Find every matching pair."; } }
            public override bool IsSolved { get { return matched.All(value => value); } }

            public override void Render(PuzzleCoreGame ui, Transform parent)
            {
                var rows = Mathf.CeilToInt(cards.Length / (float)columns);
                var grid = ui.Grid(parent, "Memory Grid", columns, rows, 78);
                for (var i = 0; i < cards.Length; i++)
                {
                    var index = i;
                    var open = revealed[i] || matched[i];
                    var label = open ? ColorName(cards[i]) : "?";
                    var color = matched[i] ? Success : open ? ColorFor(cards[i]) : TileDark;
                    ui.TileButton(grid, label, () => Flip(index), color, open ? Color.black : TextMain, 24);
                }
            }

            public override void Reset()
            {
                matched = new bool[cards.Length];
                revealed = new bool[cards.Length];
                first = -1;
                second = -1;
                Moves = 0;
            }

            private void Generate()
            {
                var values = new List<int>();
                for (var i = 0; i < pairCount; i++)
                {
                    values.Add(i);
                    values.Add(i);
                }

                rng.Shuffle(values);
                cards = values.ToArray();
                Reset();
            }

            private void Flip(int index)
            {
                if (matched[index] || revealed[index])
                {
                    return;
                }

                if (first >= 0 && second >= 0 && cards[first] != cards[second])
                {
                    revealed[first] = false;
                    revealed[second] = false;
                    first = -1;
                    second = -1;
                }

                revealed[index] = true;
                if (first < 0)
                {
                    first = index;
                    return;
                }

                second = index;
                Moves++;
                if (cards[first] == cards[second])
                {
                    matched[first] = true;
                    matched[second] = true;
                    first = -1;
                    second = -1;
                }
            }
        }

        private sealed class WaterSortPuzzle : PuzzleBase
        {
            private const int Capacity = 4;

            private readonly int colorCount;
            private readonly int tubeCount;
            private List<int>[] initial;
            private List<int>[] tubes;
            private int selected = -1;

            public WaterSortPuzzle(Difficulty difficulty, int seed) : base(difficulty, seed)
            {
                colorCount = new[] { 3, 4, 5, 6, 7 }[DiffIndex(difficulty)];
                tubeCount = colorCount + 2;
                Generate();
            }

            public override string Title { get { return "Water Sort"; } }
            public override string Objective { get { return "Pour until every tube is empty or one solid color."; } }

            public override bool IsSolved
            {
                get
                {
                    foreach (var tube in tubes)
                    {
                        if (tube.Count == 0)
                        {
                            continue;
                        }

                        if (tube.Count != Capacity)
                        {
                            return false;
                        }

                        if (tube.Any(value => value != tube[0]))
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            public override void Render(PuzzleCoreGame ui, Transform parent)
            {
                var row = ui.Row(parent, "Tubes", 10, TextAnchor.MiddleCenter);
                AddLayout(row.GetComponent<RectTransform>(), -1, 250);
                for (var i = 0; i < tubes.Length; i++)
                {
                    var index = i;
                    var stack = ui.Stack(row.transform, "Tube " + i, 4, TextAnchor.LowerCenter);
                    stack.padding = new RectOffset(4, 4, 8, 8);
                    stack.GetComponent<Image>().color = selected == i ? Warning : TileDark;
                    AddLayout(stack.GetComponent<RectTransform>(), 82, 230);
                    for (var slot = Capacity - 1; slot >= 0; slot--)
                    {
                        var filled = slot < tubes[i].Count;
                        var color = filled ? ColorFor(tubes[i][slot]) : Rgb(34, 39, 50);
                        ui.Button(stack.transform, filled ? ColorName(tubes[i][slot]) : "", () => ui.PuzzleAction(() => SelectTube(index)), color, filled ? Color.black : TextMuted, 18, 44, 70);
                    }
                }
            }

            public override void Reset()
            {
                tubes = Clone(initial);
                selected = -1;
                Moves = 0;
            }

            private void Generate()
            {
                tubes = new List<int>[tubeCount];
                for (var i = 0; i < tubeCount; i++)
                {
                    tubes[i] = new List<int>();
                }

                var colors = Enumerable.Range(0, colorCount).ToList();
                rng.Shuffle(colors);
                var cursor = 0;
                while (cursor < colors.Count)
                {
                    if (cursor == colors.Count - 1)
                    {
                        for (var i = 0; i < Capacity; i++)
                        {
                            tubes[cursor].Add(colors[cursor]);
                        }

                        cursor++;
                        continue;
                    }

                    var a = colors[cursor];
                    var b = colors[cursor + 1];
                    tubes[cursor].Add(a);
                    tubes[cursor].Add(a);
                    tubes[cursor].Add(b);
                    tubes[cursor].Add(b);
                    tubes[cursor + 1].Add(b);
                    tubes[cursor + 1].Add(b);
                    tubes[cursor + 1].Add(a);
                    tubes[cursor + 1].Add(a);
                    cursor += 2;
                }

                rng.Shuffle(tubes);
                initial = Clone(tubes);
                Moves = 0;
            }

            private void SelectTube(int index)
            {
                if (selected < 0)
                {
                    if (tubes[index].Count > 0)
                    {
                        selected = index;
                    }

                    return;
                }

                if (selected == index)
                {
                    selected = -1;
                    return;
                }

                if (Pour(selected, index))
                {
                    Moves++;
                }

                selected = -1;
            }

            private bool Pour(int from, int to)
            {
                if (tubes[from].Count == 0 || tubes[to].Count >= Capacity)
                {
                    return false;
                }

                var color = tubes[from][tubes[from].Count - 1];
                if (tubes[to].Count > 0 && tubes[to][tubes[to].Count - 1] != color)
                {
                    return false;
                }

                var moved = false;
                while (tubes[from].Count > 0 && tubes[from][tubes[from].Count - 1] == color && tubes[to].Count < Capacity)
                {
                    tubes[to].Add(color);
                    tubes[from].RemoveAt(tubes[from].Count - 1);
                    moved = true;
                }

                return moved;
            }

            private List<int>[] Clone(List<int>[] source)
            {
                var clone = new List<int>[source.Length];
                for (var i = 0; i < source.Length; i++)
                {
                    clone[i] = new List<int>(source[i]);
                }

                return clone;
            }
        }

        private sealed class SudokuPuzzle : PuzzleBase
        {
            private readonly int size;
            private readonly int boxWidth;
            private readonly int boxHeight;
            private int[,] solution;
            private int[,] grid;
            private bool[,] given;

            public SudokuPuzzle(Difficulty difficulty, int seed) : base(difficulty, seed)
            {
                if (DiffIndex(difficulty) < 2)
                {
                    size = 4;
                    boxWidth = 2;
                    boxHeight = 2;
                }
                else if (DiffIndex(difficulty) < 4)
                {
                    size = 6;
                    boxWidth = 3;
                    boxHeight = 2;
                }
                else
                {
                    size = 9;
                    boxWidth = 3;
                    boxHeight = 3;
                }

                Generate();
            }

            public override string Title { get { return "Sudoku"; } }
            public override string Objective { get { return "Fill the grid so rows, columns and boxes contain every number."; } }

            public override bool IsSolved
            {
                get
                {
                    for (var y = 0; y < size; y++)
                    {
                        for (var x = 0; x < size; x++)
                        {
                            if (grid[x, y] != solution[x, y])
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }
            }

            public override void Render(PuzzleCoreGame ui, Transform parent)
            {
                var gridPanel = ui.Grid(parent, "Sudoku Grid", size, size, Mathf.Clamp(520f / size, 44f, 82f), 4);
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var cx = x;
                        var cy = y;
                        var value = grid[x, y];
                        var text = value == 0 ? "" : value.ToString();
                        var color = given[x, y] ? TileDark : Tile;
                        ui.TileButton(gridPanel, text, () => Cycle(cx, cy), color, given[x, y] ? TextMuted : TextMain, 22);
                    }
                }
            }

            public override void Reset()
            {
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        if (!given[x, y])
                        {
                            grid[x, y] = 0;
                        }
                    }
                }

                Moves = 0;
            }

            private void Generate()
            {
                solution = new int[size, size];
                var numbers = Enumerable.Range(1, size).ToList();
                rng.Shuffle(numbers);

                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var pattern = (boxWidth * (y % boxHeight) + y / boxHeight + x) % size;
                        solution[x, y] = numbers[pattern];
                    }
                }

                var rowOrder = ShuffledBands(size, boxHeight);
                var columnOrder = ShuffledBands(size, boxWidth);
                var remapped = new int[size, size];
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        remapped[x, y] = solution[columnOrder[x], rowOrder[y]];
                    }
                }

                solution = remapped;
                grid = (int[,])solution.Clone();
                given = new bool[size, size];
                var holeRatio = new[] { 0.38f, 0.48f, 0.56f, 0.62f, 0.68f }[DiffIndex(Difficulty)];
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        if (rng.Chance(holeRatio))
                        {
                            grid[x, y] = 0;
                            given[x, y] = false;
                        }
                        else
                        {
                            given[x, y] = true;
                        }
                    }
                }

                Moves = 0;
            }

            private List<int> ShuffledBands(int count, int bandSize)
            {
                var bands = Enumerable.Range(0, count / bandSize).ToList();
                rng.Shuffle(bands);
                var result = new List<int>();
                foreach (var band in bands)
                {
                    var within = Enumerable.Range(0, bandSize).ToList();
                    rng.Shuffle(within);
                    foreach (var row in within)
                    {
                        result.Add(band * bandSize + row);
                    }
                }

                return result;
            }

            private void Cycle(int x, int y)
            {
                if (given[x, y])
                {
                    return;
                }

                grid[x, y] = (grid[x, y] + 1) % (size + 1);
                Moves++;
            }
        }

        private sealed class NonogramPuzzle : PuzzleBase
        {
            private readonly int size;
            private bool[,] solution;
            private int[,] player;

            public NonogramPuzzle(Difficulty difficulty, int seed) : base(difficulty, seed)
            {
                size = new[] { 5, 5, 6, 7, 8 }[DiffIndex(difficulty)];
                Generate();
            }

            public override string Title { get { return "Nonogram"; } }
            public override string Objective { get { return "Use row and column clues to mark every cell."; } }

            public override bool IsSolved
            {
                get
                {
                    for (var y = 0; y < size; y++)
                    {
                        for (var x = 0; x < size; x++)
                        {
                            if (solution[x, y] && player[x, y] != 1)
                            {
                                return false;
                            }

                            if (!solution[x, y] && player[x, y] != 2)
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }
            }

            public override void Render(PuzzleCoreGame ui, Transform parent)
            {
                var grid = ui.Grid(parent, "Nonogram Grid", size + 1, size + 1, Mathf.Clamp(520f / (size + 1), 42f, 70f), 4);
                ui.TileButton(grid, "", null, TileDark, TextMuted, 14);
                for (var x = 0; x < size; x++)
                {
                    ui.TileButton(grid, Clue(ColumnValues(x)), null, TileDark, TextMuted, 13);
                }

                for (var y = 0; y < size; y++)
                {
                    ui.TileButton(grid, Clue(RowValues(y)), null, TileDark, TextMuted, 13);
                    for (var x = 0; x < size; x++)
                    {
                        var cx = x;
                        var cy = y;
                        var state = player[x, y];
                        var label = state == 1 ? "#" : state == 2 ? "x" : "";
                        var color = state == 1 ? TextMain : state == 2 ? TileDark : Tile;
                        ui.TileButton(grid, label, () => Cycle(cx, cy), color, state == 1 ? Color.black : TextMuted, 20);
                    }
                }
            }

            public override void Reset()
            {
                player = new int[size, size];
                Moves = 0;
            }

            private void Generate()
            {
                solution = new bool[size, size];
                for (var y = 0; y < size; y++)
                {
                    var filledInRow = 0;
                    for (var x = 0; x < size; x++)
                    {
                        var value = rng.Chance(0.46f);
                        solution[x, y] = value;
                        if (value)
                        {
                            filledInRow++;
                        }
                    }

                    if (filledInRow == 0)
                    {
                        solution[rng.Range(0, size), y] = true;
                    }
                }

                for (var x = 0; x < size; x++)
                {
                    var any = false;
                    for (var y = 0; y < size; y++)
                    {
                        any |= solution[x, y];
                    }

                    if (!any)
                    {
                        solution[x, rng.Range(0, size)] = true;
                    }
                }

                Reset();
            }

            private void Cycle(int x, int y)
            {
                player[x, y] = (player[x, y] + 1) % 3;
                Moves++;
            }

            private IEnumerable<bool> RowValues(int y)
            {
                for (var x = 0; x < size; x++)
                {
                    yield return solution[x, y];
                }
            }

            private IEnumerable<bool> ColumnValues(int x)
            {
                for (var y = 0; y < size; y++)
                {
                    yield return solution[x, y];
                }
            }

            private string Clue(IEnumerable<bool> values)
            {
                var result = new List<int>();
                var run = 0;
                foreach (var value in values)
                {
                    if (value)
                    {
                        run++;
                    }
                    else if (run > 0)
                    {
                        result.Add(run);
                        run = 0;
                    }
                }

                if (run > 0)
                {
                    result.Add(run);
                }

                return result.Count == 0 ? "0" : string.Join(" ", result.Select(v => v.ToString()).ToArray());
            }
        }

        private sealed class FlowPuzzle : PuzzleBase
        {
            private readonly int size;
            private readonly int colorCount;
            private int[,] solution;
            private int[,] player;
            private bool[,] endpoint;
            private int selectedColor;

            public FlowPuzzle(Difficulty difficulty, int seed) : base(difficulty, seed)
            {
                size = new[] { 5, 5, 6, 6, 7 }[DiffIndex(difficulty)];
                colorCount = new[] { 3, 4, 4, 5, 6 }[DiffIndex(difficulty)];
                Generate();
            }

            public override string Title { get { return "Flow"; } }
            public override string Objective { get { return "Connect matching endpoints and cover the whole grid."; } }

            public override bool IsSolved
            {
                get
                {
                    for (var y = 0; y < size; y++)
                    {
                        for (var x = 0; x < size; x++)
                        {
                            if (player[x, y] != solution[x, y])
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }
            }

            public override void Render(PuzzleCoreGame ui, Transform parent)
            {
                var palette = ui.Row(parent, "Flow Palette", 8, TextAnchor.MiddleCenter);
                AddLayout(palette.GetComponent<RectTransform>(), -1, 56);
                for (var i = 0; i < colorCount; i++)
                {
                    var color = i;
                    ui.Button(palette.transform, ColorName(i), () => ui.PuzzleAction(() => selectedColor = color), selectedColor == i ? Warning : ColorFor(i), Color.black, 18, 48, 74);
                }

                var grid = ui.Grid(parent, "Flow Grid", size, size, Mathf.Clamp(520f / size, 48f, 82f), 5);
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var cx = x;
                        var cy = y;
                        var value = player[x, y];
                        var isEndpoint = endpoint[x, y];
                        var label = value >= 0 ? (isEndpoint ? ColorName(value) : "") : "";
                        var color = value >= 0 ? ColorFor(value) : TileDark;
                        ui.TileButton(grid, label, () => Paint(cx, cy), color, value >= 0 ? Color.black : TextMuted, 22);
                    }
                }
            }

            public override void Reset()
            {
                player = new int[size, size];
                endpoint = endpoint ?? new bool[size, size];
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        player[x, y] = endpoint[x, y] ? solution[x, y] : -1;
                    }
                }

                selectedColor = 0;
                Moves = 0;
            }

            private void Generate()
            {
                solution = new int[size, size];
                endpoint = new bool[size, size];
                var path = new List<Vector2Int>();
                for (var y = 0; y < size; y++)
                {
                    if (y % 2 == 0)
                    {
                        for (var x = 0; x < size; x++)
                        {
                            path.Add(new Vector2Int(x, y));
                        }
                    }
                    else
                    {
                        for (var x = size - 1; x >= 0; x--)
                        {
                            path.Add(new Vector2Int(x, y));
                        }
                    }
                }

                var cuts = new List<int> { 0, path.Count };
                while (cuts.Count < colorCount + 1)
                {
                    var cut = rng.Range(2, path.Count - 2);
                    if (!cuts.Contains(cut))
                    {
                        cuts.Add(cut);
                    }
                }

                cuts.Sort();
                for (var color = 0; color < colorCount; color++)
                {
                    for (var i = cuts[color]; i < cuts[color + 1]; i++)
                    {
                        var p = path[i];
                        solution[p.x, p.y] = color;
                    }

                    var start = path[cuts[color]];
                    var end = path[cuts[color + 1] - 1];
                    endpoint[start.x, start.y] = true;
                    endpoint[end.x, end.y] = true;
                }

                Reset();
            }

            private void Paint(int x, int y)
            {
                if (endpoint[x, y])
                {
                    selectedColor = player[x, y];
                    return;
                }

                player[x, y] = player[x, y] == selectedColor ? -1 : selectedColor;
                Moves++;
            }
        }

        private sealed class Match3Puzzle : PuzzleBase
        {
            private readonly int width;
            private readonly int height;
            private readonly int colorCount;
            private int[,] board;
            private int[,] initial;
            private Vector2Int selected = new Vector2Int(-1, -1);

            public Match3Puzzle(Difficulty difficulty, int seed) : base(difficulty, seed)
            {
                width = new[] { 6, 6, 7, 8, 8 }[DiffIndex(difficulty)];
                height = width;
                colorCount = new[] { 5, 5, 6, 6, 7 }[DiffIndex(difficulty)];
                TargetScore = new[] { 240, 360, 540, 760, 1000 }[DiffIndex(difficulty)];
                MovesLeft = new[] { 18, 20, 24, 28, 32 }[DiffIndex(difficulty)];
                Generate();
            }

            public int Score { get; private set; }
            public int TargetScore { get; private set; }
            public int MovesLeft { get; private set; }

            public override string Title { get { return "Match-3"; } }
            public override string Objective { get { return "Swap adjacent gems to reach the score target."; } }
            public override bool IsSolved { get { return Score >= TargetScore; } }
            public override bool IsFailed { get { return MovesLeft <= 0 && !IsSolved; } }

            public override void Render(PuzzleCoreGame ui, Transform parent)
            {
                var grid = ui.Grid(parent, "Match3 Grid", width, height, Mathf.Clamp(520f / width, 46f, 76f), 5);
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var cx = x;
                        var cy = y;
                        var isSelected = selected.x == x && selected.y == y;
                        ui.TileButton(grid, ColorName(board[x, y]), () => Select(cx, cy), isSelected ? Warning : ColorFor(board[x, y]), Color.black, 20);
                    }
                }
            }

            public override void Reset()
            {
                board = (int[,])initial.Clone();
                selected = new Vector2Int(-1, -1);
                Score = 0;
                Moves = 0;
                MovesLeft = new[] { 18, 20, 24, 28, 32 }[DiffIndex(Difficulty)];
            }

            private void Generate()
            {
                board = new int[width, height];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var value = rng.Range(0, colorCount);
                        var guard = 0;
                        while (WouldMatchAt(x, y, value) && guard++ < 20)
                        {
                            value = rng.Range(0, colorCount);
                        }

                        board[x, y] = value;
                    }
                }

                initial = (int[,])board.Clone();
                Reset();
            }

            private bool WouldMatchAt(int x, int y, int value)
            {
                return x >= 2 && board[x - 1, y] == value && board[x - 2, y] == value
                    || y >= 2 && board[x, y - 1] == value && board[x, y - 2] == value;
            }

            private void Select(int x, int y)
            {
                if (IsFailed)
                {
                    return;
                }

                var current = new Vector2Int(x, y);
                if (selected.x < 0)
                {
                    selected = current;
                    return;
                }

                if (selected == current)
                {
                    selected = new Vector2Int(-1, -1);
                    return;
                }

                if (Mathf.Abs(selected.x - x) + Mathf.Abs(selected.y - y) != 1)
                {
                    selected = current;
                    return;
                }

                Swap(selected, current);
                var cleared = ResolveMatches();
                if (cleared == 0)
                {
                    Swap(selected, current);
                }
                else
                {
                    Score += cleared * 10;
                    Moves++;
                    MovesLeft--;
                }

                selected = new Vector2Int(-1, -1);
            }

            private int ResolveMatches()
            {
                var total = 0;
                while (true)
                {
                    var clear = new bool[width, height];
                    var any = false;
                    for (var y = 0; y < height; y++)
                    {
                        var runStart = 0;
                        for (var x = 1; x <= width; x++)
                        {
                            if (x < width && board[x, y] == board[runStart, y])
                            {
                                continue;
                            }

                            if (x - runStart >= 3)
                            {
                                any = true;
                                for (var cx = runStart; cx < x; cx++)
                                {
                                    clear[cx, y] = true;
                                }
                            }

                            runStart = x;
                        }
                    }

                    for (var x = 0; x < width; x++)
                    {
                        var runStart = 0;
                        for (var y = 1; y <= height; y++)
                        {
                            if (y < height && board[x, y] == board[x, runStart])
                            {
                                continue;
                            }

                            if (y - runStart >= 3)
                            {
                                any = true;
                                for (var cy = runStart; cy < y; cy++)
                                {
                                    clear[x, cy] = true;
                                }
                            }

                            runStart = y;
                        }
                    }

                    if (!any)
                    {
                        return total;
                    }

                    for (var x = 0; x < width; x++)
                    {
                        var write = height - 1;
                        for (var y = height - 1; y >= 0; y--)
                        {
                            if (clear[x, y])
                            {
                                total++;
                                continue;
                            }

                            board[x, write] = board[x, y];
                            write--;
                        }

                        while (write >= 0)
                        {
                            board[x, write] = rng.Range(0, colorCount);
                            write--;
                        }
                    }
                }
            }

            private void Swap(Vector2Int a, Vector2Int b)
            {
                var temp = board[a.x, a.y];
                board[a.x, a.y] = board[b.x, b.y];
                board[b.x, b.y] = temp;
            }
        }

        private sealed class SokobanPuzzle : PuzzleBase
        {
            private readonly int width;
            private readonly int height;
            private readonly bool[,] walls;
            private readonly HashSet<Vector2Int> goals = new HashSet<Vector2Int>();
            private readonly List<Vector2Int> initialBoxes = new List<Vector2Int>();
            private List<Vector2Int> boxes = new List<Vector2Int>();
            private Vector2Int player;
            private Vector2Int initialPlayer;

            public SokobanPuzzle(Difficulty difficulty, int seed) : base(difficulty, seed)
            {
                width = new[] { 7, 8, 9, 10, 11 }[DiffIndex(difficulty)];
                height = new[] { 6, 7, 8, 8, 9 }[DiffIndex(difficulty)];
                walls = new bool[width, height];
                Generate();
            }

            public override string Title { get { return "Sokoban"; } }
            public override string Objective { get { return "Push every crate onto a goal."; } }
            public override bool IsSolved { get { return boxes.All(box => goals.Contains(box)); } }

            public override void Render(PuzzleCoreGame ui, Transform parent)
            {
                var grid = ui.Grid(parent, "Sokoban Grid", width, height, Mathf.Clamp(560f / width, 42f, 72f), 4);
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var position = new Vector2Int(x, y);
                        var hasBox = boxes.Contains(position);
                        var hasGoal = goals.Contains(position);
                        var label = walls[x, y] ? "#" : player == position ? "P" : hasBox && hasGoal ? "*" : hasBox ? "B" : hasGoal ? "G" : "";
                        var color = walls[x, y] ? TileDark : player == position ? Success : hasBox ? Warning : hasGoal ? Rgb(64, 97, 82) : Tile;
                        ui.TileButton(grid, label, () => TryStepToward(position), color, walls[x, y] ? TextMuted : Color.black, 18);
                    }
                }

                var controls = ui.Row(parent, "Sokoban Controls", 8, TextAnchor.MiddleCenter);
                AddLayout(controls.GetComponent<RectTransform>(), -1, 60);
                ui.Button(controls.transform, "Up", () => ui.PuzzleAction(() => Move(GridUp)), Tile, TextMain, 18, 48, 110);
                ui.Button(controls.transform, "Left", () => ui.PuzzleAction(() => Move(GridLeft)), Tile, TextMain, 18, 48, 110);
                ui.Button(controls.transform, "Down", () => ui.PuzzleAction(() => Move(GridDown)), Tile, TextMain, 18, 48, 110);
                ui.Button(controls.transform, "Right", () => ui.PuzzleAction(() => Move(GridRight)), Tile, TextMain, 18, 48, 110);
            }

            public override bool HandleKey(KeyCode key)
            {
                if (!DirectionFromKey(key, out var direction))
                {
                    return false;
                }

                return Move(direction);
            }

            public override void Reset()
            {
                boxes = new List<Vector2Int>(initialBoxes);
                player = initialPlayer;
                Moves = 0;
            }

            private void Generate()
            {
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        walls[x, y] = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    }
                }

                var boxCount = new[] { 1, 2, 3, 4, 5 }[DiffIndex(Difficulty)];
                var rows = Enumerable.Range(1, height - 2).ToList();
                rng.Shuffle(rows);
                rows = rows.Take(boxCount).OrderBy(v => v).ToList();
                var startX = Math.Max(2, width / 2 - 1);
                foreach (var row in rows)
                {
                    var goal = new Vector2Int(width - 2, row);
                    var box = new Vector2Int(goal.x - 1, row);
                    goals.Add(goal);
                    initialBoxes.Add(box);
                }

                player = new Vector2Int(1, rows[0]);
                initialPlayer = player;
                for (var y = 1; y < height - 1; y++)
                {
                    if (rng.Chance(0.18f) && y != player.y && !rows.Contains(y))
                    {
                        walls[startX, y] = true;
                    }
                }

                Reset();
            }

            private bool Move(Vector2Int direction)
            {
                var target = player + direction;
                if (Blocked(target))
                {
                    return false;
                }

                var boxIndex = boxes.IndexOf(target);
                if (boxIndex >= 0)
                {
                    var pushed = target + direction;
                    if (Blocked(pushed) || boxes.Contains(pushed))
                    {
                        return false;
                    }

                    boxes[boxIndex] = pushed;
                }

                player = target;
                Moves++;
                return true;
            }

            private void TryStepToward(Vector2Int position)
            {
                var delta = position - player;
                if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
                {
                    return;
                }

                Move(delta);
            }

            private bool Blocked(Vector2Int position)
            {
                return position.x < 0 || position.x >= width || position.y < 0 || position.y >= height || walls[position.x, position.y];
            }
        }
    }
}
