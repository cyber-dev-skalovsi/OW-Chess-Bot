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
        private bool isCompactMode = false;
        private bool isPinLocked = false;
        private int pinAttempts = 0;
        private const string PIN_FILE = "lock.dat";
        private const int MAX_PIN_ATTEMPTS = 3;

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
        private const int HOTKEY_PIN_LOCK = 9008;
        private const int HOTKEY_OPTIONS = 9009;
        private const int HOTKEY_COMPACT = 9010;

        // Modifiers
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_CTRL_SHIFT = MOD_CONTROL | MOD_SHIFT;

        // Virtual Keys
        private const uint VK_H = 0x48;
        private const uint VK_F = 0x46;
        private const uint VK_L = 0x4C;
        private const uint VK_X = 0x58;
        private const uint VK_P = 0x50;
        private const uint VK_O = 0x4F;
        private const uint VK_C = 0x43;
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

        // UI Elements for compact mode
        private Grid compactGrid;
        private TextBlock compactMoveText;

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
            bot.SetMaxDepth(10);
            Debug.WriteLine("✅ Bot initialized (depth: 10)");

            InitializeCompactMode();
            InitializeChessBoard();
            UpdateChessBoard(currentFen);

            Task.Run(RunListener);
        }

        private void InitializeCompactMode()
        {
            compactGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 20)),
                Visibility = Visibility.Collapsed
            };

            compactMoveText = new TextBlock
            {
                Text = "",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            compactGrid.Children.Add(compactMoveText);

            // FIX: Access the Border, then find the Viewbox, then the Grid inside
            if (this.Content is Border border && border.Child is Viewbox viewbox)
            {
                // Create a new Grid to hold both ChessBoard and compactGrid
                var containerGrid = new Grid();

                // Move ChessBoard into the container
                viewbox.Child = null; // Remove ChessBoard from Viewbox
                containerGrid.Children.Add(ChessBoard);
                containerGrid.Children.Add(compactGrid);

                // Put container back in Viewbox
                viewbox.Child = containerGrid;
            }
            else
            {
                Debug.WriteLine("❌ ERROR: Unexpected XAML structure!");
            }
        }

        private void RegisterAllHotkeys()
        {
            RegisterHotKey(_hwnd, HOTKEY_HIDE, MOD_CTRL_SHIFT, VK_H);
            RegisterHotKey(_hwnd, HOTKEY_FLIP, MOD_CTRL_SHIFT, VK_F);
            RegisterHotKey(_hwnd, HOTKEY_LOCK_POS, MOD_CTRL_SHIFT, VK_L);
            RegisterHotKey(_hwnd, HOTKEY_DESTRUCT, MOD_CTRL_SHIFT, VK_X);
            RegisterHotKey(_hwnd, HOTKEY_PIN_LOCK, MOD_CTRL_SHIFT, VK_P);
            RegisterHotKey(_hwnd, HOTKEY_OPTIONS, MOD_CTRL_SHIFT, VK_O);
            RegisterHotKey(_hwnd, HOTKEY_COMPACT, MOD_CTRL_SHIFT, VK_C);
            RegisterArrowHotkeys();
            Debug.WriteLine("✅ All hotkeys registered");
        }

        private void UnregisterAllHotkeys()
        {
            UnregisterHotKey(_hwnd, HOTKEY_HIDE);
            UnregisterHotKey(_hwnd, HOTKEY_FLIP);
            UnregisterHotKey(_hwnd, HOTKEY_LOCK_POS);
            UnregisterHotKey(_hwnd, HOTKEY_DESTRUCT);
            UnregisterHotKey(_hwnd, HOTKEY_PIN_LOCK);
            UnregisterHotKey(_hwnd, HOTKEY_OPTIONS);
            UnregisterHotKey(_hwnd, HOTKEY_COMPACT);
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
                    case HOTKEY_PIN_LOCK:
                        TogglePinLock();
                        break;
                    case HOTKEY_OPTIONS:
                        ShowOptionsMenu();
                        break;
                    case HOTKEY_COMPACT:
                        ToggleCompactMode();
                        break;
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void ToggleCompactMode()
        {
            isCompactMode = !isCompactMode;

            if (isCompactMode)
            {
                ChessBoard.Visibility = Visibility.Collapsed;
                compactGrid.Visibility = Visibility.Visible;

                // Store current size before switching
                savedSize = Width;

                // Set compact size
                Width = 80;
                Height = 50;

                UpdateCompactDisplay();
                Debug.WriteLine("📦 Compact mode enabled (Ctrl+Shift+C)");
            }
            else
            {
                compactGrid.Visibility = Visibility.Collapsed;
                ChessBoard.Visibility = Visibility.Visible;

                // Restore saved size
                Width = savedSize;
                Height = savedSize;

                Debug.WriteLine("📋 Board mode enabled (Ctrl+Shift+C)");
            }
            ClampPosition();
        }

        private void UpdateCompactDisplay()
        {
            if (!string.IsNullOrEmpty(currentBestMove) && currentBestMove.Length >= 4)
            {
                // Convert UCI to readable format (e.g., "e2e4" to "e4")
                string move = currentBestMove.Substring(2, 2);
                if (currentBestMove.Length > 4)
                {
                    move += currentBestMove.Substring(4); // Add promotion piece
                }
                compactMoveText.Text = move;
            }
            else
            {
                compactMoveText.Text = "...";
            }
        }

        private void TogglePinLock()
        {
            if (isPinLocked)
            {
                // Already locked, do nothing - unlock will happen via ToggleVisibility
                Debug.WriteLine("⚠️ Already PIN locked - use Ctrl+Shift+H to unlock");
            }
            else
            {
                // Lock - set PIN if needed, then hide
                if (!File.Exists(PIN_FILE))
                {
                    ShowPinDialog(true);
                }
                else
                {
                    isPinLocked = true;
                    DisableAllHotkeysExceptPin();
                    Visibility = Visibility.Hidden;
                    Debug.WriteLine("🔒 PIN locked and hidden");
                }
            }
        }

        private void ShowPinDialog(bool isSettingPin)
        {
            var pinWindow = new Window
            {
                Width = 320,
                Height = 200,
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
                Text = isSettingPin ? "🔐 Set PIN" : "🔓 Enter PIN",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var label = new TextBlock
            {
                Text = isSettingPin ? "Enter 4-digit PIN:" : $"Attempts remaining: {MAX_PIN_ATTEMPTS - pinAttempts}",
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var passwordBox = new PasswordBox
            {
                MaxLength = 4,
                FontSize = 18,
                Padding = new Thickness(10),
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1)
            };

            var button = new Button
            {
                Content = "OK",
                Margin = new Thickness(0, 20, 0, 0),
                Padding = new Thickness(40, 10, 40, 10),
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };

            button.MouseEnter += (s, e) => button.Background = new SolidColorBrush(Color.FromRgb(80, 80, 80));
            button.MouseLeave += (s, e) => button.Background = new SolidColorBrush(Color.FromRgb(60, 60, 60));

            button.Click += (s, e) =>
            {
                var pin = passwordBox.Password;
                if (pin.Length != 4 || !int.TryParse(pin, out _))
                {
                    label.Text = "❌ PIN must be 4 digits";
                    label.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
                    passwordBox.Clear();
                    return;
                }

                if (isSettingPin)
                {
                    File.WriteAllText(PIN_FILE, pin);
                    isPinLocked = true;
                    DisableAllHotkeysExceptPin();
                    Visibility = Visibility.Hidden;
                    pinWindow.Close();
                    Debug.WriteLine("🔒 PIN set, locked and hidden");
                }
                else
                {
                    var storedPin = File.ReadAllText(PIN_FILE);
                    if (pin == storedPin)
                    {
                        isPinLocked = false;
                        pinAttempts = 0;
                        RegisterAllHotkeys();
                        Visibility = Visibility.Visible;
                        Activate();
                        pinWindow.Close();
                        Debug.WriteLine("🔓 PIN unlocked and shown");
                    }
                    else
                    {
                        pinAttempts++;
                        if (pinAttempts >= MAX_PIN_ATTEMPTS)
                        {
                            pinWindow.Close();
                            SelfDestruct();
                        }
                        else
                        {
                            label.Text = $"❌ Wrong PIN! Attempts: {MAX_PIN_ATTEMPTS - pinAttempts}";
                            label.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
                            passwordBox.Clear();
                        }
                    }
                }
            };

            passwordBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };

            stack.Children.Add(title);
            stack.Children.Add(label);
            stack.Children.Add(passwordBox);
            stack.Children.Add(button);
            border.Child = stack;
            pinWindow.Content = border;

            passwordBox.Focus();
            pinWindow.ShowDialog();
        }

        private void DisableAllHotkeysExceptPin()
        {
            UnregisterHotKey(_hwnd, HOTKEY_HIDE);
            UnregisterHotKey(_hwnd, HOTKEY_FLIP);
            UnregisterHotKey(_hwnd, HOTKEY_LOCK_POS);
            UnregisterHotKey(_hwnd, HOTKEY_DESTRUCT);
            UnregisterHotKey(_hwnd, HOTKEY_OPTIONS);
            UnregisterHotKey(_hwnd, HOTKEY_COMPACT);
            UnregisterArrowHotkeys();
        }

        private void SelfDestruct()
        {
            Debug.WriteLine("💥 SELF DESTRUCT ACTIVATED");

            // Delete PIN file if exists
            if (File.Exists(PIN_FILE))
            {
                File.Delete(PIN_FILE);
            }

            // Close application
            Application.Current.Shutdown();
        }

        private void ShowOptionsMenu()
        {
            if (isPinLocked) return;

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
            // Size slider
            var sizeLabel = new TextBlock
            {
                Text = isCompactMode ? $"Compact Size: {Width:F0}" : $"Board Size: {Width:F0}",
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                Margin = new Thickness(0, 20, 0, 8),
                FontSize = 14
            };
            var sizeSlider = new Slider
            {
                Minimum = isCompactMode ? 40 : 100,
                Maximum = isCompactMode ? 200 : 400,
                Value = Width,
                TickFrequency = 10,
                IsSnapToTickEnabled = true,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
            };
            sizeSlider.ValueChanged += (s, e) =>
            {
                Width = e.NewValue;
                Height = e.NewValue;

                if (!isCompactMode)
                {
                    savedSize = e.NewValue;
                }

                ClampPosition();
                sizeLabel.Text = isCompactMode ? $"Compact Size: {e.NewValue:F0}" : $"Board Size: {e.NewValue:F0}";
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
            if (isPinLocked) return;

            isLocked = !isLocked;
            if (isLocked)
            {
                UnregisterArrowHotkeys();
                // Keep click-through enabled even when locked
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
            if (isPinLocked) return;

            Dispatcher.Invoke(() =>
            {
                isFlipped = !isFlipped;
                Debug.WriteLine($"🔄 Board Flipped (Ctrl+Shift+F): {isFlipped}");
                UpdateChessBoard(currentFen, currentBestMove);
            });
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
                // Trying to show - check if PIN locked
                if (isPinLocked)
                {
                    ShowPinDialog(false); // Ask for PIN to unlock
                }
                else
                {
                    Visibility = Visibility.Visible;
                    Activate();
                    Debug.WriteLine("🔼 GUI visible (Ctrl+Shift+H)");
                }
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
            if (isPinLocked) return;

            // Keep these for when window has focus
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
                        Name = $"Square_{row}_{col}",
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
                }
            }
            Debug.WriteLine("✅ Chess board initialized");
        }

        private void UpdateChessBoard(string fen, string? bestMove = null)
        {
            currentFen = fen;
            currentBestMove = bestMove ?? "";

            if (isCompactMode)
            {
                UpdateCompactDisplay();
                return;
            }

            Dispatcher.Invoke(() =>
            {
                string[] fenParts = fen.Split(' ');
                string position = fenParts[0];
                string[] ranks = position.Split('/');

                // Reset board
                foreach (var child in ChessBoard.Children)
                {
                    if (child is Border border)
                    {
                        int row = Grid.GetRow(border);
                        int col = Grid.GetColumn(border);
                        border.Background = (row + col) % 2 == 0
                            ? new SolidColorBrush(Color.FromRgb(240, 217, 181))
                            : new SolidColorBrush(Color.FromRgb(181, 136, 99));

                        if (border.Child is TextBlock tb)
                        {
                            tb.Text = "";
                        }
                    }
                }

                // Place pieces
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

                // Highlight best move
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

            movesText = Regex.Replace(movesText, @"\d+\.", " ");
            movesText = Regex.Replace(movesText, @"[+#]", "");
            movesText = Regex.Replace(movesText, @"\s*(1-0|0-1|1/2-1/2|\*)\s*$", "");
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

                    var promotionMatch = Regex.Match(moveStr, @"^([a-h])(8|1)([QRBNqrbn])$");
                    if (promotionMatch.Success)
                    {
                        string file = promotionMatch.Groups[1].Value;
                        string targetRank = promotionMatch.Groups[2].Value;
                        string piece = promotionMatch.Groups[3].Value;
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