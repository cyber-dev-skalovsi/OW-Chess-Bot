using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using ChessChallenge.Chess;
using System.Text.RegularExpressions;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace SystemHelper
{
    public partial class MainWindow : Window
    {
        private MyBot bot;
        private bool isFlipped = false;
        private string currentFen = FenUtility.StartPositionFEN;
        private string currentBestMove = "";
        private string lastPgn = "";
        private bool isLocked = false;

        private Dictionary<string, string> pieceUnicode = new Dictionary<string, string>
        {
            {"wK", "♔"}, {"wQ", "♕"}, {"wR", "♖"}, {"wB", "♗"}, {"wN", "♘"}, {"wP", "♙"},
            {"bK", "♚"}, {"bQ", "♛"}, {"bR", "♜"}, {"bB", "♝"}, {"bN", "♞"}, {"bP", "♟"}
        };

        private double screenLeft, screenTop, screenRight, screenBottom;
        private double savedOpacity = 0.5;
        private double savedSize = 200;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        // Hotkey IDs
        private const int HOTKEY_HIDE = 9000;
        private const int HOTKEY_LEFT = 9001;
        private const int HOTKEY_UP = 9002;
        private const int HOTKEY_RIGHT = 9003;
        private const int HOTKEY_DOWN = 9004;
        private const int HOTKEY_FLIP = 9005;
        private const int HOTKEY_LOCK_POS = 9006;
        private const int HOTKEY_DESTRUCT = 9007;
        private const int HOTKEY_OPTIONS = 9008;

        // Modifiers
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_CTRL_SHIFT = MOD_CONTROL | MOD_SHIFT;

        // Virtual Keys
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

        // Cache for board squares - performance optimization
        private Border[,] squareCache = new Border[8, 8];

        public MainWindow()
        {
            InitializeComponent();

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

            MouseLeftButtonDown += (s, e) =>
            {
                if (!_isClickThrough)
                    DragMove();
            };

            KeyDown += MainWindow_KeyDown;

            Loaded += (s, e) =>
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                RegisterAllHotkeys();
                HwndSource.FromHwnd(_hwnd).AddHook(HwndHook);
                EnableClickThrough();
            };

            Closing += (s, e) =>
            {
                UnregisterAllHotkeys();
            };

            Debug.WriteLine("========================================");
            Debug.WriteLine("🤖 Chess Analysis GUI Starting");
            Debug.WriteLine("========================================");

            bot = new MyBot();
            bot.SetMaxDepth(6);
            Debug.WriteLine("✅ Bot initialized (depth: 10)");

            InitializeChessBoard();
            UpdateChessBoard(currentFen);

            Task.Run(RunListener);
        }

        private void RegisterAllHotkeys()
        {
            RegisterHotKey(_hwnd, HOTKEY_HIDE, MOD_CTRL_SHIFT, VK_H);
            RegisterHotKey(_hwnd, HOTKEY_FLIP, MOD_CTRL_SHIFT, VK_F);
            RegisterHotKey(_hwnd, HOTKEY_LOCK_POS, MOD_CTRL_SHIFT, VK_L);
            RegisterHotKey(_hwnd, HOTKEY_DESTRUCT, MOD_CTRL_SHIFT, VK_X);
            RegisterHotKey(_hwnd, HOTKEY_OPTIONS, MOD_CTRL_SHIFT, VK_O);
            RegisterArrowHotkeys();
            Debug.WriteLine("✅ All hotkeys registered");
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
                    case HOTKEY_HIDE:
                        ToggleVisibility();
                        break;
                    case HOTKEY_LEFT:
                        if (!isLocked)
                        {
                            Left -= MOVE_STEP;
                            ClampPosition();
                        }
                        break;
                    case HOTKEY_UP:
                        if (!isLocked)
                        {
                            Top -= MOVE_STEP;
                            ClampPosition();
                        }
                        break;
                    case HOTKEY_RIGHT:
                        if (!isLocked)
                        {
                            Left += MOVE_STEP;
                            ClampPosition();
                        }
                        break;
                    case HOTKEY_DOWN:
                        if (!isLocked)
                        {
                            Top += MOVE_STEP;
                            ClampPosition();
                        }
                        break;
                    case HOTKEY_FLIP:
                        FlipBoard();
                        break;
                    case HOTKEY_LOCK_POS:
                        ToggleLock();
                        break;
                    case HOTKEY_DESTRUCT:
                        SelfDestruct();
                        break;
                    case HOTKEY_OPTIONS:
                        ShowOptionsMenu();
                        break;
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void SelfDestruct()
        {
            Debug.WriteLine("💥 SELF DESTRUCT ACTIVATED");
            Application.Current.Shutdown();
        }

        private void ShowOptionsMenu()
        {
            var optionsWindow = new Window
            {
                Width = 350,
                Height = 280,
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
                Background = new SolidColorBrush(Color.FromArgb(240, 30, 30, 30)),
                CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 100, 100, 100)),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel { Margin = new Thickness(30) };

            var title = new TextBlock
            {
                Text = "⚙️ Options",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 25)
            };

            // Opacity slider
            var opacityLabel = new TextBlock
            {
                Text = $"Opacity: {Opacity:F2}",
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                Margin = new Thickness(0, 0, 0, 8),
                FontSize = 14
            };
            var opacitySlider = new Slider
            {
                Minimum = 0.1,
                Maximum = 1.0,
                Value = Opacity,
                TickFrequency = 0.1,
                IsSnapToTickEnabled = true,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
            };
            opacitySlider.ValueChanged += (s, e) =>
            {
                Opacity = e.NewValue;
                savedOpacity = e.NewValue;
                opacityLabel.Text = $"Opacity: {e.NewValue:F2}";
            };

            // Size slider
            var sizeLabel = new TextBlock
            {
                Text = $"Size: {Width:F0}",
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                Margin = new Thickness(0, 20, 0, 8),
                FontSize = 14
            };
            var sizeSlider = new Slider
            {
                Minimum = 100,
                Maximum = 400,
                Value = Width,
                TickFrequency = 10,
                IsSnapToTickEnabled = true,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
            };
            sizeSlider.ValueChanged += (s, e) =>
            {
                Width = e.NewValue;
                Height = e.NewValue;
                savedSize = e.NewValue;
                ClampPosition();
                sizeLabel.Text = $"Size: {e.NewValue:F0}";
            };

            var closeButton = new Button
            {
                Content = "Close",
                Margin = new Thickness(0, 25, 0, 0),
                Padding = new Thickness(40, 10, 40, 10),
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand
            };

            closeButton.MouseEnter += (s, e) => closeButton.Background = new SolidColorBrush(Color.FromRgb(80, 80, 80));
            closeButton.MouseLeave += (s, e) => closeButton.Background = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            closeButton.Click += (s, e) => optionsWindow.Close();

            stack.Children.Add(title);
            stack.Children.Add(opacityLabel);
            stack.Children.Add(opacitySlider);
            stack.Children.Add(sizeLabel);
            stack.Children.Add(sizeSlider);
            stack.Children.Add(closeButton);
            border.Child = stack;
            optionsWindow.Content = border;

            optionsWindow.ShowDialog();
        }

        private void ToggleLock()
        {
            isLocked = !isLocked;
            if (isLocked)
            {
                UnregisterArrowHotkeys();
                EnableClickThrough();
                Debug.WriteLine("🔒 Position locked (Ctrl+Shift+L) - still click-through");
            }
            else
            {
                RegisterArrowHotkeys();
                EnableClickThrough();
                Debug.WriteLine("🔓 Position unlocked (Ctrl+Shift+L)");
            }
        }

        private void FlipBoard()
        {
            isFlipped = !isFlipped;
            Debug.WriteLine($"🔄 Board Flipped (Ctrl+Shift+F): {isFlipped}");
            UpdateChessBoard(currentFen, currentBestMove);
        }

        private void ToggleVisibility()
        {
            if (Visibility == Visibility.Visible)
            {
                Visibility = Visibility.Hidden;
                Debug.WriteLine("🔽 GUI hidden (Ctrl+Shift+H)");
            }
            else
            {
                Visibility = Visibility.Visible;
                Activate();
                Debug.WriteLine("🔼 GUI visible (Ctrl+Shift+H)");
            }
        }

        private void EnableClickThrough()
        {
            var exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            var layeredExStyle = exStyle | WS_EX_LAYERED;
            SetWindowLong(_hwnd, GWL_EXSTYLE, layeredExStyle | WS_EX_TRANSPARENT);
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
            if (e.Key == Key.F)
            {
                FlipBoard();
                e.Handled = true;
            }
        }

        private void InitializeChessBoard()
        {
            ChessBoard.Children.Clear();
            for (int i = 0; i < 8; i++)
            {
                ChessBoard.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                ChessBoard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Pre-create all squares and cache them
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

                    // Cache the square for fast lookup
                    squareCache[row, col] = square;
                }
            }
            Debug.WriteLine("✅ Chess board initialized with cache");
        }

        private void UpdateChessBoard(string fen, string? bestMove = null)
        {
            currentFen = fen;
            currentBestMove = bestMove ?? "";

            // Parse FEN outside of Dispatcher for performance
            string[] fenParts = fen.Split(' ');
            string position = fenParts[0];
            string[] ranks = position.Split('/');

            // Pre-calculate all piece positions and highlights
            var piecePositions = new List<(int row, int col, string piece)>();
            var highlights = new List<(int row, int col)>();

            // Calculate piece positions
            for (int rank = 0; rank < 8; rank++)
            {
                int file = 0;
                foreach (char c in ranks[rank])
                {
                    if (char.IsDigit(c))
                    {
                        file += c - '0';
                    }
                    else
                    {
                        string pieceKey = char.IsUpper(c) ? "w" : "b";
                        pieceKey += char.ToUpper(c);
                        if (pieceUnicode.ContainsKey(pieceKey))
                        {
                            var (displayRow, displayCol) = GetDisplayCoords(rank, file);
                            piecePositions.Add((displayRow, displayCol, pieceUnicode[pieceKey]));
                        }
                        file++;
                    }
                }
            }

            // Calculate highlights
            if (!string.IsNullOrEmpty(currentBestMove) && currentBestMove.Length >= 4)
            {
                string from = currentBestMove.Substring(0, 2);
                string to = currentBestMove.Substring(2, 2);
                var (fromRow, fromCol) = SquareNameToDisplayCoords(from);
                var (toRow, toCol) = SquareNameToDisplayCoords(to);
                highlights.Add((fromRow, fromCol));
                highlights.Add((toRow, toCol));
            }

            // Now update UI in single Dispatcher call
            Dispatcher.Invoke(() =>
            {
                // Reset all squares using cache
                for (int row = 0; row < 8; row++)
                {
                    for (int col = 0; col < 8; col++)
                    {
                        var square = squareCache[row, col];
                        square.Background = (row + col) % 2 == 0
                            ? new SolidColorBrush(Color.FromRgb(240, 217, 181))
                            : new SolidColorBrush(Color.FromRgb(181, 136, 99));

                        if (square.Child is TextBlock tb)
                        {
                            tb.Text = "";
                        }
                    }
                }

                // Place all pieces
                foreach (var (row, col, piece) in piecePositions)
                {
                    if (squareCache[row, col].Child is TextBlock tb)
                    {
                        tb.Text = piece;
                    }
                }

                // Apply highlights
                var highlightBrush = new SolidColorBrush(Color.FromRgb(255, 255, 100));
                foreach (var (row, col) in highlights)
                {
                    squareCache[row, col].Background = highlightBrush;
                }
            });
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

        async Task RunListener()
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:30012/");

            try
            {
                listener.Start();
                Debug.WriteLine("========================================");
                Debug.WriteLine("✅ GUI listening on http://localhost:30012/");
                Debug.WriteLine("========================================\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ FATAL: Failed to start listener: {ex.Message}");
                return;
            }

            int requestCount = 0;
            while (true)
            {
                try
                {
                    var ctx = await listener.GetContextAsync();
                    requestCount++;

                    Debug.WriteLine($"\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    Debug.WriteLine($"📥 Request #{requestCount}: {ctx.Request.HttpMethod}");

                    if (ctx.Request.HttpMethod == "OPTIONS")
                    {
                        Debug.WriteLine(" ↳ CORS preflight");
                        ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
                        ctx.Response.AddHeader("Access-Control-Allow-Methods", "POST, OPTIONS");
                        ctx.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
                        ctx.Response.StatusCode = 200;
                        ctx.Response.OutputStream.Close();
                        continue;
                    }

                    if (ctx.Request.HttpMethod == HttpMethod.Post.Method)
                    {
                        Debug.WriteLine(" 📨 POST request received");
                        ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");

                        string requestBody;
                        using (var sr = new StreamReader(ctx.Request.InputStream))
                        {
                            requestBody = await sr.ReadToEndAsync();
                        }

                        JsonElement jss;
                        try
                        {
                            jss = JsonSerializer.Deserialize<JsonElement>(requestBody);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($" ❌ JSON parse error: {ex.Message}");
                            ctx.Response.StatusCode = 400;
                            ctx.Response.OutputStream.Close();
                            continue;
                        }

                        try
                        {
                            var position = jss.GetProperty("position").GetString()!;

                            if (position == lastPgn)
                            {
                                Debug.WriteLine(" ⏭️ Same position, skipping");
                                ctx.Response.StatusCode = 200;
                                ctx.Response.OutputStream.Close();
                                continue;
                            }

                            lastPgn = position;
                            Debug.WriteLine($" ♟️ New position ({position.Length} chars)");

                            string fen = ConvertPgnToFen(position);

                            var startTime = DateTime.Now;
                            var board = new Board();
                            board.LoadPosition(fen);

                            var move = bot.Think(
                                new ChessChallenge.API.Board(board),
                                new ChessChallenge.API.Timer(10000, 10000, 1000, 0)
                            );

                            var thinkTime = (DateTime.Now - startTime).TotalMilliseconds;

                            if (move.RawValue == 0)
                            {
                                Debug.WriteLine(" ⚠️ NULL move");
                                UpdateChessBoard(fen, "");
                            }
                            else
                            {
                                var bestMove = MoveUtility.GetMoveNameUCI(new Move(move.RawValue));
                                Debug.WriteLine($" ✅ Move: {bestMove} ({thinkTime:F0}ms)");
                                UpdateChessBoard(fen, bestMove);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($" ❌ ERROR: {ex.Message}");
                            currentFen = FenUtility.StartPositionFEN;
                            UpdateChessBoard(currentFen, "");
                        }

                        ctx.Response.StatusCode = 200;
                        ctx.Response.OutputStream.Close();
                    }

                    Debug.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ LISTENER ERROR: {ex.Message}");
                }
            }
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
}