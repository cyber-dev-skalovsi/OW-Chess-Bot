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
        // State variables for board orientation and current position
        private bool isFlipped = false;
        private string currentFen = FenUtility.StartPositionFEN;
        private string currentBestMove = "";
        private string lastPgn = "";
        private Dictionary<string, string> pieceUnicode = new Dictionary<string, string>
        {
            {"wK", "♔"}, {"wQ", "♕"}, {"wR", "♖"}, {"wB", "♗"}, {"wN", "♘"}, {"wP", "♙"},
            {"bK", "♚"}, {"bQ", "♛"}, {"bR", "♜"}, {"bB", "♝"}, {"bN", "♞"}, {"bP", "♟"}
        };
        // For global hotkeys
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        private const int HOTKEY_ID = 9000; // Ctrl+Q
        private const int HOTKEY_LEFT = 9001;
        private const int HOTKEY_UP = 9002;
        private const int HOTKEY_RIGHT = 9003;
        private const int HOTKEY_DOWN = 9004;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_Q = 0x51;
        private const uint VK_LEFT = 0x25;
        private const uint VK_UP = 0x26;
        private const uint VK_RIGHT = 0x27;
        private const uint VK_DOWN = 0x28;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int HOTKEY_FLIP = 9005;
        private const uint VK_Y = 0x59;
        private IntPtr _hwnd;
        private bool _isClickThrough = true; // Start with click-through enabled
        private bool _isSemiTransparent = true; // Start semi-transparent
        private const double MOVE_STEP = 10.0;
        public MainWindow()
        {
            InitializeComponent();
            // Stealth: Hide window title to avoid showing "Chess Analysis" or any descriptive text
            this.Title = "";
            ShowInTaskbar = false;
            Width = 200;
            Height = 200;
            Top = 45;
            Left = SystemParameters.WorkArea.Right - Width + 8;
            Topmost = true;
            Opacity = 0.5;
            // Make window draggable (only when not click-through)
            MouseLeftButtonDown += (s, e) =>
            {
                if (!_isClickThrough)
                    DragMove();
            };
            // Add keyboard shortcuts
            KeyDown += MainWindow_KeyDown;
            // Register global hotkey
            Loaded += (s, e) =>
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                RegisterHotKey(_hwnd, HOTKEY_ID, MOD_CONTROL, VK_Q);
                // Register global arrow keys (no modifier)
                RegisterHotKey(_hwnd, HOTKEY_LEFT, 0, VK_LEFT);
                RegisterHotKey(_hwnd, HOTKEY_UP, 0, VK_UP);
                RegisterHotKey(_hwnd, HOTKEY_RIGHT, 0, VK_RIGHT);
                RegisterHotKey(_hwnd, HOTKEY_DOWN, 0, VK_DOWN);
                RegisterHotKey(_hwnd, HOTKEY_FLIP, MOD_CONTROL, VK_Y); // ADD THIS LINE
                HwndSource.FromHwnd(_hwnd).AddHook(HwndHook);
                // Enable click-through by default
                EnableClickThrough();
            };
            Closing += (s, e) =>
            {
                UnregisterHotKey(_hwnd, HOTKEY_ID);
                UnregisterHotKey(_hwnd, HOTKEY_LEFT);
                UnregisterHotKey(_hwnd, HOTKEY_UP);
                UnregisterHotKey(_hwnd, HOTKEY_RIGHT);
                UnregisterHotKey(_hwnd, HOTKEY_DOWN);
                UnregisterHotKey(_hwnd, HOTKEY_FLIP); // ADD THIS LINE
            };
            Debug.WriteLine("========================================");
            Debug.WriteLine("🤖 Chess Analysis GUI Starting");
            Debug.WriteLine("========================================");
            bot = new MyBot();
            bot.SetMaxDepth(10);
            Debug.WriteLine("✅ Bot initialized (depth: 10)");
            InitializeChessBoard();
            UpdateChessBoard(currentFen); // Show initial board state
            Task.Run(RunListener);
        }
        // Global hotkey hook
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                switch (hotkeyId)
                {
                    case HOTKEY_ID:
                        ToggleVisibility();
                        break;
                    case HOTKEY_LEFT:
                        Left -= MOVE_STEP;
                        break;
                    case HOTKEY_UP:
                        Top -= MOVE_STEP;
                        break;
                    case HOTKEY_RIGHT:
                        Left += MOVE_STEP;
                        break;
                    case HOTKEY_DOWN:
                        Top += MOVE_STEP;
                        break;
                    case HOTKEY_FLIP: // ADD THIS CASE
                        FlipBoard();
                        break;
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void FlipBoard()
        {
            Dispatcher.Invoke(() =>
            {
                isFlipped = !isFlipped;
                Debug.WriteLine($"🔄 Board Flipped (Ctrl+Y): {isFlipped}");
                // Re-draw the board using the last known FEN and best move
                UpdateChessBoard(currentFen, currentBestMove);
            });
        }
        private void ToggleVisibility()
        {
            if (Visibility == Visibility.Visible)
            {
                Visibility = Visibility.Hidden;
                Debug.WriteLine("🔽 GUI hidden (Ctrl+Q)");
            }
            else
            {
                Visibility = Visibility.Visible;
                Activate(); // Bring to front
                Debug.WriteLine("🔼 GUI visible (Ctrl+Q)");
            }
        }
        private void EnableClickThrough()
        {
            var exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            var layeredExStyle = exStyle | WS_EX_LAYERED;
            SetWindowLong(_hwnd, GWL_EXSTYLE, layeredExStyle | WS_EX_TRANSPARENT);
            _isClickThrough = true;
            Debug.WriteLine("Window click-through enabled (clicks pass through)");
        }
        private void DisableClickThrough()
        {
            var exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
            _isClickThrough = false;
            Debug.WriteLine("Window click-through disabled (normal interaction)");
        }
        private void ToggleClickThrough()
        {
            if (_isClickThrough)
            {
                DisableClickThrough();
            }
            else
            {
                EnableClickThrough();
            }
        }
        // --- Keyboard Shortcuts ---
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+O to toggle semi-transparency (opacity 0.5 / 1.0)
            if (e.Key == Key.O && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                _isSemiTransparent = !_isSemiTransparent;
                Opacity = _isSemiTransparent ? 0.5 : 1.0;
                Debug.WriteLine($"Opacity toggled to: {Opacity}");
                e.Handled = true;
            }
            // Ctrl+P to toggle click-through
            else if (e.Key == Key.P && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ToggleClickThrough();
                e.Handled = true;
            }
            // Ctrl+Q to toggle visibility (when window is focused)
            else if (e.Key == Key.Q && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ToggleVisibility();
                e.Handled = true;
            }
            // Ctrl+W to close
            else if (e.Key == Key.W && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                Debug.WriteLine("👋 Closing GUI (Ctrl+W)");
                Application.Current.Shutdown();
                e.Handled = true;
            }
            // F key to flip board
            else if (e.Key == Key.F)
            {
                isFlipped = !isFlipped;
                Debug.WriteLine($"🔄 Board Flipped: {isFlipped}");
                // Re-draw the board using the last known FEN and best move
                UpdateChessBoard(currentFen, currentBestMove);
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
                        Name = $"Square_{row}_{col}",
                        // Background color logic remains the same (alternating rows/cols)
                        Background = (row + col) % 2 == 0
                            ? new SolidColorBrush(Color.FromRgb(240, 217, 181))
                            : new SolidColorBrush(Color.FromRgb(181, 136, 99))
                    };
                    var textBlock = new TextBlock
                    {
                        FontSize = 48, // Larger font for bigger pieces
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = Brushes.Black
                    };
                    square.Child = textBlock;
                    Grid.SetRow(square, row);
                    Grid.SetColumn(square, col);
                    ChessBoard.Children.Add(square);
                }
            }
            Debug.WriteLine("✅ Chess board initialized");
        }
        private void UpdateChessBoard(string fen, string? bestMove = null)
        {
            // Update state variables
            currentFen = fen;
            currentBestMove = bestMove ?? "";
            Dispatcher.Invoke(() =>
            {
                string[] fenParts = fen.Split(' ');
                string position = fenParts[0];
                string[] ranks = position.Split('/');
                // 1. Reset Board Colors and Pieces
                foreach (var child in ChessBoard.Children)
                {
                    if (child is Border border)
                    {
                        int row = Grid.GetRow(border);
                        int col = Grid.GetColumn(border);
                        // Reset background color
                        border.Background = (row + col) % 2 == 0
                            ? new SolidColorBrush(Color.FromRgb(240, 217, 181))
                            : new SolidColorBrush(Color.FromRgb(181, 136, 99));
                        // Clear piece text
                        if (border.Child is TextBlock tb)
                        {
                            tb.Text = "";
                        }
                    }
                }
                // 2. Place Pieces based on FEN and Flipping state
                for (int rank = 0; rank < 8; rank++) // Iterates FEN ranks (8 to 1)
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
                                // Convert FEN rank/file to the actual WPF grid display coordinates
                                var (displayRow, displayCol) = GetDisplayCoords(rank, file);
                                var square = GetSquare(displayRow, displayCol);
                                if (square?.Child is TextBlock tb)
                                {
                                    tb.Text = pieceUnicode[pieceKey];
                                }
                            }
                            file++;
                        }
                    }
                }
                // 3. Highlight Best Move
                if (!string.IsNullOrEmpty(currentBestMove) && currentBestMove.Length >= 4)
                {
                    string from = currentBestMove.Substring(0, 2);
                    string to = currentBestMove.Substring(2, 2);
                    var (fromRow, fromCol) = SquareNameToDisplayCoords(from);
                    var (toRow, toCol) = SquareNameToDisplayCoords(to);
                    var fromSquare = GetSquare(fromRow, fromCol);
                    var toSquare = GetSquare(toRow, toCol);
                    if (fromSquare != null)
                    {
                        fromSquare.Background = new SolidColorBrush(Color.FromRgb(255, 255, 100));
                    }
                    if (toSquare != null)
                    {
                        toSquare.Background = new SolidColorBrush(Color.FromRgb(255, 255, 100));
                    }
                }
            });
        }
        // --- Coordinate Helpers with Flipping Logic ---
        /// <summary>
        /// Converts FEN rank index (0-7, where 0 is Rank 8) and file index (0-7, where 0 is 'a')
        /// to the WPF Grid row/col based on the current 'isFlipped' state.
        /// </summary>
        private (int row, int col) GetDisplayCoords(int rank, int file)
        {
            // If not flipped (White view): FEN rank 0 -> WPF row 0. FEN file 0 -> WPF col 0.
            // If flipped (Black view): FEN rank 0 -> WPF row 7. FEN file 0 -> WPF col 7.
            int displayRow = isFlipped ? 7 - rank : rank;
            int displayCol = isFlipped ? 7 - file : file;
            return (displayRow, displayCol);
        }
        /// <summary>
        /// Converts chess square notation (e.g., "e2") to the WPF Grid row/col
        /// based on the current 'isFlipped' state.
        /// </summary>
        private (int row, int col) SquareNameToDisplayCoords(string square)
        {
            // Convert 'a1' to internal rank/file indices (0-7, 0-7)
            int file = square[0] - 'a'; // 'a' is 0, 'h' is 7
            int rank = 8 - (square[1] - '0'); // '8' is 0, '1' is 7
            // Apply flipping logic to get display coordinates
            return GetDisplayCoords(rank, file);
        }
        // --- Listener and PGN/FEN Conversion ---
        private Border? GetSquare(int row, int col)
        {
            foreach (var child in ChessBoard.Children)
            {
                if (child is Border border && Grid.GetRow(border) == row && Grid.GetColumn(border) == col)
                {
                    return border;
                }
            }
            return null;
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
                        Debug.WriteLine($" 📄 Request body length: {requestBody.Length}");
                        JsonElement jss;
                        try
                        {
                            jss = JsonSerializer.Deserialize<JsonElement>(requestBody);
                            Debug.WriteLine(" ✓ JSON parsed successfully");
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
                            Debug.WriteLine(" 🔍 Extracting position from JSON...");
                            var position = jss.GetProperty("position").GetString()!;
                            Debug.WriteLine($" ✓ Position extracted: {position.Substring(0, Math.Min(50, position.Length))}...");
                            // Check if this is the same position as last time
                            if (position == lastPgn)
                            {
                                Debug.WriteLine(" ⏭️ Same position as before, skipping analysis");
                                ctx.Response.StatusCode = 200;
                                ctx.Response.OutputStream.Close();
                                continue;
                            }
                            lastPgn = position;
                            Debug.WriteLine($" ♟️ New position detected ({position.Length} chars)");
                            Debug.WriteLine($" 🔄 Converting PGN to FEN...");
                            string fen = ConvertPgnToFen(position);
                            Debug.WriteLine($" ✓ FEN conversion complete: {fen.Substring(0, Math.Min(60, fen.Length))}...");
                            var startTime = DateTime.Now;
                            Debug.WriteLine(" 🧠 Creating board and loading position...");
                            var board = new Board();
                            board.LoadPosition(fen);
                            Debug.WriteLine(" ✓ Board loaded");
                            Debug.WriteLine(" 🤔 Bot thinking...");
                            var move = bot.Think(
                                new ChessChallenge.API.Board(board),
                                board.IsWhiteToMove
                                    ? new ChessChallenge.API.Timer(10000, 10000, 1000, 0)
                                    : new ChessChallenge.API.Timer(10000, 10000, 1000, 0)
                            );
                            var thinkTime = (DateTime.Now - startTime).TotalMilliseconds;
                            if (move.RawValue == 0)
                            {
                                Debug.WriteLine(" ⚠️ Bot returned NULL move (RawValue = 0)!");
                                Debug.WriteLine($" ⚠️ Board state - White to move: {board.IsWhiteToMove}");
                                UpdateChessBoard(fen, "");
                            }
                            else
                            {
                                var bestMove = MoveUtility.GetMoveNameUCI(new Move(move.RawValue));
                                Debug.WriteLine($" ✅ Best move found: {bestMove} ({thinkTime:F0}ms)");
                                UpdateChessBoard(fen, bestMove);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($" ❌ ERROR in processing: {ex.Message}");
                            Debug.WriteLine($" 📍 Stack trace: {ex.StackTrace}");
                            Debug.WriteLine($" 📍 Inner exception: {ex.InnerException?.Message ?? "none"}");
                            // Reset to starting position on error
                            currentFen = FenUtility.StartPositionFEN;
                            UpdateChessBoard(currentFen, "");
                        }
                        ctx.Response.StatusCode = 200;
                        ctx.Response.OutputStream.Close();
                    }
                    ctx.Response.OutputStream.Close();
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
            Debug.WriteLine(" 🔧 ConvertPgnToFen START");
            Debug.WriteLine($" 📥 Input PGN length: {pgn.Length}");
            var lines = pgn.Split('\n');
            Debug.WriteLine($" 📋 Split into {lines.Length} lines");
            string movesText = "";
            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("[")) continue;
                if (string.IsNullOrWhiteSpace(line)) continue;
                movesText += " " + line.Trim();
            }
            Debug.WriteLine($" 📝 Raw moves text: {movesText}");
            // Clean up PGN string
            movesText = Regex.Replace(movesText, @"\d+\.", " ");
            movesText = Regex.Replace(movesText, @"[+#]", "");
            movesText = Regex.Replace(movesText, @"\s*(1-0|0-1|1/2-1/2|\*)\s*$", "");
            // Normalize promotion notation: h8=Q -> h8Q (handle both upper and lowercase)
            movesText = Regex.Replace(movesText, @"=([QRBNqrbn])", "$1");
            movesText = Regex.Replace(movesText, @"\s+", " ");
            movesText = movesText.Trim();
            Debug.WriteLine($" 🧹 Cleaned moves: {movesText}");
            var board = new Board();
            board.LoadPosition(FenUtility.StartPositionFEN);
            Debug.WriteLine(" ✓ Starting position loaded");
            if (!string.IsNullOrWhiteSpace(movesText))
            {
                var moves = movesText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Debug.WriteLine($" 🎯 Processing {moves.Length} moves...");
                int moveNum = 0;
                foreach (var moveStr in moves)
                {
                    moveNum++;
                    if (string.IsNullOrWhiteSpace(moveStr)) continue;
                    string processedMove = moveStr;
                    // Handle promotion moves: h8Q needs to become h7h8Q (or a7a8Q, etc.)
                    // Pattern: single file letter + rank 8/1 + promotion piece
                    var promotionMatch = Regex.Match(moveStr, @"^([a-h])(8|1)([QRBNqrbn])$");
                    if (promotionMatch.Success)
                    {
                        string file = promotionMatch.Groups[1].Value;
                        string targetRank = promotionMatch.Groups[2].Value;
                        string piece = promotionMatch.Groups[3].Value;
                        // Determine starting rank (7 for promotion to 8, 2 for promotion to 1)
                        string startRank = targetRank == "8" ? "7" : "2";
                        processedMove = $"{file}{startRank}{file}{targetRank}{piece}";
                        Debug.WriteLine($" 🔄 Expanded promotion: '{moveStr}' -> '{processedMove}'");
                    }
                    Debug.WriteLine($" #{moveNum}: Attempting '{processedMove}'");
                    try
                    {
                        bool success = board.TryMakeMoveFromSan(processedMove, out Move move);
                        if (!success)
                        {
                            Debug.WriteLine($" ❌ FAILED to parse move '{processedMove}'");
                            Debug.WriteLine($" 📍 Current FEN: {FenUtility.CurrentFen(board)}");
                        }
                        else
                        {
                            Debug.WriteLine($" ✓ Success");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($" ❌ EXCEPTION on move '{processedMove}': {ex.Message}");
                    }
                }
            }
            string finalFen = FenUtility.CurrentFen(board);
            Debug.WriteLine($" 📤 Final FEN: {finalFen}");
            Debug.WriteLine(" 🔧 ConvertPgnToFen END");
            return finalFen;
        }
    }
}