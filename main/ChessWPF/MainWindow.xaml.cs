using ChessChallenge.AI;
using ChessChallenge.Chess;
using ChessChallenge.Evaluation;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SystemHelper
{
    public class AppSettings
    {
        public double Opacity { get; set; } = 0.5;
        public double Size { get; set; } = 200;
        public bool IsApiEnabled { get; set; } = true;
        public bool IsChatbotEnabled { get; set; } = true;
        public int BotDepth { get; set; } = 6;
        public bool UseExternalEngine { get; set; }
        public string EnginePath { get; set; } = "";
        public string EngineCommand { get; set; } = "go nodes 1";
        public int HighlightMode { get; set; } = 0; // 0 = Normal, 1 = Yellow Boxes, 2 = Pink Dots
    }

    public partial class MainWindow : Window
    {
        private MyBot bot;
        private GroqAIHelper? groqHelper;
        private OpeningBook openingBook;
        private bool isFlipped, isLocked;
        private string currentFen = FenUtility.StartPositionFEN;
        private string currentBestMove = "", lastPgn = "";
        private Process? _engineProcess;
        private bool _isEngineRunning;
        private double evalBeforeOpponentMove, savedOpacity = 0.5, savedSize = 200;
        private double screenLeft, screenTop, screenRight, screenBottom;
        private string lastFenBeforeOpponent = "";
        private bool isApiConnectionEnabled = true, isChatbotEnabled = true;
        private int currentBotDepth = 6;
        private bool useExternalEngine;
        private string externalEnginePath = "", externalEngineCommand = "go nodes 1";
        private int highlightMode = 0; // 0 = Normal, 1 = Yellow Boxes, 2 = Pink Dots
        private const string SettingsFile = "settings.json";

        private readonly Dictionary<string, string> pieceUnicode = new()
        {
            {"wK", "♔"}, {"wQ", "♕"}, {"wR", "♖"}, {"wB", "♗"}, {"wN", "♘"}, {"wP", "♙"},
            {"bK", "♚"}, {"bQ", "♛"}, {"bR", "♜"}, {"bB", "♝"}, {"bN", "♞"}, {"bP", "♟"}
        };

        private readonly Border[,] squareCache = new Border[8, 8];

        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int HOTKEY_HIDE = 9000, HOTKEY_LEFT = 9001, HOTKEY_UP = 9002, HOTKEY_RIGHT = 9003;
        private const int HOTKEY_DOWN = 9004, HOTKEY_FLIP = 9005, HOTKEY_LOCK_POS = 9006;
        private const int HOTKEY_DESTRUCT = 9007, HOTKEY_OPTIONS = 9008;
        private const int HOTKEY_LEFT_SLOW = 9009, HOTKEY_UP_SLOW = 9010, HOTKEY_RIGHT_SLOW = 9011, HOTKEY_DOWN_SLOW = 9012;

        private const uint MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_CTRL_SHIFT = MOD_CONTROL | MOD_SHIFT;
        private const uint VK_H = 0x48, VK_F = 0x46, VK_L = 0x4C, VK_X = 0x58, VK_O = 0x4F;
        private const uint VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28;
        private const int GWL_EXSTYLE = -20, WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20;
        private const double MOVE_STEP = 10.0;
        private const double MOVE_STEP_SLOW = 1.0;

        private IntPtr _hwnd;
        private bool _isClickThrough = true;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();

            Title = "";
            ShowInTaskbar = false;

            (screenLeft, screenTop, screenRight, screenBottom) =
                (SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top,
                 SystemParameters.WorkArea.Right, SystemParameters.WorkArea.Bottom);

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

                Task.Run(() =>
                {
                    bot = new();
                    bot.SetMaxDepth(currentBotDepth);
                    openingBook = new();
                    try { groqHelper = new(); } catch { }
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

        private void InitializeExternalEngine()
        {
            StopExternalEngine();

            if (string.IsNullOrWhiteSpace(externalEnginePath) || !File.Exists(externalEnginePath))
            {
                Debug.WriteLine("❌ Engine path invalid or empty.");
                return;
            }

            try
            {
                _engineProcess = new()
                {
                    StartInfo = new()
                    {
                        FileName = externalEnginePath,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                _engineProcess.Start();
                _engineProcess.StandardInput.WriteLine("uci");
                _engineProcess.StandardInput.WriteLine("isready");
                _isEngineRunning = true;
                Debug.WriteLine($"✅ External Engine Started: {System.IO.Path.GetFileName(externalEnginePath)}");
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
                try 
                { 
                    _engineProcess.StandardInput.WriteLine("quit");
                    if (!_engineProcess.WaitForExit(500))
                    {
                        _engineProcess.Kill();
                    }
                } 
                catch { }

                try { _engineProcess.Dispose(); } catch { }
            }
            _engineProcess = null;
            _isEngineRunning = false;
        }

        private string GetBestMoveFromExternalEngine(string fen)
        {
            if (_engineProcess == null || _engineProcess.HasExited || !_isEngineRunning)
            {
                InitializeExternalEngine();
                if (!_isEngineRunning || _engineProcess == null) return "";
            }

            try
            {
                while (_engineProcess.StandardOutput.Peek() > -1) 
                    _engineProcess.StandardOutput.ReadLine();

                _engineProcess.StandardInput.WriteLine($"position fen {fen}");
                _engineProcess.StandardInput.WriteLine(externalEngineCommand);

                string? line;
                while ((line = _engineProcess.StandardOutput.ReadLine()) != null)
                {
                    if (line.StartsWith("bestmove"))
                    {
                        var parts = line.Split(' ');
                        return parts.Length > 1 ? parts[1] : "";
                    }
                }
            }
            catch (Exception ex) 
            { 
                Debug.WriteLine($"❌ Engine communication error: {ex.Message}");
                _isEngineRunning = false;
            }
            return "";
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return;

                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile));
                if (settings == null) return;

                savedOpacity = settings.Opacity;
                savedSize = settings.Size;
                isApiConnectionEnabled = settings.IsApiEnabled;
                isChatbotEnabled = settings.IsChatbotEnabled;
                currentBotDepth = Math.Clamp(settings.BotDepth, 4, 14);
                useExternalEngine = settings.UseExternalEngine;
                externalEnginePath = settings.EnginePath;
                externalEngineCommand = settings.EngineCommand;
                highlightMode = settings.HighlightMode;
            }
            catch (Exception ex) { Debug.WriteLine($"Failed to load settings: {ex.Message}"); }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new AppSettings
                {
                    Opacity = Opacity,
                    Size = Width,
                    IsApiEnabled = isApiConnectionEnabled,
                    IsChatbotEnabled = isChatbotEnabled,
                    BotDepth = currentBotDepth,
                    UseExternalEngine = useExternalEngine,
                    EnginePath = externalEnginePath,
                    EngineCommand = externalEngineCommand,
                    HighlightMode = highlightMode
                };
                File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

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

            RegisterHotKey(_hwnd, HOTKEY_LEFT_SLOW, MOD_CONTROL, VK_LEFT);
            RegisterHotKey(_hwnd, HOTKEY_UP_SLOW, MOD_CONTROL, VK_UP);
            RegisterHotKey(_hwnd, HOTKEY_RIGHT_SLOW, MOD_CONTROL, VK_RIGHT);
            RegisterHotKey(_hwnd, HOTKEY_DOWN_SLOW, MOD_CONTROL, VK_DOWN);
        }

        private void UnregisterArrowHotkeys()
        {
            UnregisterHotKey(_hwnd, HOTKEY_LEFT);
            UnregisterHotKey(_hwnd, HOTKEY_UP);
            UnregisterHotKey(_hwnd, HOTKEY_RIGHT);
            UnregisterHotKey(_hwnd, HOTKEY_DOWN);

            UnregisterHotKey(_hwnd, HOTKEY_LEFT_SLOW);
            UnregisterHotKey(_hwnd, HOTKEY_UP_SLOW);
            UnregisterHotKey(_hwnd, HOTKEY_RIGHT_SLOW);
            UnregisterHotKey(_hwnd, HOTKEY_DOWN_SLOW);
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
                switch (wParam.ToInt32())
                {
                    case HOTKEY_HIDE: ToggleVisibility(); break;
                    case HOTKEY_LEFT: if (!isLocked) { Left -= MOVE_STEP; ClampPosition(); } break;
                    case HOTKEY_UP: if (!isLocked) { Top -= MOVE_STEP; ClampPosition(); } break;
                    case HOTKEY_RIGHT: if (!isLocked) { Left += MOVE_STEP; ClampPosition(); } break;
                    case HOTKEY_DOWN: if (!isLocked) { Top += MOVE_STEP; ClampPosition(); } break;
                    case HOTKEY_LEFT_SLOW: if (!isLocked) { Left -= MOVE_STEP_SLOW; ClampPosition(); } break;
                    case HOTKEY_UP_SLOW: if (!isLocked) { Top -= MOVE_STEP_SLOW; ClampPosition(); } break;
                    case HOTKEY_RIGHT_SLOW: if (!isLocked) { Left += MOVE_STEP_SLOW; ClampPosition(); } break;
                    case HOTKEY_DOWN_SLOW: if (!isLocked) { Top += MOVE_STEP_SLOW; ClampPosition(); } break;
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
                Title = "Options",
                Width = 450,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow,
                Background = SystemColors.ControlBrush
            };

            optionsWindow.MouseLeftButtonDown += (s, e) => optionsWindow.DragMove();

            var mainStack = new StackPanel { Margin = new Thickness(15) };

            var statusPanel = new GroupBox
            {
                Header = "Current Mode",
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10)
            };

            var statusText = new TextBlock
            {
                Text = useExternalEngine ? "♟️ External Engine Active" : "🤖 Internal Bot Active",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = useExternalEngine ? Brushes.Green : Brushes.Blue,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            statusPanel.Content = statusText;
            mainStack.Children.Add(statusPanel);

            var displayGroup = new GroupBox
            {
                Header = "Display Settings",
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10)
            };
            var displayStack = new StackPanel();

            var opacityLabel = new TextBlock { Text = $"Opacity: {Opacity:F2}", Margin = new Thickness(0, 5, 0, 5) };
            var opacitySlider = new Slider 
            { 
                Minimum = 0.1, 
                Maximum = 1.0, 
                Value = Opacity, 
                TickFrequency = 0.1, 
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 0, 0, 10)
            };
            opacitySlider.ValueChanged += (s, e) =>
            {
                Opacity = savedOpacity = e.NewValue;
                opacityLabel.Text = $"Opacity: {e.NewValue:F2}";
            };
            displayStack.Children.Add(opacityLabel);
            displayStack.Children.Add(opacitySlider);

            var sizeLabel = new TextBlock { Text = $"Size: {Width:F0}px", Margin = new Thickness(0, 5, 0, 5) };
            var sizeSlider = new Slider 
            { 
                Minimum = 100, 
                Maximum = 800, 
                Value = Width, 
                TickFrequency = 10, 
                IsSnapToTickEnabled = true 
            };
            sizeSlider.ValueChanged += (s, e) =>
            {
                Width = savedSize = e.NewValue;
                Height = e.NewValue;
                ClampPosition();
                sizeLabel.Text = $"Size: {e.NewValue:F0}px";
            };
            displayStack.Children.Add(sizeLabel);
            displayStack.Children.Add(sizeSlider);

            // Highlight Mode Radio Buttons
            var highlightLabel = new TextBlock { Text = "Highlight Mode:", Margin = new Thickness(0, 15, 0, 5), FontWeight = FontWeights.Bold };
            displayStack.Children.Add(highlightLabel);

            var normalRadio = new RadioButton
            {
                Content = "Normal (Full Board)",
                IsChecked = highlightMode == 0,
                Margin = new Thickness(0, 5, 0, 5),
                GroupName = "HighlightMode"
            };
            normalRadio.Checked += (s, e) => { highlightMode = 0; UpdateChessBoard(currentFen, currentBestMove); };
            displayStack.Children.Add(normalRadio);

            var boxRadio = new RadioButton
            {
                Content = "Yellow Boxes (Transparent Board)",
                IsChecked = highlightMode == 1,
                Margin = new Thickness(0, 5, 0, 5),
                GroupName = "HighlightMode"
            };
            boxRadio.Checked += (s, e) => { highlightMode = 1; UpdateChessBoard(currentFen, currentBestMove); };
            displayStack.Children.Add(boxRadio);

            var dotRadio = new RadioButton
            {
                Content = "Pink Dots (Transparent Board)",
                IsChecked = highlightMode == 2,
                Margin = new Thickness(0, 5, 0, 5),
                GroupName = "HighlightMode"
            };
            dotRadio.Checked += (s, e) => { highlightMode = 2; UpdateChessBoard(currentFen, currentBestMove); };
            displayStack.Children.Add(dotRadio);

            displayGroup.Content = displayStack;
            mainStack.Children.Add(displayGroup);

            var engineGroup = new GroupBox
            {
                Header = "Engine Settings",
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10)
            };
            var engineStack = new StackPanel();

            var engineCheck = new CheckBox
            {
                Content = "Use External Engine",
                IsChecked = useExternalEngine,
                Margin = new Thickness(0, 5, 0, 10)
            };
            engineCheck.Checked += (s, e) =>
            {
                useExternalEngine = true;
                statusText.Text = "♟️ External Engine Active";
                statusText.Foreground = Brushes.Green;
                InitializeExternalEngine();
            };
            engineCheck.Unchecked += (s, e) =>
            {
                useExternalEngine = false;
                statusText.Text = "🤖 Internal Bot Active";
                statusText.Foreground = Brushes.Blue;
                StopExternalEngine();
            };
            engineStack.Children.Add(engineCheck);

            var pathLabel = new TextBlock { Text = "Engine Path:", Margin = new Thickness(0, 5, 0, 5) };
            engineStack.Children.Add(pathLabel);

            var pathPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var browseBtn = new Button 
            { 
                Content = "Browse...", 
                Width = 80,
                Margin = new Thickness(5, 0, 0, 0)
            };
            DockPanel.SetDock(browseBtn, Dock.Right);

            var pathBox = new TextBox 
            { 
                Text = externalEnginePath,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            browseBtn.Click += (s, e) =>
            {
                var dlg = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*" };
                if (dlg.ShowDialog() == true)
                {
                    pathBox.Text = externalEnginePath = dlg.FileName;
                    if (useExternalEngine) InitializeExternalEngine();
                }
            };

            pathPanel.Children.Add(browseBtn);
            pathPanel.Children.Add(pathBox);
            engineStack.Children.Add(pathPanel);

            var cmdLabel = new TextBlock { Text = "UCI Command:", Margin = new Thickness(0, 5, 0, 5) };
            engineStack.Children.Add(cmdLabel);
            var cmdBox = new TextBox 
            { 
                Text = externalEngineCommand,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            cmdBox.TextChanged += (s, e) => externalEngineCommand = cmdBox.Text;
            engineStack.Children.Add(cmdBox);

            var depthLabel = new TextBlock { Text = $"Internal Bot Depth: {currentBotDepth}", Margin = new Thickness(0, 5, 0, 5) };
            var depthSlider = new Slider 
            { 
                Minimum = 4, 
                Maximum = 14, 
                Value = currentBotDepth, 
                TickFrequency = 1, 
                IsSnapToTickEnabled = true 
            };
            depthSlider.ValueChanged += (s, e) =>
            {
                currentBotDepth = (int)e.NewValue;
                depthLabel.Text = $"Internal Bot Depth: {currentBotDepth}";
                bot?.SetMaxDepth(currentBotDepth);
            };
            engineStack.Children.Add(depthLabel);
            engineStack.Children.Add(depthSlider);

            engineGroup.Content = engineStack;
            mainStack.Children.Add(engineGroup);

            var buttonPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var saveButton = new Button
            {
                Content = "Save & Close",
                Width = 100,
                Height = 30,
                Margin = new Thickness(5, 0, 0, 0),
                IsDefault = true
            };
            saveButton.Click += (s, e) =>
            {
                SaveSettings();
                optionsWindow.Close();
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                IsCancel = true
            };

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(saveButton);
            mainStack.Children.Add(buttonPanel);

            optionsWindow.Content = mainStack;
            optionsWindow.ShowDialog();
        }

        private void ToggleLock()
        {
            isLocked = !isLocked;
            if (isLocked) UnregisterArrowHotkeys();
            else RegisterArrowHotkeys();
            EnableClickThrough();
        }

        private void FlipBoard()
        {
            isFlipped = !isFlipped;
            UpdateChessBoard(currentFen, currentBestMove);
        }

        private void ToggleVisibility()
        {
            Visibility = Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
            if (Visibility == Visibility.Visible) Activate();
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
            ChessBoard.Background = Brushes.Transparent; // Make grid background transparent

            for (int i = 0; i < 8; i++)
            {
                ChessBoard.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
                ChessBoard.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            }

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    var square = new Border
                    {
                        Background = (row + col) % 2 == 0
                            ? new SolidColorBrush(Color.FromRgb(240, 217, 181))
                            : new SolidColorBrush(Color.FromRgb(181, 136, 99)),
                        Child = new TextBlock
                        {
                            FontSize = 48,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = Brushes.Black
                        }
                    };
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
            string[] ranks = fenParts[0].Split('/');

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
                var (fromRow, fromCol) = SquareNameToDisplayCoords(currentBestMove[..2]);
                var (toRow, toCol) = SquareNameToDisplayCoords(currentBestMove.Substring(2, 2));
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

                        if (highlightMode == 0)
                        {
                            // Normal mode
                            square.Background = (row + col) % 2 == 0
                                ? new SolidColorBrush(Color.FromRgb(240, 217, 181))
                                : new SolidColorBrush(Color.FromRgb(181, 136, 99));
                            if (square.Child is TextBlock tb) tb.Text = "";
                        }
                        else
                        {
                            // Transparent modes (1 = Yellow Boxes, 2 = Pink Dots)
                            square.Background = Brushes.Transparent;
                            if (square.Child is TextBlock tb) tb.Text = "";

                            // Remove any existing dots
                            if (square.Child is Grid g)
                            {
                                g.Children.Clear();
                            }
                        }
                    }
                }

                if (highlightMode == 0)
                {
                    // Normal mode - show pieces
                    foreach (var (row, col, piece) in piecePositions)
                        if (squareCache[row, col].Child is TextBlock tb) tb.Text = piece;

                    // Yellow highlight boxes
                    var highlightBrush = new SolidColorBrush(Color.FromRgb(255, 255, 100));
                    foreach (var (row, col) in highlights)
                        squareCache[row, col].Background = highlightBrush;
                }
                else if (highlightMode == 1)
                {
                    // Yellow Boxes mode
                    var highlightBrush = new SolidColorBrush(Color.FromRgb(255, 255, 100));
                    foreach (var (row, col) in highlights)
                        squareCache[row, col].Background = highlightBrush;
                }
                else if (highlightMode == 2)
                {
                    // Pink Dots mode
                    foreach (var (row, col) in highlights)
                    {
                        var square = squareCache[row, col];

                        // Create a grid to hold the dot
                        var grid = new Grid();

                        // Create pink dot
                        var dot = new Ellipse
                        {
                            Width = 20,
                            Height = 20,
                            Fill = new SolidColorBrush(Color.FromRgb(255, 105, 180)), // Hot pink
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Bottom,
                            Margin = new Thickness(0, 0, 5, 5)
                        };

                        grid.Children.Add(dot);
                        square.Child = grid;
                    }
                }
            });
        }

        private void UpdateExplanationBox(string moveInfo, string explanation) =>
            Dispatcher.Invoke(() =>
            {
                MoveInfoText.Text = moveInfo;
                ExplanationText.Text = explanation;
                ExplanationBorder.Visibility = Visibility.Visible;
            });

        private void HideExplanationBox() =>
            Dispatcher.Invoke(() => ExplanationBorder.Visibility = Visibility.Collapsed);

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

        async Task RunListener()
        {
            var listener = new HttpListener();
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
                        using (var sr = new StreamReader(ctx.Request.InputStream))
                            requestBody = await sr.ReadToEndAsync();

                        JsonElement jss;
                        try { jss = JsonSerializer.Deserialize<JsonElement>(requestBody); }
                        catch { Respond(ctx, 400); continue; }

                        try
                        {
                            var position = jss.GetProperty("position").GetString()!;
                            string type = jss.TryGetProperty("type", out var typeProp)
                                ? typeProp.GetString() ?? ""
                                : "";

                            string fen;

                            if (string.Equals(type, "FEN", StringComparison.OrdinalIgnoreCase))
                            {
                                fen = position;
                            }
                            else
                            {
                                if (position == lastPgn) { Respond(ctx, 200); continue; }
                                lastPgn = position;
                                fen = ConvertPgnToFen(position);
                            }

                            while (bot == null) await Task.Delay(100);

                            string bestMoveUCI = "";
                            double thinkTime;
                            var apiMove = ChessChallenge.API.Move.NullMove;
                            var apiBoard = ChessChallenge.API.Board.CreateBoardFromFEN(fen);
                            var startTime = DateTime.Now;

                            bool foundInBook = openingBook.TryGetBookMove(apiBoard, out string bookMoveStr);

                            if (foundInBook)
                            {
                                bestMoveUCI = bookMoveStr;
                                apiMove = new(bestMoveUCI, apiBoard);
                                Debug.WriteLine($"📖 Book Move Played: {bestMoveUCI}");
                            }
                            else
                            {
                                if (useExternalEngine)
                                {
                                    if (!_isEngineRunning) InitializeExternalEngine();

                                    if (_isEngineRunning)
                                    {
                                        bestMoveUCI = GetBestMoveFromExternalEngine(fen);
                                        if (!string.IsNullOrEmpty(bestMoveUCI))
                                            apiMove = new(bestMoveUCI, apiBoard);
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
                                _ = Task.Run(() => ProcessAiExplanation(apiBoard, apiMove, bestMoveUCI, thinkTime));
                            else
                                HideExplanationBox();

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
            if (move.IsCapture) moveInfo += " | ⚔️";
            if (opponentBlundered) moveInfo = $"🚨 BLUNDER! +{opponentMoveSwing:F1} | {moveInfo}";

            try
            {
                string explanation = opponentBlundered
                    ? await groqHelper!.ExplainBlunderAsync(apiBoard, move, bestMoveUCI, opponentMoveSwing)
                    : await groqHelper!.ExplainMoveShortAsync(apiBoard, move, bestMoveUCI);
                UpdateExplanationBox(moveInfo, explanation);
            }
            catch { UpdateExplanationBox(moveInfo, "Move executed."); }
        }

        private double EvaluatePositionScore(ChessChallenge.API.Board board)
        {
            int score = 0;
            var pieceValues = new Dictionary<ChessChallenge.API.PieceType, int>
            {
                { ChessChallenge.API.PieceType.Pawn, 100 },
                { ChessChallenge.API.PieceType.Knight, 300 },
                { ChessChallenge.API.PieceType.Bishop, 300 },
                { ChessChallenge.API.PieceType.Rook, 500 },
                { ChessChallenge.API.PieceType.Queen, 900 },
                { ChessChallenge.API.PieceType.King, 0 }
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
            var sb = new StringBuilder();

            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("[") || string.IsNullOrWhiteSpace(line)) continue;
                sb.Append(' ').Append(line.Trim());
            }

            string movesText = sb.ToString();
            movesText = Regex.Replace(movesText, @"\d+\.", " ");
            movesText = Regex.Replace(movesText, @"[+#]", "");
            movesText = Regex.Replace(movesText, @"\s*(1-0|0-1|1/2-1/2|\*)\s*$", "");
            movesText = Regex.Replace(movesText, @"=([QRBNqrbn])", "$1");
            movesText = Regex.Replace(movesText, @"\s+", " ").Trim();

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
