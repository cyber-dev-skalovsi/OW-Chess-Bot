using System;
using System.Collections.Generic;
using System.Text.RegularExpressions; // Added for robustness, though not strictly needed in this file

namespace ChessChallenge.Chess
{
    // Represents the current state of the board during a game.
    // The state includes things such as: positions of all pieces, side to move,
    // castling rights, en-passant square, etc. Some extra information is included
    // as well to help with evaluation and move generation.

    // The initial state of the board can be set from a FEN string, and moves are
    // subsequently made (or undone) using the MakeMove and UnmakeMove functions.

    public sealed class Board
    {

        public ulong ZobristKey => currentGameState.zobristKey;
        public const int WhiteIndex = 0;
        public const int BlackIndex = 1;

        // Side to move info
        public bool IsWhiteToMove;
        public int MoveColour => IsWhiteToMove ? PieceHelper.White : PieceHelper.Black;
        public int OpponentColour => IsWhiteToMove ? PieceHelper.Black : PieceHelper.White;
        public int MoveColourIndex => IsWhiteToMove ? WhiteIndex : BlackIndex;
        public int OpponentColourIndex => IsWhiteToMove ? BlackIndex : WhiteIndex;

        // Stores piece code for each square on the board
        public int[] Square;

        // Piece lists
        public PieceList[] rooks;
        public PieceList[] bishops;
        public PieceList[] queens;
        public PieceList[] knights;
        public PieceList[] pawns;
        public PieceList[] kings;
        public PieceList[] pieceLists;
        // Square index of white and black king
        public int[] KingSquare;

        // --- Bitboards ---
        // Bitboard for each piece type and colour (white pawns, white knights, ... black pawns, etc.)
        public ulong[] pieceBitboards;
        // Bitboards for all pieces of either colour (all white pieces, all black pieces)
        public ulong[] colourBitboards;
        public ulong allPiecesBitboard;
        public ulong FriendlyOrthogonalSliders;
        public ulong FriendlyDiagonalSliders;
        public ulong EnemyOrthogonalSliders;
        public ulong EnemyDiagonalSliders;

        // Total plies (half-moves) played in game
        public int plyCount;

        // List of (hashed) positions since last pawn move or capture (for detecting 3-fold repetition)
        public Stack<ulong> RepetitionPositionHistory;
        public Stack<string> RepetitionPositionHistoryFen;

        Stack<GameState> gameStateHistory;
        public GameState currentGameState;

        public List<Move> AllGameMoves;
        public string GameStartFen => StartPositionInfo.fen;
        public FenUtility.PositionInfo StartPositionInfo;

        // piece count excluding pawns and kings
        public int totalPieceCountWithoutPawnsAndKings;
        bool cachedInCheckValue;
        bool hasCachedInCheckValue;


        // The MoveGenerator is needed for parsing SAN (Standard Algebraic Notation)
        private readonly MoveGenerator moveGenerator; // Added for SAN parsing

        public Board(Board? source = null)
        {
            // Initialize moveGenerator here
            moveGenerator = new MoveGenerator();

            if (source != null)
            {
                LoadPosition(source.StartPositionInfo);

                for (int i = 0; i < source.AllGameMoves.Count; i++)
                {
                    MakeMove(source.AllGameMoves[i], false);
                }
            }
        }


        // Is current player in check?
        // Note: caches check value so calling multiple times does not require recalculating
        public bool IsInCheck()
        {
            if (hasCachedInCheckValue)
            {
                return cachedInCheckValue;
            }
            cachedInCheckValue = CalculateInCheckState();
            hasCachedInCheckValue = true;

            return cachedInCheckValue;
        }


        // Update piece lists / bitboards based on given move info.
        // Note that this does not account for the following things, which must be handled separately:
        // 1. Removal of a captured piece
        // 2. Movement of rook when castling
        // 3. Removal of pawn from 1st/8th rank during pawn promotion
        // 4. Addition of promoted piece during pawn promotion
        void MovePiece(int piece, int startSquare, int targetSquare)
        {
            BitBoardUtility.ToggleSquares(ref pieceBitboards[piece], startSquare, targetSquare);
            BitBoardUtility.ToggleSquares(ref colourBitboards[MoveColourIndex], startSquare, targetSquare);

            pieceLists[piece].MovePiece(startSquare, targetSquare);
            Square[startSquare] = PieceHelper.None;
            Square[targetSquare] = piece;
        }

        // Make a move on the board
        // The inSearch parameter controls whether this move should be recorded in the game history.
        // (for detecting three-fold repetition)
        public void MakeMove(Move move, bool inSearch = true)
        {
            // Get info about move
            int startSquare = move.StartSquareIndex;
            int targetSquare = move.TargetSquareIndex;
            int moveFlag = move.MoveFlag;
            bool isPromotion = move.IsPromotion;
            bool isEnPassant = moveFlag is Move.EnPassantCaptureFlag;

            int movedPiece = Square[startSquare];
            int movedPieceType = PieceHelper.PieceType(movedPiece);
            int capturedPiece = isEnPassant ? PieceHelper.MakePiece(PieceHelper.Pawn, OpponentColour) : Square[targetSquare];
            int capturedPieceType = PieceHelper.PieceType(capturedPiece);

            int prevCastleState = currentGameState.castlingRights;
            int prevEnPassantFile = currentGameState.enPassantFile;
            ulong newZobristKey = currentGameState.zobristKey;
            int newCastlingRights = currentGameState.castlingRights;
            int newEnPassantFile = 0;

            // Update bitboard of moved piece (pawn promotion is a special case and is corrected later)
            MovePiece(movedPiece, startSquare, targetSquare);

            // Handle captures
            if (capturedPieceType != PieceHelper.None)
            {
                int captureSquare = targetSquare;

                if (isEnPassant)
                {
                    captureSquare = targetSquare + (IsWhiteToMove ? -8 : 8);
                    Square[captureSquare] = PieceHelper.None;
                }
                if (capturedPieceType != PieceHelper.Pawn)
                {
                    totalPieceCountWithoutPawnsAndKings--;
                }

                // Remove captured piece from bitboards/piece list
                pieceLists[capturedPiece].RemovePieceAtSquare(captureSquare);
                BitBoardUtility.ToggleSquare(ref pieceBitboards[capturedPiece], captureSquare);
                BitBoardUtility.ToggleSquare(ref colourBitboards[OpponentColourIndex], captureSquare);
                newZobristKey ^= Zobrist.piecesArray[capturedPiece, captureSquare];
            }

            // Handle king
            if (movedPieceType == PieceHelper.King)
            {
                KingSquare[MoveColourIndex] = targetSquare;
                newCastlingRights &= (IsWhiteToMove) ? 0b1100 : 0b0011;

                // Handle castling
                if (moveFlag == Move.CastleFlag)
                {
                    int rookPiece = PieceHelper.MakePiece(PieceHelper.Rook, MoveColour);
                    bool kingside = targetSquare == BoardHelper.g1 || targetSquare == BoardHelper.g8;
                    int castlingRookFromIndex = (kingside) ? targetSquare + 1 : targetSquare - 2;
                    int castlingRookToIndex = (kingside) ? targetSquare - 1 : targetSquare + 1;

                    // Update rook position
                    BitBoardUtility.ToggleSquares(ref pieceBitboards[rookPiece], castlingRookFromIndex, castlingRookToIndex);
                    BitBoardUtility.ToggleSquares(ref colourBitboards[MoveColourIndex], castlingRookFromIndex, castlingRookToIndex);
                    pieceLists[rookPiece].MovePiece(castlingRookFromIndex, castlingRookToIndex);
                    Square[castlingRookFromIndex] = PieceHelper.None;
                    Square[castlingRookToIndex] = PieceHelper.Rook | MoveColour;

                    newZobristKey ^= Zobrist.piecesArray[rookPiece, castlingRookFromIndex];
                    newZobristKey ^= Zobrist.piecesArray[rookPiece, castlingRookToIndex];
                }
            }

            // Handle promotion
            if (isPromotion)
            {
                totalPieceCountWithoutPawnsAndKings++;
                int promotionPieceType = moveFlag switch
                {
                    Move.PromoteToQueenFlag => PieceHelper.Queen,
                    Move.PromoteToRookFlag => PieceHelper.Rook,
                    Move.PromoteToKnightFlag => PieceHelper.Knight,
                    Move.PromoteToBishopFlag => PieceHelper.Bishop,
                    _ => 0
                };

                int promotionPiece = PieceHelper.MakePiece(promotionPieceType, MoveColour);

                // Remove pawn from promotion square and add promoted piece instead
                BitBoardUtility.ToggleSquare(ref pieceBitboards[movedPiece], targetSquare);
                BitBoardUtility.ToggleSquare(ref pieceBitboards[promotionPiece], targetSquare);
                pieceLists[movedPiece].RemovePieceAtSquare(targetSquare);
                pieceLists[promotionPiece].AddPieceAtSquare(targetSquare);
                Square[targetSquare] = promotionPiece;
            }

            // Pawn has moved two forwards, mark file with en-passant flag
            if (moveFlag == Move.PawnTwoUpFlag)
            {
                int file = BoardHelper.FileIndex(startSquare) + 1;
                newEnPassantFile = file;
                newZobristKey ^= Zobrist.enPassantFile[file];
            }

            // Update castling rights
            if (prevCastleState != 0)
            {
                // Any piece moving to/from rook square removes castling right for that side
                if (targetSquare == BoardHelper.h1 || startSquare == BoardHelper.h1)
                {
                    newCastlingRights &= GameState.ClearWhiteKingsideMask;
                }
                else if (targetSquare == BoardHelper.a1 || startSquare == BoardHelper.a1)
                {
                    newCastlingRights &= GameState.ClearWhiteQueensideMask;
                }
                if (targetSquare == BoardHelper.h8 || startSquare == BoardHelper.h8)
                {
                    newCastlingRights &= GameState.ClearBlackKingsideMask;
                }
                else if (targetSquare == BoardHelper.a8 || startSquare == BoardHelper.a8)
                {
                    newCastlingRights &= GameState.ClearBlackQueensideMask;
                }
            }

            // Update zobrist key with new piece position and side to move
            newZobristKey ^= Zobrist.sideToMove;
            newZobristKey ^= Zobrist.piecesArray[movedPiece, startSquare];
            newZobristKey ^= Zobrist.piecesArray[Square[targetSquare], targetSquare];
            newZobristKey ^= Zobrist.enPassantFile[prevEnPassantFile];

            if (newCastlingRights != prevCastleState)
            {
                newZobristKey ^= Zobrist.castlingRights[prevCastleState]; // remove old castling rights state
                newZobristKey ^= Zobrist.castlingRights[newCastlingRights]; // add new castling rights state
            }

            // Change side to move
            IsWhiteToMove = !IsWhiteToMove;

            plyCount++;
            int newFiftyMoveCounter = currentGameState.fiftyMoveCounter + 1;

            // Update extra bitboards
            allPiecesBitboard = colourBitboards[WhiteIndex] | colourBitboards[BlackIndex];
            UpdateSliderBitboards();

            // Pawn moves and captures reset the fifty move counter and clear 3-fold repetition history
            if (movedPieceType == PieceHelper.Pawn || capturedPieceType != PieceHelper.None)
            {
                if (!inSearch)
                {
                    RepetitionPositionHistory.Clear();
                    RepetitionPositionHistoryFen.Clear();
                }
                newFiftyMoveCounter = 0;
            }

            GameState newState = new(capturedPieceType, newEnPassantFile, newCastlingRights, newFiftyMoveCounter, newZobristKey);
            gameStateHistory.Push(newState);
            currentGameState = newState;
            hasCachedInCheckValue = false;

            if (!inSearch)
            {
                RepetitionPositionHistory.Push(newState.zobristKey);
                RepetitionPositionHistoryFen.Push(FenUtility.CurrentFen(this));
                AllGameMoves.Add(move);
            }
        }

        // Undo a move previously made on the board
        public void UndoMove(Move move, bool inSearch = true)
        {
            // Swap colour to move
            IsWhiteToMove = !IsWhiteToMove;

            bool undoingWhiteMove = IsWhiteToMove;

            // Get move info
            int movedFrom = move.StartSquareIndex;
            int movedTo = move.TargetSquareIndex;
            int moveFlag = move.MoveFlag;

            bool undoingEnPassant = moveFlag == Move.EnPassantCaptureFlag;
            bool undoingPromotion = move.IsPromotion;
            bool undoingCapture = currentGameState.capturedPieceType != PieceHelper.None;

            int movedPiece = undoingPromotion ? PieceHelper.MakePiece(PieceHelper.Pawn, MoveColour) : Square[movedTo];
            int movedPieceType = PieceHelper.PieceType(movedPiece);
            int capturedPieceType = currentGameState.capturedPieceType;

            // If undoing promotion, then remove piece from promotion square and replace with pawn
            if (undoingPromotion)
            {
                int promotedPiece = Square[movedTo];
                int pawnPiece = PieceHelper.MakePiece(PieceHelper.Pawn, MoveColour);
                totalPieceCountWithoutPawnsAndKings--;

                pieceLists[promotedPiece].RemovePieceAtSquare(movedTo);
                pieceLists[movedPiece].AddPieceAtSquare(movedTo);
                BitBoardUtility.ToggleSquare(ref pieceBitboards[promotedPiece], movedTo);
                BitBoardUtility.ToggleSquare(ref pieceBitboards[pawnPiece], movedTo);
            }

            MovePiece(movedPiece, movedTo, movedFrom);

            // Undo capture
            if (undoingCapture)
            {
                int captureSquare = movedTo;
                int capturedPiece = PieceHelper.MakePiece(capturedPieceType, OpponentColour);

                if (undoingEnPassant)
                {
                    captureSquare = movedTo + ((undoingWhiteMove) ? -8 : 8);
                }
                if (capturedPieceType != PieceHelper.Pawn)
                {
                    totalPieceCountWithoutPawnsAndKings++;
                }

                // Add back captured piece
                BitBoardUtility.ToggleSquare(ref pieceBitboards[capturedPiece], captureSquare);
                BitBoardUtility.ToggleSquare(ref colourBitboards[OpponentColourIndex], captureSquare);
                pieceLists[capturedPiece].AddPieceAtSquare(captureSquare);
                Square[captureSquare] = capturedPiece;
            }


            // Update king
            if (movedPieceType is PieceHelper.King)
            {
                KingSquare[MoveColourIndex] = movedFrom;

                // Undo castling
                if (moveFlag is Move.CastleFlag)
                {
                    int rookPiece = PieceHelper.MakePiece(PieceHelper.Rook, MoveColour);
                    bool kingside = movedTo == BoardHelper.g1 || movedTo == BoardHelper.g8;
                    int rookSquareBeforeCastling = kingside ? movedTo + 1 : movedTo - 2;
                    int rookSquareAfterCastling = kingside ? movedTo - 1 : movedTo + 1;

                    // Undo castling by returning rook to original square
                    BitBoardUtility.ToggleSquares(ref pieceBitboards[rookPiece], rookSquareAfterCastling, rookSquareBeforeCastling);
                    BitBoardUtility.ToggleSquares(ref colourBitboards[MoveColourIndex], rookSquareAfterCastling, rookSquareBeforeCastling);
                    Square[rookSquareAfterCastling] = PieceHelper.None;
                    Square[rookSquareBeforeCastling] = rookPiece;
                    pieceLists[rookPiece].MovePiece(rookSquareAfterCastling, rookSquareBeforeCastling);
                }
            }

            allPiecesBitboard = colourBitboards[WhiteIndex] | colourBitboards[BlackIndex];
            UpdateSliderBitboards();

            if (!inSearch && RepetitionPositionHistory.Count > 0)
            {
                RepetitionPositionHistory.Pop();
                RepetitionPositionHistoryFen.Pop();
            }
            if (!inSearch)
            {
                AllGameMoves.RemoveAt(AllGameMoves.Count - 1);
            }

            // Go back to previous state
            gameStateHistory.Pop();
            currentGameState = gameStateHistory.Peek();
            plyCount--;
            hasCachedInCheckValue = false;
        }

        // Switch side to play without making a move (NOTE: must not be in check when called)
        public void MakeNullMove()
        {
            IsWhiteToMove = !IsWhiteToMove;

            plyCount++;

            ulong newZobristKey = currentGameState.zobristKey;
            newZobristKey ^= Zobrist.sideToMove;
            newZobristKey ^= Zobrist.enPassantFile[currentGameState.enPassantFile];

            GameState newState = new(PieceHelper.None, 0, currentGameState.castlingRights, currentGameState.fiftyMoveCounter + 1, newZobristKey);
            currentGameState = newState;
            gameStateHistory.Push(currentGameState);
            UpdateSliderBitboards();
            hasCachedInCheckValue = true;
            cachedInCheckValue = false;
        }

        public void UnmakeNullMove()
        {

            IsWhiteToMove = !IsWhiteToMove;
            plyCount--;
            gameStateHistory.Pop();
            currentGameState = gameStateHistory.Peek();
            UpdateSliderBitboards();
            hasCachedInCheckValue = true;
            cachedInCheckValue = false;
        }


        // Calculate in check value
        // Call IsInCheck instead for automatic caching of value
        public bool CalculateInCheckState()
        {
            int kingSquare = KingSquare[MoveColourIndex];
            ulong blockers = allPiecesBitboard;

            if (EnemyOrthogonalSliders != 0)
            {
                ulong rookAttacks = Magic.GetRookAttacks(kingSquare, blockers);
                if ((rookAttacks & EnemyOrthogonalSliders) != 0)
                {
                    return true;
                }
            }
            if (EnemyDiagonalSliders != 0)
            {
                ulong bishopAttacks = Magic.GetBishopAttacks(kingSquare, blockers);
                if ((bishopAttacks & EnemyDiagonalSliders) != 0)
                {
                    return true;
                }
            }

            ulong enemyKnights = pieceBitboards[PieceHelper.MakePiece(PieceHelper.Knight, OpponentColour)];
            if ((Bits.KnightAttacks[kingSquare] & enemyKnights) != 0)
            {
                return true;
            }

            ulong enemyPawns = pieceBitboards[PieceHelper.MakePiece(PieceHelper.Pawn, OpponentColour)];
            ulong pawnAttackMask = IsWhiteToMove ? Bits.WhitePawnAttacks[kingSquare] : Bits.BlackPawnAttacks[kingSquare];
            if ((pawnAttackMask & enemyPawns) != 0)
            {
                return true;
            }

            return false;
        }


        // Load the starting position
        public void LoadStartPosition()
        {
            LoadPosition(FenUtility.StartPositionFEN);
        }

        // Load custom position from fen string
        public void LoadPosition(string fen)
        {
            LoadPosition(FenUtility.PositionFromFen(fen));
        }

        public void LoadPosition(FenUtility.PositionInfo posInfo)
        {
            StartPositionInfo = posInfo;
            Initialize();

            // Load pieces into board array and piece lists
            for (int squareIndex = 0; squareIndex < 64; squareIndex++)
            {
                int piece = posInfo.squares[squareIndex];
                int pieceType = PieceHelper.PieceType(piece);
                int colourIndex = PieceHelper.IsWhite(piece) ? WhiteIndex : BlackIndex;
                Square[squareIndex] = piece;

                if (piece != PieceHelper.None)
                {
                    BitBoardUtility.SetSquare(ref pieceBitboards[piece], squareIndex);
                    BitBoardUtility.SetSquare(ref colourBitboards[colourIndex], squareIndex);

                    if (pieceType == PieceHelper.King)
                    {
                        KingSquare[colourIndex] = squareIndex;
                    }

                    pieceLists[piece].AddPieceAtSquare(squareIndex);

                    totalPieceCountWithoutPawnsAndKings += (pieceType is PieceHelper.Pawn or PieceHelper.King) ? 0 : 1;
                }
            }

            // Side to move
            IsWhiteToMove = posInfo.whiteToMove;

            // Set extra bitboards
            allPiecesBitboard = colourBitboards[WhiteIndex] | colourBitboards[BlackIndex];
            UpdateSliderBitboards();

            // Create gamestate
            int whiteCastle = ((posInfo.whiteCastleKingside) ? 1 << 0 : 0) | ((posInfo.whiteCastleQueenside) ? 1 << 1 : 0);
            int blackCastle = ((posInfo.blackCastleKingside) ? 1 << 2 : 0) | ((posInfo.blackCastleQueenside) ? 1 << 3 : 0);
            int castlingRights = whiteCastle | blackCastle;

            plyCount = (posInfo.moveCount - 1) * 2 + (IsWhiteToMove ? 0 : 1);

            // Set game state (note: calculating zobrist key relies on current game state)
            currentGameState = new GameState(PieceHelper.None, posInfo.epFile, castlingRights, posInfo.fiftyMovePlyCount, 0);
            ulong zobristKey = Zobrist.CalculateZobristKey(this);
            currentGameState = new GameState(PieceHelper.None, posInfo.epFile, castlingRights, posInfo.fiftyMovePlyCount, zobristKey);

            RepetitionPositionHistory.Push(zobristKey);

            gameStateHistory.Push(currentGameState);
            RepetitionPositionHistoryFen.Push(FenUtility.CurrentFen(this));
        }

        void UpdateSliderBitboards()
        {
            int friendlyRook = PieceHelper.MakePiece(PieceHelper.Rook, MoveColour);
            int friendlyQueen = PieceHelper.MakePiece(PieceHelper.Queen, MoveColour);
            int friendlyBishop = PieceHelper.MakePiece(PieceHelper.Bishop, MoveColour);
            FriendlyOrthogonalSliders = pieceBitboards[friendlyRook] | pieceBitboards[friendlyQueen];
            FriendlyDiagonalSliders = pieceBitboards[friendlyBishop] | pieceBitboards[friendlyQueen];

            int enemyRook = PieceHelper.MakePiece(PieceHelper.Rook, OpponentColour);
            int enemyQueen = PieceHelper.MakePiece(PieceHelper.Queen, OpponentColour);
            int enemyBishop = PieceHelper.MakePiece(PieceHelper.Bishop, OpponentColour);
            EnemyOrthogonalSliders = pieceBitboards[enemyRook] | pieceBitboards[enemyQueen];
            EnemyDiagonalSliders = pieceBitboards[enemyBishop] | pieceBitboards[enemyQueen];
        }

        void Initialize()
        {
            AllGameMoves = new List<Move>();
            Square = new int[64];
            KingSquare = new int[2];

            RepetitionPositionHistory = new Stack<ulong>(capacity: 64);
            RepetitionPositionHistoryFen = new Stack<string>(capacity: 64);
            gameStateHistory = new Stack<GameState>(capacity: 64);

            currentGameState = new GameState();
            plyCount = 0;

            knights = new PieceList[] { new PieceList(10), new PieceList(10) };
            pawns = new PieceList[] { new PieceList(8), new PieceList(8) };
            rooks = new PieceList[] { new PieceList(10), new PieceList(10) };
            bishops = new PieceList[] { new PieceList(10), new PieceList(10) };
            queens = new PieceList[] { new PieceList(9), new PieceList(9) };
            kings = new PieceList[] { new PieceList(1), new PieceList(1) };


            pieceLists = new PieceList[PieceHelper.MaxPieceIndex + 1];
            pieceLists[PieceHelper.WhitePawn] = pawns[WhiteIndex];
            pieceLists[PieceHelper.WhiteKnight] = knights[WhiteIndex];
            pieceLists[PieceHelper.WhiteBishop] = bishops[WhiteIndex];
            pieceLists[PieceHelper.WhiteRook] = rooks[WhiteIndex];
            pieceLists[PieceHelper.WhiteQueen] = queens[WhiteIndex];
            pieceLists[PieceHelper.WhiteKing] = kings[WhiteIndex];

            pieceLists[PieceHelper.BlackPawn] = pawns[BlackIndex];
            pieceLists[PieceHelper.BlackKnight] = knights[BlackIndex];
            pieceLists[PieceHelper.BlackBishop] = bishops[BlackIndex];
            pieceLists[PieceHelper.BlackRook] = rooks[BlackIndex];
            pieceLists[PieceHelper.BlackQueen] = queens[BlackIndex];
            pieceLists[PieceHelper.BlackKing] = kings[BlackIndex];

            totalPieceCountWithoutPawnsAndKings = 0;

            // Initialize bitboards
            pieceBitboards = new ulong[PieceHelper.MaxPieceIndex + 1];
            colourBitboards = new ulong[2];
            allPiecesBitboard = 0;
        }

        // =========================================================================================
        // 🚨 FIX: SAN Parsing Method (Crucial for PGN loading) 
        // =========================================================================================

        /// <summary>
        /// Attempts to parse a move in Standard Algebraic Notation (SAN) and make the move on the board.
        /// This is crucial for PGN loading. If successful, the move is made and true is returned.
        /// If unsuccessful (e.g., illegal move or invalid SAN), false is returned and the board remains unchanged.
        /// </summary>
        public bool TryMakeMoveFromSan(string sanMoveString, out Chess.Move move)
        {
            // Remove check/checkmate symbols and annotations
            sanMoveString = sanMoveString.Replace("+", "").Replace("#", "").Replace("!", "").Replace("?", "").Trim();

            // Handle castling
            if (sanMoveString == "O-O" || sanMoveString == "0-0")
            {
                return TryMakeCastlingMove(true, out move);
            }
            if (sanMoveString == "O-O-O" || sanMoveString == "0-0-0")
            {
                return TryMakeCastlingMove(false, out move);
            }

            // Generate all legal moves
            System.Span<Chess.Move> legalMoves = moveGenerator.GenerateMoves(this, includeQuietMoves: true);

            // Parse the SAN move
            int pieceType = PieceHelper.Pawn;
            int startIndex = 0;

            // Check if first character is a piece indicator
            if (sanMoveString.Length > 0 && "NBRQK".Contains(sanMoveString[0]))
            {
                pieceType = sanMoveString[0] switch
                {
                    'N' => PieceHelper.Knight,
                    'B' => PieceHelper.Bishop,
                    'R' => PieceHelper.Rook,
                    'Q' => PieceHelper.Queen,
                    'K' => PieceHelper.King,
                    _ => PieceHelper.Pawn
                };
                startIndex = 1;
            }

            // Remove capture symbol if present
            string moveStr = sanMoveString.Substring(startIndex).Replace("x", "");

            // Extract target square and check for promotion
            string targetSquare;
            int promotionPieceType = PieceHelper.None;

            // Handle promotion with = sign (e.g., e8=Q)
            if (moveStr.Contains("="))
            {
                int promoIndex = moveStr.IndexOf('=');
                targetSquare = moveStr.Substring(promoIndex - 2, 2);
                if (promoIndex + 1 < moveStr.Length)
                {
                    promotionPieceType = moveStr[promoIndex + 1] switch
                    {
                        'Q' or 'q' => PieceHelper.Queen,
                        'R' or 'r' => PieceHelper.Rook,
                        'B' or 'b' => PieceHelper.Bishop,
                        'N' or 'n' => PieceHelper.Knight,
                        _ => PieceHelper.None
                    };
                }
            }
            // Handle promotion WITHOUT = sign (e.g., e8Q or h7h8q)
            else if (moveStr.Length >= 3)
            {
                char lastChar = moveStr[moveStr.Length - 1];
                // Check if last character is a promotion piece
                if ("QRBNqrbn".Contains(lastChar))
                {
                    // Check if second-to-last char is '8' or '1' (promotion rank)
                    if (moveStr.Length >= 3 && (moveStr[moveStr.Length - 2] == '8' || moveStr[moveStr.Length - 2] == '1'))
                    {
                        targetSquare = moveStr.Substring(moveStr.Length - 3, 2);
                        promotionPieceType = lastChar switch
                        {
                            'Q' or 'q' => PieceHelper.Queen,
                            'R' or 'r' => PieceHelper.Rook,
                            'B' or 'b' => PieceHelper.Bishop,
                            'N' or 'n' => PieceHelper.Knight,
                            _ => PieceHelper.None
                        };
                    }
                    else
                    {
                        targetSquare = moveStr.Substring(moveStr.Length - 2, 2);
                    }
                }
                else
                {
                    targetSquare = moveStr.Substring(moveStr.Length - 2, 2);
                }
            }
            else if (moveStr.Length >= 2)
            {
                targetSquare = moveStr.Substring(moveStr.Length - 2, 2);
            }
            else
            {
                move = Chess.Move.NullMove;
                return false;
            }

            // Parse target square
            if (targetSquare.Length != 2 ||
                targetSquare[0] < 'a' || targetSquare[0] > 'h' ||
                targetSquare[1] < '1' || targetSquare[1] > '8')
            {
                move = Chess.Move.NullMove;
                return false;
            }

            int targetFile = targetSquare[0] - 'a';
            int targetRank = targetSquare[1] - '1';
            int targetSquareIndex = targetRank * 8 + targetFile;

            // Extract disambiguation info (file or rank)
            int? disambiguationFile = null;
            int? disambiguationRank = null;

            // Calculate how much of the string is disambiguation
            int endOfDisambiguation = moveStr.Length - 2; // Default: stop before last 2 chars (target square)
            if (promotionPieceType != PieceHelper.None)
            {
                // If there's a promotion, we need to account for the promotion character too
                endOfDisambiguation = moveStr.Contains("=") ? moveStr.IndexOf('=') - 2 : moveStr.Length - 3;
            }

            string disambiguation = moveStr.Substring(0, Math.Max(0, endOfDisambiguation));
            if (disambiguation.Length > 0)
            {
                foreach (char c in disambiguation)
                {
                    if (c >= 'a' && c <= 'h')
                    {
                        disambiguationFile = c - 'a';
                    }
                    else if (c >= '1' && c <= '8')
                    {
                        disambiguationRank = c - '1';
                    }
                }
            }

            // Find matching legal move
            foreach (var legalMove in legalMoves)
            {
                if (legalMove.TargetSquareIndex != targetSquareIndex)
                    continue;

                int movedPiece = Square[legalMove.StartSquareIndex];
                if (PieceHelper.PieceType(movedPiece) != pieceType)
                    continue;

                // Check promotion
                if (promotionPieceType != PieceHelper.None)
                {
                    if (!legalMove.IsPromotion || legalMove.PromotionPieceType != promotionPieceType)
                        continue;
                }

                // Check disambiguation
                if (disambiguationFile.HasValue)
                {
                    int startFile = legalMove.StartSquareIndex % 8;
                    if (startFile != disambiguationFile.Value)
                        continue;
                }

                if (disambiguationRank.HasValue)
                {
                    int startRank = legalMove.StartSquareIndex / 8;
                    if (startRank != disambiguationRank.Value)
                        continue;
                }

                // Found the move!
                MakeMove(legalMove, inSearch: false);
                move = legalMove;
                return true;
            }

            move = Chess.Move.NullMove;
            return false;
        }

        private bool TryMakeCastlingMove(bool kingSide, out Chess.Move move)
        {
            System.Span<Chess.Move> legalMoves = moveGenerator.GenerateMoves(this, includeQuietMoves: true);

            int targetSquare;
            if (IsWhiteToMove)
            {
                targetSquare = kingSide ? BoardHelper.g1 : BoardHelper.c1;
            }
            else
            {
                targetSquare = kingSide ? BoardHelper.g8 : BoardHelper.c8;
            }

            foreach (var legalMove in legalMoves)
            {
                if (legalMove.MoveFlag == Chess.Move.CastleFlag &&
                    legalMove.TargetSquareIndex == targetSquare)
                {
                    MakeMove(legalMove, inSearch: false);
                    move = legalMove;
                    return true;
                }
            }

            move = Chess.Move.NullMove;
            return false;
        }

        // NOTE: The previous API.Board.cs file also needed a call to this method.
        // That API file modification must be applied as well for a complete solution.

    }
}