using ChessChallenge.AI;
using ChessChallenge.Chess;
using ChessChallenge.Evaluation;
using Microsoft.Win32; // Required for OpenFileDialog
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace SystemHelper
{
    // Simple class to hold our settings
    public class AppSettings
    {
        public double Opacity { get; set; } = 0.5;
        public double Size { get; set; } = 200;
        public bool IsApiEnabled { get; set; } = true;
        public bool IsChatbotEnabled { get; set; } = true;
        public int BotDepth { get; set; } = 6;

        // New Engine Settings
        public bool UseExternalEngine { get; set; } = false;
        public string EnginePath { get; set; } = "";
        public string EngineCommand { get; set; } = "go nodes 1"; // Default to "LLM" mode
    }

    public partial class MainWindow : Window
    {
        private MyBot bot;
        private GroqAIHelper? groqHelper;
        private OpeningBook openingBook;
        private bool isFlipped = false;
        private string currentFen = FenUtility.StartPositionFEN;
        private string currentBestMove = "";
        private string lastPgn = "";
        private bool isLocked = false;

        // External Engine Process
        private Process? _engineProcess;
        private bool _isEngineRunning = false;

        // Analysis state
        private double evalBeforeOpponentMove = 0.0;
        private string lastFenBeforeOpponent = "";

        // Settings fields (loaded from file)
        private bool isApiConnectionEnabled = true;
        private bool isChatbotEnabled = true;
        private int currentBotDepth = 6;
        private double savedOpacity = 0.5;
        private double savedSize = 200;

        private bool useExternalEngine = false;
        private string externalEnginePath = "";
        private string externalEngineCommand = "go nodes 1";

        private const string SettingsFile = "settings.json";

        private Dictionary<string, string> pieceUnicode = new Dictionary<string, string>
        {
            {"wK", "♔"}, {"wQ", "♕"}, {"wR", "♖"}, {"wB", "♗"}, {"wN", "♘"}, {"wP", "♙"},
            {"bK", "♚"}, {"bQ", "♛"}, {"bR", "♜"}, {"bB", "♝"}, {"bN", "♞"}, {"bP", "♟"}
        };

        private double screenLeft, screenTop, screenRight, screenBottom;

        // --- DLL IMPORTS ---
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        // Constants
        private const int HOTKEY_HIDE = 9000;
        private const int HOTKEY_LEFT = 9001;
        private const int HOTKEY_UP = 9002;
        private const int HOTKEY_RIGHT = 9003;
        private const int HOTKEY_DOWN = 9004;
        private const int HOTKEY_FLIP = 9005;
        private const int HOTKEY_LOCK_POS = 9006;
        private const int HOTKEY_DESTRUCT = 9007;
        private const int HOTKEY_OPTIONS = 9008;

        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_CTRL_SHIFT = MOD_CONTROL | MOD_SHIFT;

        private const uint VK_H = 0x48;
        private const uint VK_F = 0x46;
        private const uint VK_L = 0x4C;
        private const uint VK_X = 0x58;
        private const uint VK_O = 0x4F;
        private const uint VK_LEFT = 0x25;
        private const uint VK_UP = 0x26;
        private const uint VK_RIGHT = 0x27;
        private const uint VK_DOWN = 0x28;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;

        private IntPtr _hwnd;
        private bool _isClickThrough = true;
        private const double MOVE_STEP = 10.0;
        private Border[,] squareCache = new Border[8, 8];

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();

            this.Title = "";
            ShowInTaskbar = false;

            screenLeft = SystemParameters.WorkArea.Left;
            screenTop = SystemParameters.WorkArea.Top;
            screenRight = SystemParameters.WorkArea.Right;
            screenBottom = SystemParameters.WorkArea.Bottom;

            Width = savedSize;
            Height = savedSize;
            Top = 45;
            Left = screenRight - Width + 8;
            ClampPosition();

            Topmost = true;
            Opacity = savedOpacity;

            MouseLeftButtonDown += (s, e) => { if (!_isClickThrough) DragMove(); };
            KeyDown += MainWindow_KeyDown;

            Loaded += (s, e) =>
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                RegisterAllHotkeys();
                HwndSource.FromHwnd(_hwnd).AddHook(HwndHook);
                EnableClickThrough();

                // Initialize Bot & Engine in background
                Task.Run(() =>
                {
                    bot = new MyBot();
                    bot.SetMaxDepth(currentBotDepth);
                    openingBook = new OpeningBook(); // <--- ADD THIS
                    try { groqHelper = new GroqAIHelper(); } catch { }

                    if (useExternalEngine) InitializeExternalEngine();

                    Debug.WriteLine($"✅ Startup complete. Engine Enabled: {useExternalEngine}");
                });
            };


            Closing += (s, e) =>
            {
                StopExternalEngine();
                SaveSettings();
                UnregisterAllHotkeys();
            };

            InitializeChessBoard();
            UpdateChessBoard(currentFen);

            Task.Run(RunListener);
        }

        // --- EXTERNAL ENGINE LOGIC ---
        private void InitializeExternalEngine()
        {
            StopExternalEngine(); // Clean up if running

            if (string.IsNullOrWhiteSpace(externalEnginePath) || !File.Exists(externalEnginePath))
            {
                Debug.WriteLine("❌ Engine path invalid or empty.");
                return;
            }

            try
            {
                _engineProcess = new Process();
                _engineProcess.StartInfo.FileName = externalEnginePath;
                _engineProcess.StartInfo.UseShellExecute = false;
                _engineProcess.StartInfo.RedirectStandardInput = true;
                _engineProcess.StartInfo.RedirectStandardOutput = true;
                _engineProcess.StartInfo.CreateNoWindow = true;
                _engineProcess.Start();

                _engineProcess.StandardInput.WriteLine("uci");
                _engineProcess.StandardInput.WriteLine("isready");
                _isEngineRunning = true;
                Debug.WriteLine($"✅ External Engine Started: {Path.GetFileName(externalEnginePath)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Failed to start engine: {ex.Message}");
                _isEngineRunning = false;
            }
        }

        private void StopExternalEngine()
        {
            if (_engineProcess != null && !_engineProcess.HasExited)
            {
                try { _engineProcess.Kill(); } catch { }
                _engineProcess.Dispose();
            }
            _isEngineRunning = false;
        }

        private string GetBestMoveFromExternalEngine(string fen)
        {
            if (!_isEngineRunning || _engineProcess == null || _engineProcess.HasExited)
            {
                InitializeExternalEngine(); // Try to restart
                if (!_isEngineRunning) return "";
            }

            try
            {
                // Clean buffer (optional, but safer)
                while (_engineProcess.StandardOutput.Peek() > -1) _engineProcess.StandardOutput.ReadLine();

                _engineProcess.StandardInput.WriteLine($"position fen {fen}");
                _engineProcess.StandardInput.WriteLine(externalEngineCommand);

                // Read until bestmove
                string? line;
                // Safety timeout could be added here, but simplified for now
                while ((line = _engineProcess.StandardOutput.ReadLine()) != null)
                {
                    if (line.StartsWith("bestmove"))
                    {
                        // Format: "bestmove e2e4 ponder ..."
                        var parts = line.Split(' ');
                        return parts.Length > 1 ? parts[1] : "";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Engine communication error: {ex.Message}");
            }
            return "";
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        savedOpacity = settings.Opacity;
                        savedSize = settings.Size;
                        isApiConnectionEnabled = settings.IsApiEnabled;
                        isChatbotEnabled = settings.IsChatbotEnabled;
                        currentBotDepth = Math.Clamp(settings.BotDepth, 4, 14);

                        // Load engine settings
                        useExternalEngine = settings.UseExternalEngine;
                        externalEnginePath = settings.EnginePath;
                        externalEngineCommand = settings.EngineCommand;
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Failed to load settings: {ex.Message}"); }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new AppSettings
                {
                    Opacity = this.Opacity,
                    Size = this.Width,
                    IsApiEnabled = isApiConnectionEnabled,
                    IsChatbotEnabled = isChatbotEnabled,
                    BotDepth = currentBotDepth,
                    UseExternalEngine = useExternalEngine,
                    EnginePath = externalEnginePath,
                    EngineCommand = externalEngineCommand
                };
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }

        // --- REST OF THE CODE ---

        private void RegisterAllHotkeys()
        {
            RegisterHotKey(_hwnd, HOTKEY_HIDE, MOD_CTRL_SHIFT, VK_H);
            RegisterHotKey(_hwnd, HOTKEY_FLIP, MOD_CTRL_SHIFT, VK_F);
            RegisterHotKey(_hwnd, HOTKEY_LOCK_POS, MOD_CTRL_SHIFT, VK_L);
            RegisterHotKey(_hwnd, HOTKEY_DESTRUCT, MOD_CTRL_SHIFT, VK_X);
            RegisterHotKey(_hwnd, HOTKEY_OPTIONS, MOD_CTRL_SHIFT, VK_O);
            RegisterArrowHotkeys();
        }

        private void UnregisterAllHotkeys()
        {
            UnregisterHotKey(_hwnd, HOTKEY_HIDE);
            UnregisterHotKey(_hwnd, HOTKEY_FLIP);
            UnregisterHotKey(_hwnd, HOTKEY_LOCK_POS);
            UnregisterHotKey(_hwnd, HOTKEY_DESTRUCT);
            UnregisterHotKey(_hwnd, HOTKEY_OPTIONS);
            UnregisterArrowHotkeys();
        }

        private void RegisterArrowHotkeys()
        {
            RegisterHotKey(_hwnd, HOTKEY_LEFT, 0, VK_LEFT);
            RegisterHotKey(_hwnd, HOTKEY_UP, 0, VK_UP);
            RegisterHotKey(_hwnd, HOTKEY_RIGHT, 0, VK_RIGHT);
            RegisterHotKey(_hwnd, HOTKEY_DOWN, 0, VK_DOWN);
        }

        private void UnregisterArrowHotkeys()
        {
            UnregisterHotKey(_hwnd, HOTKEY_LEFT);
            UnregisterHotKey(_hwnd, HOTKEY_UP);
            UnregisterHotKey(_hwnd, HOTKEY_RIGHT);
            UnregisterHotKey(_hwnd, HOTKEY_DOWN);
        }

        private void ClampPosition()
        {
            Left = Math.Max(screenLeft, Math.Min(Left, screenRight - Width));
            Top = Math.Max(screenTop, Math.Min(Top, screenBottom - Height));
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                switch (hotkeyId)
                {
                    case HOTKEY_HIDE: ToggleVisibility(); break;
                    case HOTKEY_LEFT: if (!isLocked) { Left -= MOVE_STEP; ClampPosition(); } break;
                    case HOTKEY_UP: if (!isLocked) { Top -= MOVE_STEP; ClampPosition(); } break;
                    case HOTKEY_RIGHT: if (!isLocked) { Left += MOVE_STEP; ClampPosition(); } break;
                    case HOTKEY_DOWN: if (!isLocked) { Top += MOVE_STEP; ClampPosition(); } break;
                    case HOTKEY_FLIP: FlipBoard(); break;
                    case HOTKEY_LOCK_POS: ToggleLock(); break;
                    case HOTKEY_DESTRUCT: SelfDestruct(); break;
                    case HOTKEY_OPTIONS: ShowOptionsMenu(); break;
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void SelfDestruct() => Application.Current.Shutdown();

        private void ShowOptionsMenu()
        {
            var optionsWindow = new Window
            {
                Width = 350,
                Height = 600, // Slightly taller for status
                Title = "",
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(245, 30, 30, 30)),
                CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 100, 100, 100)),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel { Margin = new Thickness(30) };

            // --- HEADER ---
            stack.Children.Add(new TextBlock
            {
                Text = "⚙️ Options",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            });

            // Current Active Mode Status
            var statusText = new TextBlock
            {
                Text = useExternalEngine ? "Active: ♟️ External Engine" : "Active: 🤖 Internal Bot",
                Foreground = useExternalEngine ? Brushes.LightGreen : Brushes.Cyan,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20)
            };
            stack.Children.Add(statusText);

            TextBlock CreateLabel(string text) => new TextBlock { Text = text, Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)), Margin = new Thickness(0, 15, 0, 5), FontSize = 14 };

            // --- SLIDERS ---
            var opacityLabel = CreateLabel($"Opacity: {Opacity:F2}");
            var opacitySlider = new Slider { Minimum = 0.1, Maximum = 1.0, Value = Opacity, TickFrequency = 0.1, IsSnapToTickEnabled = true };
            opacitySlider.ValueChanged += (s, e) => { Opacity = e.NewValue; savedOpacity = e.NewValue; opacityLabel.Text = $"Opacity: {e.NewValue:F2}"; };
            stack.Children.Add(opacityLabel);
            stack.Children.Add(opacitySlider);

            var sizeLabel = CreateLabel($"Size: {Width:F0}");
            var sizeSlider = new Slider { Minimum = 100, Maximum = 400, Value = Width, TickFrequency = 10, IsSnapToTickEnabled = true };
            sizeSlider.ValueChanged += (s, e) => { Width = e.NewValue; savedSize = e.NewValue; ClampPosition(); sizeLabel.Text = $"Size: {e.NewValue:F0}"; };
            stack.Children.Add(sizeLabel);
            stack.Children.Add(sizeSlider);

            // --- ENGINE SETTINGS ---
            stack.Children.Add(new TextBlock { Text = "♟️ Engine Settings", FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 20, 0, 5) });

            var engineCheck = new CheckBox
            {
                IsChecked = useExternalEngine,
                Content = "Use External Engine",
                Foreground = Brushes.LightGray,
                Margin = new Thickness(0, 5, 0, 5),
                Cursor = Cursors.Hand
            };

            engineCheck.Checked += (s, e) =>
            {
                useExternalEngine = true;
                statusText.Text = "Active: ♟️ External Engine";
                statusText.Foreground = Brushes.LightGreen;
                InitializeExternalEngine();
            };

            engineCheck.Unchecked += (s, e) =>
            {
                useExternalEngine = false;
                statusText.Text = "Active: 🤖 Internal Bot";
                statusText.Foreground = Brushes.Cyan;
                // Safe stop in background to prevent UI freeze/crash
                Task.Run(() => StopExternalEngine());
            };
            stack.Children.Add(engineCheck);

            // Path Selection
            var pathPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            var pathBox = new TextBox { Text = externalEnginePath, Width = 200, Height = 25, VerticalContentAlignment = VerticalAlignment.Center, Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            var browseBtn = new Button { Content = "...", Width = 30, Height = 25, Margin = new Thickness(5, 0, 0, 0), Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)), Foreground = Brushes.White, Cursor = Cursors.Hand };

            browseBtn.Click += (s, e) => {
                var dlg = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe" };
                if (dlg.ShowDialog() == true)
                {
                    externalEnginePath = dlg.FileName;
                    pathBox.Text = externalEnginePath;
                    // If enabled, restart with new path
                    if (useExternalEngine) InitializeExternalEngine();
                }
            };
            pathPanel.Children.Add(pathBox);
            pathPanel.Children.Add(browseBtn);
            stack.Children.Add(pathPanel);

            // Command Input
            stack.Children.Add(CreateLabel("Command (e.g. 'go nodes 1')"));
            var cmdBox = new TextBox { Text = externalEngineCommand, Height = 25, VerticalContentAlignment = VerticalAlignment.Center, Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            cmdBox.TextChanged += (s, e) => externalEngineCommand = cmdBox.Text;
            stack.Children.Add(cmdBox);

            // Bot Depth
            var depthLabel = CreateLabel($"Internal Bot Depth: {currentBotDepth}");
            var depthSlider = new Slider { Minimum = 4, Maximum = 14, Value = currentBotDepth, TickFrequency = 1, IsSnapToTickEnabled = true };
            depthSlider.ValueChanged += (s, e) => {
                currentBotDepth = (int)e.NewValue;
                depthLabel.Text = $"Internal Bot Depth: {currentBotDepth}";
                if (bot != null) bot.SetMaxDepth(currentBotDepth);
            };
            stack.Children.Add(depthLabel);
            stack.Children.Add(depthSlider);

            // Close Button
            var closeButton = new Button
            {
                Content = "Save & Close",
                Margin = new Thickness(0, 20, 0, 0),
                Padding = new Thickness(20, 8, 20, 8),
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };
            closeButton.Click += (s, e) => { SaveSettings(); optionsWindow.Close(); };
            stack.Children.Add(closeButton);

            border.Child = stack;
            optionsWindow.Content = border;
            optionsWindow.ShowDialog();

            SaveSettings();
        }

        private void ToggleLock()
        {
            isLocked = !isLocked;
            if (isLocked) { UnregisterArrowHotkeys(); EnableClickThrough(); }
            else { RegisterArrowHotkeys(); EnableClickThrough(); }
        }

        private void FlipBoard()
        {
            isFlipped = !isFlipped;
            UpdateChessBoard(currentFen, currentBestMove);
        }

        private void ToggleVisibility()
        {
            if (Visibility == Visibility.Visible) Visibility = Visibility.Hidden;
            else { Visibility = Visibility.Visible; Activate(); }
        }

        private void EnableClickThrough()
        {
            var exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            _isClickThrough = true;
        }

        private void DisableClickThrough()
        {
            var exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
            _isClickThrough = false;
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F) { FlipBoard(); e.Handled = true; }
        }

        private void InitializeChessBoard()
        {
            ChessBoard.Children.Clear();
            for (int i = 0; i < 8; i++)
            {
                ChessBoard.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                ChessBoard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    var square = new Border
                    {
                        Background = (row + col) % 2 == 0
                            ? new SolidColorBrush(Color.FromRgb(240, 217, 181))
                            : new SolidColorBrush(Color.FromRgb(181, 136, 99))
                    };
                    var textBlock = new TextBlock
                    {
                        FontSize = 48,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = Brushes.Black
                    };
                    square.Child = textBlock;
                    Grid.SetRow(square, row);
                    Grid.SetColumn(square, col);
                    ChessBoard.Children.Add(square);
                    squareCache[row, col] = square;
                }
            }
        }

        private void UpdateChessBoard(string fen, string? bestMove = null)
        {
            currentFen = fen;
            currentBestMove = bestMove ?? "";
            string[] fenParts = fen.Split(' ');
            string position = fenParts[0];
            string[] ranks = position.Split('/');

            var piecePositions = new List<(int row, int col, string piece)>();
            var highlights = new List<(int row, int col)>();

            for (int rank = 0; rank < 8; rank++)
            {
                int file = 0;
                foreach (char c in ranks[rank])
                {
                    if (char.IsDigit(c)) file += c - '0';
                    else
                    {
                        string pieceKey = (char.IsUpper(c) ? "w" : "b") + char.ToUpper(c);
                        if (pieceUnicode.ContainsKey(pieceKey))
                        {
                            var (displayRow, displayCol) = GetDisplayCoords(rank, file);
                            piecePositions.Add((displayRow, displayCol, pieceUnicode[pieceKey]));
                        }
                        file++;
                    }
                }
            }

            if (!string.IsNullOrEmpty(currentBestMove) && currentBestMove.Length >= 4)
            {
                string from = currentBestMove.Substring(0, 2);
                string to = currentBestMove.Substring(2, 2);
                var (fromRow, fromCol) = SquareNameToDisplayCoords(from);
                var (toRow, toCol) = SquareNameToDisplayCoords(to);
                highlights.Add((fromRow, fromCol));
                highlights.Add((toRow, toCol));
            }

            Dispatcher.Invoke(() =>
            {
                for (int row = 0; row < 8; row++)
                {
                    for (int col = 0; col < 8; col++)
                    {
                        var square = squareCache[row, col];
                        square.Background = (row + col) % 2 == 0
                            ? new SolidColorBrush(Color.FromRgb(240, 217, 181))
                            : new SolidColorBrush(Color.FromRgb(181, 136, 99));
                        if (square.Child is TextBlock tb) tb.Text = "";
                    }
                }
                foreach (var (row, col, piece) in piecePositions)
                {
                    if (squareCache[row, col].Child is TextBlock tb) tb.Text = piece;
                }
                var highlightBrush = new SolidColorBrush(Color.FromRgb(255, 255, 100));
                foreach (var (row, col) in highlights) squareCache[row, col].Background = highlightBrush;
            });
        }

        private void UpdateExplanationBox(string moveInfo, string explanation)
        {
            Dispatcher.Invoke(() =>
            {
                MoveInfoText.Text = moveInfo;
                ExplanationText.Text = explanation;
                ExplanationBorder.Visibility = Visibility.Visible;
            });
        }

        private void HideExplanationBox()
        {
            Dispatcher.Invoke(() => ExplanationBorder.Visibility = Visibility.Collapsed);
        }

        private (int row, int col) GetDisplayCoords(int rank, int file)
        {
            int displayRow = isFlipped ? 7 - rank : rank;
            int displayCol = isFlipped ? 7 - file : file;
            return (displayRow, displayCol);
        }

        private (int row, int col) SquareNameToDisplayCoords(string square)
        {
            int file = square[0] - 'a';
            int rank = 8 - (square[1] - '0');
            return GetDisplayCoords(rank, file);
        }

        // --- LISTENER & GAME LOOP ---
        // --- UPDATED LISTENER LOOP (Crash Fix) ---
        async Task RunListener()
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:30012/");
            try { listener.Start(); } catch { return; }

            while (true)
            {
                try
                {
                    var ctx = await listener.GetContextAsync();
                    if (ctx.Request.HttpMethod == "OPTIONS") { Respond(ctx, 200); continue; }

                    if (ctx.Request.HttpMethod == HttpMethod.Post.Method)
                    {
                        if (!isApiConnectionEnabled) { Respond(ctx, 200); continue; }

                        string requestBody;
                        using (var sr = new StreamReader(ctx.Request.InputStream)) requestBody = await sr.ReadToEndAsync();

                        JsonElement jss;
                        try { jss = JsonSerializer.Deserialize<JsonElement>(requestBody); } catch { Respond(ctx, 400); continue; }

                        try
                        {
                            var position = jss.GetProperty("position").GetString()!;
                            if (position == lastPgn) { Respond(ctx, 200); continue; }

                            lastPgn = position;
                            string fen = ConvertPgnToFen(position);

                            while (bot == null) await Task.Delay(100);

                            string bestMoveUCI = "";
                            double thinkTime = 0;
                            ChessChallenge.API.Move apiMove = ChessChallenge.API.Move.NullMove;
                            var apiBoard = ChessChallenge.API.Board.CreateBoardFromFEN(fen);

                            var startTime = DateTime.Now;

                            // --- 1. CHECK OPENING BOOK FIRST ---
                            // Only use book if we are not analyzing a blunder (optional, but standard behavior)
                            bool foundInBook = openingBook.TryGetBookMove(apiBoard, out string bookMoveStr);

                            if (foundInBook)
                            {
                                bestMoveUCI = bookMoveStr;
                                // Create the API move object for the book move so we can use it for updates/explanation
                                // Note: Book moves in dictionary should be "e2e4", "g8f6" format.
                                apiMove = new ChessChallenge.API.Move(bestMoveUCI, apiBoard);
                                Debug.WriteLine($"📖 Book Move Played: {bestMoveUCI}");
                            }
                            else
                            {
                                // --- 2. IF NO BOOK MOVE, USE ENGINE ---
                                if (useExternalEngine)
                                {
                                    // ... Your existing External Engine Logic ...
                                    if (!_isEngineRunning) InitializeExternalEngine();

                                    if (_isEngineRunning)
                                    {
                                        bestMoveUCI = GetBestMoveFromExternalEngine(fen);
                                        if (!string.IsNullOrEmpty(bestMoveUCI))
                                        {
                                            apiMove = new ChessChallenge.API.Move(bestMoveUCI, apiBoard);
                                        }
                                    }
                                    else
                                    {
                                        var timer = new ChessChallenge.API.Timer(10000, 10000, 1000, 0);
                                        apiMove = bot.Think(apiBoard, timer);
                                        if (!apiMove.IsNull) bestMoveUCI = FormatMove(apiMove);
                                    }
                                }
                                else
                                {
                                    // Use internal bot
                                    var timer = new ChessChallenge.API.Timer(10000, 10000, 1000, 0);
                                    apiMove = bot.Think(apiBoard, timer);
                                    if (!apiMove.IsNull) bestMoveUCI = FormatMove(apiMove);
                                }
                            }

                            thinkTime = (DateTime.Now - startTime).TotalMilliseconds;

                            if (string.IsNullOrEmpty(bestMoveUCI))
                            {
                                UpdateChessBoard(fen, "");
                                Respond(ctx, 200);
                                continue;
                            }

                            UpdateChessBoard(fen, bestMoveUCI);

                            if (isChatbotEnabled && groqHelper != null && !apiMove.IsNull)
                            {
                                _ = Task.Run(() => ProcessAiExplanation(apiBoard, apiMove, bestMoveUCI, thinkTime));
                            }
                            else HideExplanationBox();

                            if (!apiMove.IsNull)
                            {
                                var boardAfterOurMove = ChessChallenge.API.Board.CreateBoardFromFEN(fen);
                                boardAfterOurMove.MakeMove(apiMove);
                                evalBeforeOpponentMove = EvaluatePositionScore(boardAfterOurMove);
                                lastFenBeforeOpponent = boardAfterOurMove.GetFenString();
                            }
                        }
                        catch (Exception ex) { Debug.WriteLine("Loop Error: " + ex.Message); }
                        Respond(ctx, 200);
                    }
                }
                catch { if (!listener.IsListening) break; }
            }
        }
        private void Respond(HttpListenerContext ctx, int statusCode)
        {
            ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
            ctx.Response.AddHeader("Access-Control-Allow-Methods", "POST, OPTIONS");
            ctx.Response.StatusCode = statusCode;
            ctx.Response.OutputStream.Close();
        }

        private string FormatMove(ChessChallenge.API.Move move)
        {
            string uci = $"{move.StartSquare.Name}{move.TargetSquare.Name}";
            if (move.IsPromotion) uci += move.PromotionPieceType.ToString()[0].ToString().ToLower();
            return uci;
        }

        private async Task ProcessAiExplanation(ChessChallenge.API.Board apiBoard, ChessChallenge.API.Move move, string bestMoveUCI, double thinkTime)
        {
            double evalAfterOpponent = EvaluatePositionScore(apiBoard);
            double opponentMoveSwing = 0.0;
            bool opponentBlundered = false;

            if (!string.IsNullOrEmpty(lastFenBeforeOpponent))
            {
                opponentMoveSwing = evalAfterOpponent - evalBeforeOpponentMove;
                opponentBlundered = opponentMoveSwing > 1.0;
            }

            string moveInfo = $"Move: {bestMoveUCI} | {thinkTime:F0}ms";
            if (move.IsCastles) moveInfo += " | ♚";
            if (move.IsCapture) moveInfo += $" | ⚔️";
            if (opponentBlundered) moveInfo = $"🚨 BLUNDER! +{opponentMoveSwing:F1} | {moveInfo}";

            try
            {
                string explanation;
                if (opponentBlundered)
                    explanation = await groqHelper!.ExplainBlunderAsync(apiBoard, move, bestMoveUCI, opponentMoveSwing);
                else
                    explanation = await groqHelper!.ExplainMoveShortAsync(apiBoard, move, bestMoveUCI);
                UpdateExplanationBox(moveInfo, explanation);
            }
            catch { UpdateExplanationBox(moveInfo, "Move executed."); }
        }

        private double EvaluatePositionScore(ChessChallenge.API.Board board)
        {
            int score = 0;
            var pieceValues = new Dictionary<ChessChallenge.API.PieceType, int>
            {
                { ChessChallenge.API.PieceType.Pawn, 100 }, { ChessChallenge.API.PieceType.Knight, 300 },
                { ChessChallenge.API.PieceType.Bishop, 300 }, { ChessChallenge.API.PieceType.Rook, 500 },
                { ChessChallenge.API.PieceType.Queen, 900 }, { ChessChallenge.API.PieceType.King, 0 }
            };
            for (int i = 0; i < 64; i++)
            {
                var piece = board.GetPiece(new ChessChallenge.API.Square(i));
                if (!piece.IsNull && piece.PieceType != ChessChallenge.API.PieceType.None)
                {
                    int value = pieceValues.GetValueOrDefault(piece.PieceType, 0);
                    score += piece.IsWhite ? value : -value;
                }
            }
            return board.IsWhiteToMove ? score / 100.0 : -score / 100.0;
        }

        private string ConvertPgnToFen(string pgn)
        {
            var lines = pgn.Split('\n');
            string movesText = "";
            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("[")) continue;
                if (string.IsNullOrWhiteSpace(line)) continue;
                movesText += " " + line.Trim();
            }
            movesText = Regex.Replace(movesText, @"\d+\.", " ");
            movesText = Regex.Replace(movesText, @"[+#]", "");
            movesText = Regex.Replace(movesText, @"\s*(1-0|0-1|1/2-1/2|\*)\s*$", "");
            movesText = Regex.Replace(movesText, @"=([QRBNqrbn])", "$1");
            movesText = Regex.Replace(movesText, @"\s+", " ");
            movesText = movesText.Trim();

            var board = new Board();
            board.LoadPosition(FenUtility.StartPositionFEN);
            if (!string.IsNullOrWhiteSpace(movesText))
            {
                var moves = movesText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var moveStr in moves)
                {
                    if (string.IsNullOrWhiteSpace(moveStr)) continue;
                    string processedMove = moveStr;
                    var promotionMatch = Regex.Match(moveStr, @"^([a-h])(8|1)([QRBNqrbn])$");
                    if (promotionMatch.Success)
                    {
                        string file = promotionMatch.Groups[1].Value;
                        string targetRank = promotionMatch.Groups[2].Value;
                        string piece = promotionMatch.Groups[3].Value;
                        string startRank = targetRank == "8" ? "7" : "2";
                        processedMove = $"{file}{startRank}{file}{targetRank}{piece}";
                    }
                    board.TryMakeMoveFromSan(processedMove, out Move move);
                }
            }
            return FenUtility.CurrentFen(board);
        }
    }

    // Helper for converting Square Names to Index if missing
    public static class SquareHelper
    {
        public static int SquareTextToIndex(string square)
        {
            int file = square[0] - 'a';
            int rank = square[1] - '1';
            return rank * 8 + file;
        }
    }
}
