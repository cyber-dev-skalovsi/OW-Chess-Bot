using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ChessChallenge.API;

namespace ChessChallenge.Evaluation
{
    public class EvalExporter
    {
        public static void ExportToJson(List<EvalBreakdown> breakdowns, string outputPath)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var exportData = breakdowns.Select(b => new
            {
                move = b.MoveUCI,
                score = b.TotalScore / 100.0,
                factors = b.Factors.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value / 100.0
                ),
                components = new
                {
                    material = b.Components.Material,
                    pst = b.Components.PieceSquareTables,
                    mobility = b.Components.Mobility,
                    pawnStructure = b.Components.PawnStructure,
                    tempo = b.Components.Tempo,
                    phase = b.Components.Phase,
                    finalScore = b.Components.FinalScore
                }
            }).ToList();

            var json = JsonSerializer.Serialize(exportData, options);
            File.WriteAllText(outputPath, json);
        }

        public static void ExportForLLM(List<EvalBreakdown> breakdowns, Board board, string outputPath)
        {
            var prompt = BuildLLMPrompt(breakdowns, board);
            File.WriteAllText(outputPath, prompt);
        }

        public static void ExportAll(List<EvalBreakdown> breakdowns, Board board, string baseOutputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baseOutputPath));

            // Save JSON
            ExportToJson(breakdowns, baseOutputPath + "_breakdown.json");

            // Save LLM prompt
            ExportForLLM(breakdowns, board, baseOutputPath + "_llm_prompt.txt");

            // Save FEN
            File.WriteAllText(baseOutputPath + "_position.fen", board.GetFenString());

            // Save human-readable text
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("                    POSITION ANALYSIS");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"FEN: {board.GetFenString()}");
            sb.AppendLine($"Turn to move: {(board.IsWhiteToMove ? "White" : "Black")}");
            sb.AppendLine($"Ply count: {board.PlyCount}");
            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("                     TOP MOVES");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine();

            for (int i = 0; i < breakdowns.Count; i++)
            {
                sb.AppendLine($"#{i + 1} {breakdowns[i].ToString()}");
            }

            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();

            // Add detailed component breakdown for best move
            if (breakdowns.Count > 0)
            {
                sb.AppendLine("DETAILED BREAKDOWN OF BEST MOVE:");
                sb.AppendLine("───────────────────────────────────────────────────────────────");
                var best = breakdowns[0];
                sb.AppendLine(best.Components.ToString());
            }

            File.WriteAllText(baseOutputPath + "_analysis.txt", sb.ToString());

            Console.WriteLine($"Analysis exported to: {baseOutputPath}_*");
        }

        private static string BuildLLMPrompt(List<EvalBreakdown> breakdowns, Board board)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are an expert chess engine analyst.");
            sb.AppendLine("I have a custom evaluation function that analyzes positions and returns scores.");
            sb.AppendLine("For the current position, my engine has calculated the following:");
            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();

            sb.AppendLine($"Position FEN: {board.GetFenString()}");
            sb.AppendLine($"Turn to move: {(board.IsWhiteToMove ? "White" : "Black")}");
            sb.AppendLine();

            if (breakdowns.Count > 0)
            {
                var best = breakdowns[0];
                sb.AppendLine($"Best move according to engine: {best.MoveUCI}");
                sb.AppendLine($"Score of best move: {best.TotalScore / 100.0:+0.00;-0.00}");
                sb.AppendLine();
            }

            sb.AppendLine($"Top {breakdowns.Count} candidate moves with their scores and the main factors that contributed:");
            sb.AppendLine();

            for (int i = 0; i < breakdowns.Count; i++)
            {
                var move = breakdowns[i];
                sb.AppendLine($"{i + 1}. {move.MoveUCI,-8} {move.TotalScore / 100.0,+6:0.00}");

                // Only show most significant factors
                var significantFactors = move.Factors
                    .Where(f => Math.Abs(f.Value) >= 10.0) // Only factors >= 0.10
                    .OrderByDescending(f => Math.Abs(f.Value))
                    .Take(8); // Top 8 factors

                foreach (var factor in significantFactors)
                {
                    sb.AppendLine($"   - {factor.Key,-35} {factor.Value / 100.0,+6:0.00}");
                }

                if (i < breakdowns.Count - 1)
                    sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("Your task:");
            sb.AppendLine("Read and fully understand the complete numerical evaluation above.");
            sb.AppendLine("Then explain in 2-4 natural, human-sounding sentences WHY the engine believes");
            sb.AppendLine("its top move is the best one, and why it rejected the other strong-looking alternatives.");
            sb.AppendLine();
            sb.AppendLine("Important rules:");
            sb.AppendLine("- Do NOT hallucinate new factors that are not in the list.");
            sb.AppendLine("- Only use the factors and numbers my engine actually calculated.");
            sb.AppendLine("- Mention the most important positive and negative factors by name.");
            sb.AppendLine("- Keep it concise but insightful (maximum 4 sentences).");
            sb.AppendLine("- Sound confident and authoritative, like a grandmaster.");

            return sb.ToString();
        }

        // Export a comparison between two positions (before/after a move)
        public static void ExportMoveComparison(
            EvalBreakdown beforeMove,
            EvalBreakdown afterMove,
            string outputPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("                    MOVE COMPARISON");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"Move: {afterMove.MoveUCI}");
            sb.AppendLine();
            sb.AppendLine($"Score change: {beforeMove.TotalScore / 100.0:+0.00;-0.00} → {afterMove.TotalScore / 100.0:+0.00;-0.00}");
            sb.AppendLine($"Difference: {(afterMove.TotalScore - beforeMove.TotalScore) / 100.0:+0.00;-0.00}");
            sb.AppendLine();
            sb.AppendLine("Component changes:");
            sb.AppendLine($"  Material:     {beforeMove.Components.Material,6} → {afterMove.Components.Material,6}  ({afterMove.Components.Material - beforeMove.Components.Material,+6})");
            sb.AppendLine($"  PST:          {beforeMove.Components.PieceSquareTables,6} → {afterMove.Components.PieceSquareTables,6}  ({afterMove.Components.PieceSquareTables - beforeMove.Components.PieceSquareTables,+6})");
            sb.AppendLine($"  Mobility:     {beforeMove.Components.Mobility,6} → {afterMove.Components.Mobility,6}  ({afterMove.Components.Mobility - beforeMove.Components.Mobility,+6})");
            sb.AppendLine($"  Pawn Struct:  {beforeMove.Components.PawnStructure,6} → {afterMove.Components.PawnStructure,6}  ({afterMove.Components.PawnStructure - beforeMove.Components.PawnStructure,+6})");
            sb.AppendLine($"  Phase:        {beforeMove.Components.Phase,6} → {afterMove.Components.Phase,6}  ({afterMove.Components.Phase - beforeMove.Components.Phase,+6})");

            File.WriteAllText(outputPath, sb.ToString());
        }
    }
}