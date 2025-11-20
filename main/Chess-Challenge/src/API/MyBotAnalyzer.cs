using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Math;
using static ChessChallenge.API.BitboardHelper;

namespace ChessChallenge.Evaluation
{
    public class MyBotAnalyzer
    {
        private MyBot bot;
        private ChessChallenge.API.Board board;  // ← Add namespace qualifier
        public MyBotAnalyzer(MyBot bot)
        {
            this.bot = bot;
        }

        public List<EvalBreakdown> AnalyzeTopMoves(ChessChallenge.API.Board board, int numMoves = 5)
        {
            this.board = board;
            var moves = board.GetLegalMoves();
            var results = new List<EvalBreakdown>();

            foreach (var move in moves)
            {
                board.MakeMove(move);

                var breakdown = new EvalBreakdown
                {
                    MoveName = $"{move.StartSquare.Name}{move.TargetSquare.Name}",
                    MoveUCI = $"{move.StartSquare.Name}{move.TargetSquare.Name}" +
                              (move.IsPromotion ? move.PromotionPieceType.ToString()[0].ToString().ToLower() : ""),
                    TotalScore = EvaluatePosition()
                };

                // Calculate individual factors
                breakdown.Factors = CalculateFactors();

                board.UndoMove(move);
                results.Add(breakdown);
            }

            // Sort by score (best first)
            results = results.OrderByDescending(r => r.TotalScore).Take(numMoves).ToList();
            return results;
        }

        private int EvaluatePosition()
        {
            // Call the bot's evaluation through Negamax at depth 0
            // This is a simplified approach - we'll calculate directly instead
            return EvaluateDetailed().TotalScore;
        }

        private Dictionary<string, double> CalculateFactors()
        {
            var factors = new Dictionary<string, double>();
            var eval = EvaluateDetailed();

            factors["Material"] = eval.Material;
            factors["Piece-Square Tables"] = eval.PST;
            factors["Mobility"] = eval.Mobility;
            factors["Pawn Structure"] = eval.PawnStructure;
            factors["King Safety"] = eval.KingSafety;
            factors["Central Control"] = eval.CentralControl;
            factors["Development"] = eval.Development;

            return factors;
        }

        private DetailedEval EvaluateDetailed()
        {
            var result = new DetailedEval();
            int phase = 0;
            int mgEval = 0;
            int egEval = 0;

            // Material values (approximate from packed data)
            int[] mgMaterial = { 0, 100, 320, 330, 500, 900, 0 }; // P, N, B, R, Q, K
            int[] egMaterial = { 0, 120, 320, 330, 500, 900, 0 };

            ulong pieces = board.AllPiecesBitboard;

            while (pieces != 0)
            {
                int sqIndex = ClearAndGetIndexOfLSB(ref pieces);
                Piece piece = board.GetPiece(new Square(sqIndex));
                if (piece.IsNull) continue;

                int pieceType = (int)piece.PieceType;
                bool isWhite = piece.IsWhite;
                int sign = (isWhite == board.IsWhiteToMove) ? 1 : -1;

                // Material
                int mgMat = mgMaterial[pieceType] * sign;
                int egMat = egMaterial[pieceType] * sign;
                result.Material += (mgMat + egMat) / 2;

                // Mobility
                int mobility = GetNumberOfSetBits(
                    GetSliderAttacks((PieceType)Min(5, pieceType), new Square(sqIndex), board)
                );
                result.Mobility += mobility * sign * 5; // Approximate weight

                // Phase
                int[] phaseWeights = { 0, 0, 1, 1, 2, 4, 0 };
                phase += phaseWeights[pieceType];

                // Central control (e4, d4, e5, d5)
                if ((sqIndex >= 27 && sqIndex <= 28) || (sqIndex >= 35 && sqIndex <= 36))
                {
                    result.CentralControl += 20 * sign;
                }

                // Pawn structure
                if (pieceType == 1) // Pawn
                {
                    ulong aheadMask = isWhite
                        ? (0x0101010101010100UL << sqIndex)
                        : (0x0080808080808080UL >> (63 - sqIndex));
                    int pawnsAhead = GetNumberOfSetBits(
                        aheadMask & board.GetPieceBitboard(PieceType.Pawn, isWhite)
                    );
                    result.PawnStructure += pawnsAhead * sign * 10;
                }

                // Development (minor pieces off back rank)
                if (pieceType >= 2 && pieceType <= 3) // Knight or Bishop
                {
                    int rank = sqIndex / 8;
                    if ((isWhite && rank > 0) || (!isWhite && rank < 7))
                    {
                        result.Development += 15 * sign;
                    }
                }

                // King safety (approximate)
                if (pieceType == 6) // King
                {
                    int kingFile = sqIndex % 8;
                    int kingRank = sqIndex / 8;

                    // Castled position bonus
                    if ((isWhite && kingRank == 0 && (kingFile == 1 || kingFile == 6)) ||
                        (!isWhite && kingRank == 7 && (kingFile == 1 || kingFile == 6)))
                    {
                        result.KingSafety += 30 * sign;
                    }

                    // Penalty for exposed king
                    if ((isWhite && kingRank > 1) || (!isWhite && kingRank < 6))
                    {
                        result.KingSafety -= 20 * sign;
                    }
                }
            }

            // PST contribution (approximation - the real one is in packed data)
            result.PST = 0; // Difficult to extract from packed format

            result.TotalScore = result.Material + result.Mobility + result.PawnStructure +
                                result.CentralControl + result.Development + result.KingSafety;

            return result;
        }

        private class DetailedEval
        {
            public int Material;
            public int PST;
            public int Mobility;
            public int PawnStructure;
            public int KingSafety;
            public int CentralControl;
            public int Development;
            public int TotalScore;
        }
    }
}