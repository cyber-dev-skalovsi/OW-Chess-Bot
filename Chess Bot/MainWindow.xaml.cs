using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ChessBot
{
    public partial class MainWindow : Window
    {
        private string[,] board = new string[8, 8];
        private TextBlock selectedPiece = null;
        private int selectedRow = -1;
        private int selectedCol = -1;
        private bool isWhiteTurn = true;
        private Stack<GameState> moveHistory = new Stack<GameState>();

        private class GameState
        {
            public string[,] Board { get; set; }
            public bool IsWhiteTurn { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();

            InitializeBoard();
            DrawBoard();
            PlacePieces();
            UpdateTurnLabel();
        }

        private void InitializeBoard()
        {
            board[0, 0] = "br"; board[0, 1] = "bn"; board[0, 2] = "bb"; board[0, 3] = "bq";
            board[0, 4] = "bk"; board[0, 5] = "bb"; board[0, 6] = "bn"; board[0, 7] = "br";
            for (int i = 0; i < 8; i++) board[1, i] = "bp";

            for (int row = 2; row < 6; row++)
                for (int col = 0; col < 8; col++)
                    board[row, col] = "";

            for (int i = 0; i < 8; i++) board[6, i] = "wp";
            board[7, 0] = "wr"; board[7, 1] = "wn"; board[7, 2] = "wb"; board[7, 3] = "wq";
            board[7, 4] = "wk"; board[7, 5] = "wb"; board[7, 6] = "wn"; board[7, 7] = "wr";
        }

        private void DrawBoard()
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    var square = new Border
                    {
                        Width = 75,
                        Height = 75,
                        Background = new SolidColorBrush((row + col) % 2 == 0 ?
                            Color.FromRgb(240, 217, 181) : Color.FromRgb(181, 136, 99)),
                        Tag = $"{row},{col}"
                    };

                    Grid.SetRow(square, row);
                    Grid.SetColumn(square, col);

                    square.MouseLeftButtonDown += Square_Click;
                    ChessBoard.Children.Add(square);
                }
            }
        }

        private void PlacePieces()
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    if (!string.IsNullOrEmpty(board[row, col]))
                    {
                        UpdateSquare(row, col);
                    }
                }
            }
        }

        private void UpdateSquare(int row, int col)
        {
            int index = row * 8 + col;

            if (index >= ChessBoard.Children.Count) return;
            var border = ChessBoard.Children[index] as Border;
            if (border == null) return;
            border.Child = null;

            if (!string.IsNullOrEmpty(board[row, col]))
            {
                var textBlock = new TextBlock
                {
                    Text = GetChessPieceUnicode(board[row, col]),
                    FontSize = 48,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.Black
                };
                border.Child = textBlock;
            }
        }

        private string GetChessPieceUnicode(string piece)
        {
            var symbols = new Dictionary<string, string>
            {
                {"wk", "♔"}, {"wq", "♕"}, {"wr", "♖"},
                {"wb", "♗"}, {"wn", "♘"}, {"wp", "♙"},
                {"bk", "♚"}, {"bq", "♛"}, {"br", "♜"},
                {"bb", "♝"}, {"bn", "♞"}, {"bp", "♟"}
            };

            return symbols.ContainsKey(piece) ? symbols[piece] : "?";
        }

        private void Square_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border == null) return;

            var coords = border.Tag.ToString().Split(',');
            int row = int.Parse(coords[0]);
            int col = int.Parse(coords[1]);

            if (selectedPiece == null)
            {
                if (!string.IsNullOrEmpty(board[row, col]))
                {
                    char pieceColor = board[row, col][0];
                    if ((isWhiteTurn && pieceColor == 'w') || (!isWhiteTurn && pieceColor == 'b'))
                    {
                        ClearSelectionBorders();

                        selectedRow = row;
                        selectedCol = col;
                        selectedPiece = border.Child as TextBlock;
                        border.BorderBrush = Brushes.Yellow;
                        border.BorderThickness = new Thickness(3);
                    }
                }
            }
            else
            {
                if (selectedRow == row && selectedCol == col)
                {
                    ClearSelectionBorders();
                }
                else if (IsValidMove(selectedRow, selectedCol, row, col))
                {
                    // Save game state for undo
                    SaveGameState();

                    string capturedPiece = board[row, col];

                    board[row, col] = board[selectedRow, selectedCol];
                    board[selectedRow, selectedCol] = "";

                    UpdateSquare(selectedRow, selectedCol);
                    UpdateSquare(row, col);

                    ClearSelectionBorders();

                    isWhiteTurn = !isWhiteTurn;
                    UpdateTurnLabel();
                }

                selectedPiece = null;
                selectedRow = -1;
                selectedCol = -1;
            }
        }

        private void SaveGameState()
        {
            var state = new GameState
            {
                Board = (string[,])board.Clone(),
                IsWhiteTurn = isWhiteTurn
            };
            moveHistory.Push(state);
        }

        private void ClearSelectionBorders()
        {
            for (int i = 0; i < ChessBoard.Children.Count; i++)
            {
                if (ChessBoard.Children[i] is Border b)
                {
                    b.BorderThickness = new Thickness(0);
                    b.BorderBrush = Brushes.Transparent;
                }
            }
        }

        private void UpdateTurnLabel()
        {
            if (TurnLabel != null)
            {
                TurnLabel.Text = isWhiteTurn ? "White to move" : "Black to move";
            }
        }

        private void NewGame_Click(object sender, RoutedEventArgs e)
        {
            moveHistory.Clear();
            isWhiteTurn = true;
            InitializeBoard();

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    UpdateSquare(row, col);
                }
            }

            ClearSelectionBorders();
            UpdateTurnLabel();

            if (StatusLabel != null)
            {
                StatusLabel.Text = "";
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (moveHistory.Count > 0)
            {
                var previousState = moveHistory.Pop();
                board = previousState.Board;
                isWhiteTurn = previousState.IsWhiteTurn;

                for (int row = 0; row < 8; row++)
                {
                    for (int col = 0; col < 8; col++)
                    {
                        UpdateSquare(row, col);
                    }
                }

                ClearSelectionBorders();
                UpdateTurnLabel();
            }
        }

        private bool IsValidMove(int fromRow, int fromCol, int toRow, int toCol)
        {
            if (fromRow == toRow && fromCol == toCol) return false;
            if (toRow < 0 || toRow > 7 || toCol < 0 || toCol > 7) return false;

            string piece = board[fromRow, fromCol];
            string targetPiece = board[toRow, toCol];

            if (!string.IsNullOrEmpty(targetPiece) && targetPiece[0] == piece[0])
                return false;

            char pieceType = piece[1];
            char pieceColor = piece[0];

            switch (pieceType)
            {
                case 'p':
                    return IsValidPawnMove(fromRow, fromCol, toRow, toCol, pieceColor);
                case 'r':
                    return IsValidRookMove(fromRow, fromCol, toRow, toCol);
                case 'n':
                    return IsValidKnightMove(fromRow, fromCol, toRow, toCol);
                case 'b':
                    return IsValidBishopMove(fromRow, fromCol, toRow, toCol);
                case 'q':
                    return IsValidQueenMove(fromRow, fromCol, toRow, toCol);
                case 'k':
                    return IsValidKingMove(fromRow, fromCol, toRow, toCol);
                default:
                    return false;
            }
        }

        private bool IsValidPawnMove(int fromRow, int fromCol, int toRow, int toCol, char color)
        {
            int direction = color == 'w' ? -1 : 1;
            int startRow = color == 'w' ? 6 : 1;

            if (toCol == fromCol && toRow == fromRow + direction && string.IsNullOrEmpty(board[toRow, toCol]))
                return true;

            if (fromRow == startRow && toCol == fromCol && toRow == fromRow + 2 * direction &&
                string.IsNullOrEmpty(board[toRow, toCol]) && string.IsNullOrEmpty(board[fromRow + direction, toCol]))
                return true;

            if (Math.Abs(toCol - fromCol) == 1 && toRow == fromRow + direction && !string.IsNullOrEmpty(board[toRow, toCol]))
                return true;

            return false;
        }

        private bool IsValidRookMove(int fromRow, int fromCol, int toRow, int toCol)
        {
            if (fromRow != toRow && fromCol != toCol) return false;
            return IsPathClear(fromRow, fromCol, toRow, toCol);
        }

        private bool IsValidKnightMove(int fromRow, int fromCol, int toRow, int toCol)
        {
            int rowDiff = Math.Abs(toRow - fromRow);
            int colDiff = Math.Abs(toCol - fromCol);
            return (rowDiff == 2 && colDiff == 1) || (rowDiff == 1 && colDiff == 2);
        }

        private bool IsValidBishopMove(int fromRow, int fromCol, int toRow, int toCol)
        {
            if (Math.Abs(toRow - fromRow) != Math.Abs(toCol - fromCol)) return false;
            return IsPathClear(fromRow, fromCol, toRow, toCol);
        }

        private bool IsValidQueenMove(int fromRow, int fromCol, int toRow, int toCol)
        {
            return IsValidRookMove(fromRow, fromCol, toRow, toCol) ||
                       IsValidBishopMove(fromRow, fromCol, toRow, toCol);
        }

        private bool IsValidKingMove(int fromRow, int fromCol, int toRow, int toCol)
        {
            return Math.Abs(toRow - fromRow) <= 1 && Math.Abs(toCol - fromCol) <= 1;
        }

        private bool IsPathClear(int fromRow, int fromCol, int toRow, int toCol)
        {
            int rowDir = Math.Sign(toRow - fromRow);
            int colDir = Math.Sign(toCol - fromCol);
            int row = fromRow + rowDir;
            int col = fromCol + colDir;

            while (row != toRow || col != toCol)
            {
                if (!string.IsNullOrEmpty(board[row, col]))
                    return false;
                row += rowDir;
                col += colDir;
            }
            return true;
        }
    }
}