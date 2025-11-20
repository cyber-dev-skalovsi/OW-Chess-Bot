using ChessChallenge.AI;
using ChessChallenge.Chess;
using ChessChallenge.Evaluation;
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
    public partial class MainWindow : Window
    {
        private MyBot bot;
        private GroqAIHelper groqHelper;
        private bool isFlipped = false;
        private string currentFen = FenUtility.StartPositionFEN;
        private string currentBestMove = "";
        private string lastPgn = "";
        private bool isLocked = false;
        private double evalBeforeOpponentMove = 0.0;
        private string lastFenBeforeOpponent = "";
        private bool isApiConnectionEnabled = true;
        private bool isChatbotEnabled = true;

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

            this.SizeToContent = SizeToContent.Height;

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

            bot = new MyBot();
            bot.SetMaxDepth(6);
            groqHelper = new GroqAIHelper();
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
            Application.Current.Shutdown();
        }

        private void ShowOptionsMenu()
        {
            var optionsWindow = new Window
            {
                Width = 350,
                Height = 380,
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

            var sizeLabel = new TextBlock
            {
                Text = $"Width: {Width:F0}",
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
                savedSize = e.NewValue;
                ClampPosition();
                sizeLabel.Text = $"Width: {e.NewValue:F0}";
            };

            // API Connection toggle
            var apiConnectionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var apiCheckBox = new CheckBox
            {
                IsChecked = isApiConnectionEnabled,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };

            var apiLabel = new TextBlock
            {
                Text = "Enable API Connection",
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                FontSize = 14,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            apiCheckBox.Checked += (s, e) => isApiConnectionEnabled = true;
            apiCheckBox.Unchecked += (s, e) =>
            {
                isApiConnectionEnabled = false;
                HideExplanationBox();
            };

            apiConnectionPanel.Children.Add(apiCheckBox);
            apiConnectionPanel.Children.Add(apiLabel);

            // Chatbot toggle
            var chatbotPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var chatbotCheckBox = new CheckBox
            {
                IsChecked = isChatbotEnabled,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };

            var chatbotLabel = new TextBlock
            {
                Text = "Enable AI Explanations",
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                FontSize = 14,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            chatbotCheckBox.Checked += (s, e) => isChatbotEnabled = true;
            chatbotCheckBox.Unchecked += (s, e) =>
            {
                isChatbotEnabled = false;
                HideExplanationBox();
            };

            chatbotPanel.Children.Add(chatbotCheckBox);
            chatbotPanel.Children.Add(chatbotLabel);

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
            stack.Children.Add(apiConnectionPanel);
            stack.Children.Add(chatbotPanel);
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
            }
            else
            {
                RegisterArrowHotkeys();
                EnableClickThrough();
            }
        }

        private void FlipBoard()
        {
            isFlipped = !isFlipped;
            UpdateChessBoard(currentFen, currentBestMove);
        }

        private void ToggleVisibility()
        {
            if (Visibility == Visibility.Visible)
            {
                Visibility = Visibility.Hidden;
            }
            else
            {
                Visibility = Visibility.Visible;
                Activate();
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
                        if (square.Child is TextBlock tb)
                        {
                            tb.Text = "";
                        }
                    }
                }

                foreach (var (row, col, piece) in piecePositions)
                {
                    if (squareCache[row, col].Child is TextBlock tb)
                    {
                        tb.Text = piece;
                    }
                }

                var highlightBrush = new SolidColorBrush(Color.FromRgb(255, 255, 100));
                foreach (var (row, col) in highlights)
                {
                    squareCache[row, col].Background = highlightBrush;
                }
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
            Dispatcher.Invoke(() =>
            {
                ExplanationBorder.Visibility = Visibility.Collapsed;
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
                Debug.WriteLine("Bot listener started on http://localhost:30012/");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start listener: {ex.Message}");
                return;
            }

            int requestCount = 0;

            while (true)
            {
                try
                {
                    var ctx = await listener.GetContextAsync();
                    requestCount++;

                    if (ctx.Request.HttpMethod == "OPTIONS")
                    {
                        ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
                        ctx.Response.AddHeader("Access-Control-Allow-Methods", "POST, OPTIONS");
                        ctx.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
                        ctx.Response.StatusCode = 200;
                        ctx.Response.OutputStream.Close();
                        continue;
                    }

                    if (ctx.Request.HttpMethod == HttpMethod.Post.Method)
                    {
                        ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");

                        // Check if API connection is disabled
                        if (!isApiConnectionEnabled)
                        {
                            Debug.WriteLine("⚠️ API connection disabled - ignoring request");
                            ctx.Response.StatusCode = 200;
                            ctx.Response.OutputStream.Close();
                            continue;
                        }

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
                        catch
                        {
                            ctx.Response.StatusCode = 400;
                            ctx.Response.OutputStream.Close();
                            continue;
                        }

                        try
                        {
                            var position = jss.GetProperty("position").GetString()!;
                            if (position == lastPgn)
                            {
                                ctx.Response.StatusCode = 200;
                                ctx.Response.OutputStream.Close();
                                continue;
                            }

                            lastPgn = position;
                            string fen = ConvertPgnToFen(position);

                            var apiBoard = ChessChallenge.API.Board.CreateBoardFromFEN(fen);
                            var timer = new ChessChallenge.API.Timer(10000, 10000, 1000, 0);

                            Debug.WriteLine($"\n╔═══════════════════════════════════════════════════════════╗");
                            Debug.WriteLine($"║ 🎯 MOVE #{requestCount}");
                            Debug.WriteLine($"╚═══════════════════════════════════════════════════════════╝");
                            Debug.WriteLine($"Position: {fen}");
                            Debug.WriteLine($"Turn: {(apiBoard.IsWhiteToMove ? "White" : "Black")}\n");

                            // Calculate eval BEFORE our move (this is AFTER opponent's move)
                            double evalAfterOpponent = EvaluatePositionScore(apiBoard);

                            // Calculate opponent's move quality
                            double opponentMoveSwing = 0.0;
                            bool opponentBlundered = false;

                            if (!string.IsNullOrEmpty(lastFenBeforeOpponent))
                            {
                                opponentMoveSwing = evalAfterOpponent - evalBeforeOpponentMove;
                                opponentBlundered = opponentMoveSwing > 1.0;

                                Debug.WriteLine($"Eval before opponent: {evalBeforeOpponentMove:+0.00;-0.00}");
                                Debug.WriteLine($"Eval after opponent: {evalAfterOpponent:+0.00;-0.00}");
                                Debug.WriteLine($"Opponent move quality: {opponentMoveSwing:+0.00;-0.00}");
                                if (opponentBlundered)
                                {
                                    Debug.WriteLine($"⚠️ OPPONENT BLUNDERED! (+{opponentMoveSwing:F2})");
                                }
                            }

                            // Let bot think
                            var startTime = DateTime.Now;
                            var move = bot.Think(apiBoard, timer);
                            var thinkTime = (DateTime.Now - startTime).TotalMilliseconds;

                            if (move.IsNull)
                            {
                                Debug.WriteLine("⚠️ No legal moves (checkmate or stalemate)\n");
                                UpdateChessBoard(fen, "");
                                HideExplanationBox();
                                ctx.Response.StatusCode = 200;
                                ctx.Response.OutputStream.Close();
                                continue;
                            }

                            // Get move UCI
                            string bestMoveUCI = $"{move.StartSquare.Name}{move.TargetSquare.Name}";
                            if (move.IsPromotion)
                            {
                                bestMoveUCI += move.PromotionPieceType.ToString()[0].ToString().ToLower();
                            }

                            Debug.WriteLine($"✅ CHOSEN MOVE: {bestMoveUCI}");
                            Debug.WriteLine($"   Think time: {thinkTime:F0}ms");
                            if (move.IsCastles) Debug.WriteLine("   ♚ Castling move");
                            if (move.IsCapture) Debug.WriteLine($"   ⚔️ Captures {move.CapturePieceType}");
                            if (move.IsPromotion) Debug.WriteLine($"   👑 Promotes to {move.PromotionPieceType}");
                            Debug.WriteLine("");

                            // Build move info string
                            string moveInfo = $"Move: {bestMoveUCI} | {thinkTime:F0}ms";
                            if (move.IsCastles) moveInfo += " | ♚";
                            if (move.IsCapture) moveInfo += $" | ⚔️{move.CapturePieceType}";

                            // === AI EXPLANATION (only if both API and chatbot enabled) ===
                            if (isApiConnectionEnabled && isChatbotEnabled)
                            {
                                Debug.WriteLine("🤖 AI EXPLANATION:");
                                Debug.WriteLine("─────────────────────────────────────────────────────────");

                                try
                                {
                                    string explanation;

                                    if (opponentBlundered)
                                    {
                                        explanation = await groqHelper.ExplainBlunderAsync(
                                            apiBoard,
                                            move,
                                            bestMoveUCI,
                                            opponentMoveSwing
                                        );

                                        moveInfo = $"🚨 BLUNDER! +{opponentMoveSwing:F1} | {moveInfo}";
                                    }
                                    else
                                    {
                                        explanation = await groqHelper.ExplainMoveShortAsync(
                                            apiBoard,
                                            move,
                                            bestMoveUCI
                                        );
                                    }

                                    Debug.WriteLine(explanation);

                                    // Update GUI
                                    UpdateExplanationBox(moveInfo, explanation);
                                }
                                catch (Exception aiEx)
                                {
                                    Debug.WriteLine($"⚠️ AI error: {aiEx.Message}");
                                    if (opponentBlundered)
                                    {
                                        UpdateExplanationBox(moveInfo, $"Opponent blundered! Capitalize on +{opponentMoveSwing:F1} advantage.");
                                    }
                                    else
                                    {
                                        UpdateExplanationBox(moveInfo, "Move executed.");
                                    }
                                }
                            }
                            else
                            {
                                // Show basic info without AI explanation
                                if (isChatbotEnabled)
                                {
                                    string basicInfo = opponentBlundered
                                        ? $"Opponent made a mistake! Take advantage of the position."
                                        : "Move executed successfully.";
                                    UpdateExplanationBox(moveInfo, basicInfo);
                                }
                                else
                                {
                                    HideExplanationBox();
                                }

                                Debug.WriteLine(isApiConnectionEnabled
                                    ? "AI explanations disabled in settings."
                                    : "API connection disabled in settings.");
                            }

                            Debug.WriteLine("═══════════════════════════════════════════════════════════\n");

                            UpdateChessBoard(fen, bestMoveUCI);

                            // Store eval AFTER our move for next comparison
                            var boardAfterOurMove = ChessChallenge.API.Board.CreateBoardFromFEN(fen);
                            boardAfterOurMove.MakeMove(move);
                            evalBeforeOpponentMove = EvaluatePositionScore(boardAfterOurMove);
                            lastFenBeforeOpponent = boardAfterOurMove.GetFenString();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"❌ Error: {ex.Message}");
                            Debug.WriteLine($"Stack: {ex.StackTrace}");
                        }

                        ctx.Response.StatusCode = 200;
                        ctx.Response.OutputStream.Close();
                    }
                }
                catch (Exception ex)
                {
                    if (!listener.IsListening) break;
                }
            }
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
