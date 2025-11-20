using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessChallenge.Evaluation
{
    public class EvalBreakdown
    {
        public string MoveName { get; set; }
        public string MoveUCI { get; set; }
        public int TotalScore { get; set; }
        public Dictionary<string, double> Factors { get; set; } = new();
        public EvalComponents Components { get; set; }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{MoveName} ({MoveUCI})  Score: {TotalScore / 100.0:+0.00;-0.00}");
            foreach (var factor in Factors.OrderByDescending(f => Math.Abs(f.Value)))
            {
                if (Math.Abs(factor.Value) >= 0.5) // Only show significant factors
                {
                    sb.AppendLine($"   - {factor.Key,-35} {factor.Value / 100.0,+7:0.00}");
                }
            }
            return sb.ToString();
        }
    }

    public class EvalAnalyzer
    {
        private MyBot bot;
        private Board board;

        public EvalAnalyzer(MyBot bot)
        {
            this.bot = bot;
        }

        public List<EvalBreakdown> AnalyzeTopMoves(Board board, int numMoves = 5, int searchDepth = 0)
        {
            this.board = board;
            var moves = board.GetLegalMoves();
            var results = new List<EvalBreakdown>();

            foreach (var move in moves)
            {
                board.MakeMove(move);

                // Get detailed evaluation from the enhanced MyBot
                var components = bot.GetDetailedEval(board);

                // Negate score because we evaluate from opponent's perspective after move
                int score = -components.FinalScore;

                var breakdown = new EvalBreakdown
                {
                    MoveName = GetMoveName(move),
                    MoveUCI = GetMoveUCI(move),
                    TotalScore = score,
                    Components = components
                };

                // Build human-readable factors
                breakdown.Factors = BuildFactors(components, move);

                board.UndoMove(move);
                results.Add(breakdown);
            }

            // Sort by score (best first)
            results = results.OrderByDescending(r => r.TotalScore).Take(numMoves).ToList();
            return results;
        }

        private Dictionary<string, double> BuildFactors(EvalComponents comp, Move move)
        {
            var factors = new Dictionary<string, double>();

            // Main components
            factors["Material balance"] = comp.Material;
            factors["Piece-square tables"] = comp.PieceSquareTables;
            factors["Mobility"] = comp.Mobility;
            factors["Pawn structure"] = comp.PawnStructure;
            factors["Tempo"] = comp.Tempo;

            // Derived factors
            int totalWhitePieces = 0;
            int totalBlackPieces = 0;
            for (int i = 1; i <= 6; i++)
            {
                totalWhitePieces += comp.WhitePieceValues[i];
                totalBlackPieces += comp.BlackPieceValues[i];
            }

            // Material advantage
            if (Math.Abs(comp.Material) > 100)
            {
                factors["Material advantage"] = comp.Material;
            }

            // Development (minor pieces contribution)
            int knightBishopValue = (comp.WhitePieceValues[2] + comp.WhitePieceValues[3]) -
                                   (comp.BlackPieceValues[2] + comp.BlackPieceValues[3]);
            if (Math.Abs(knightBishopValue) > 50)
            {
                factors["Minor piece activity"] = knightBishopValue;
            }

            // Rook activity
            int rookValue = comp.WhitePieceValues[4] - comp.BlackPieceValues[4];
            if (Math.Abs(rookValue) > 50)
            {
                factors["Rook placement"] = rookValue;
            }

            // Queen activity
            int queenValue = comp.WhitePieceValues[5] - comp.BlackPieceValues[5];
            if (Math.Abs(queenValue) > 50)
            {
                factors["Queen activity"] = queenValue;
            }

            // King safety (from PST component)
            int kingValue = comp.WhitePieceValues[6] - comp.BlackPieceValues[6];
            if (Math.Abs(kingValue) > 30)
            {
                factors["King safety"] = kingValue;
            }

            // Mobility by piece type
            int minorMobility = (comp.WhiteMobility[2] + comp.WhiteMobility[3]) -
                               (comp.BlackMobility[2] + comp.BlackMobility[3]);
            if (Math.Abs(minorMobility) > 2)
            {
                factors["Minor piece mobility"] = minorMobility * 10; // Scale for readability
            }

            int rookMobility = comp.WhiteMobility[4] - comp.BlackMobility[4];
            if (Math.Abs(rookMobility) > 2)
            {
                factors["Rook mobility"] = rookMobility * 10;
            }

            int queenMobility = comp.WhiteMobility[5] - comp.BlackMobility[5];
            if (Math.Abs(queenMobility) > 2)
            {
                factors["Queen mobility"] = queenMobility * 10;
            }

            // Phase-related info
            factors["Game phase (24=opening, 0=endgame)"] = comp.Phase;

            // Move-specific factors
            if (move.IsCapture)
            {
                factors["Capture bonus"] = (int)move.CapturePieceType * 100;
            }

            if (move.IsPromotion)
            {
                factors["Promotion bonus"] = 800;
            }

            if (move.IsCastles)
            {
                factors["Castling bonus"] = 50;
            }

            return factors;
        }

        private string GetMoveName(Move move)
        {
            string name = $"{move.StartSquare.Name}{move.TargetSquare.Name}";
            if (move.IsPromotion)
            {
                name += move.PromotionPieceType.ToString()[0].ToString().ToUpper();
            }
            return name;
        }

        private string GetMoveUCI(Move move)
        {
            string uci = $"{move.StartSquare.Name}{move.TargetSquare.Name}";
            if (move.IsPromotion)
            {
                uci += move.PromotionPieceType.ToString()[0].ToString().ToLower();
            }
            return uci;
        }

        // Analyze a single move in detail
        public EvalBreakdown AnalyzeSingleMove(Board board, Move move)
        {
            this.board = board;

            board.MakeMove(move);
            var components = bot.GetDetailedEval(board);
            int score = -components.FinalScore;

            var breakdown = new EvalBreakdown
            {
                MoveName = GetMoveName(move),
                MoveUCI = GetMoveUCI(move),
                TotalScore = score,
                Components = components,
                Factors = BuildFactors(components, move)
            };

            board.UndoMove(move);
            return breakdown;
        }

        // Get current position evaluation
        public EvalBreakdown AnalyzeCurrentPosition(Board board)
        {
            var components = bot.GetDetailedEval(board);

            return new EvalBreakdown
            {
                MoveName = "Current Position",
                MoveUCI = "",
                TotalScore = components.FinalScore,
                Components = components,
                Factors = BuildFactors(components, Move.NullMove)
            };
        }
    }
}