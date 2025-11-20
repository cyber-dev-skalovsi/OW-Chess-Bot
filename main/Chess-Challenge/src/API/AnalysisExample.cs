using ChessChallenge.API;
using ChessChallenge.Evaluation;
using System;
using System.Collections.Generic;

namespace ChessChallenge.Examples
{
    public class AnalysisExample
    {
        public static void Main()
        {
            // Example 1: Analyze starting position
            AnalyzeStartingPosition();

            // Example 2: Analyze a specific position
            AnalyzeSpecificPosition();

            // Example 3: Analyze after a specific move
            AnalyzeMoveSequence();

            // Example 4: Compare multiple positions
            ComparePositions();
        }

        public static void AnalyzeStartingPosition()
        {
            Console.WriteLine("=== Example 1: Starting Position ===\n");

            // Create bot and board
            var myBot = new MyBot();
            var board = Board.CreateBoardFromFEN("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");

            // Create analyzer
            var analyzer = new EvalAnalyzer(myBot);

            // Analyze top 5 moves
            var breakdowns = analyzer.AnalyzeTopMoves(board, numMoves: 5);

            // Export all formats
            EvalExporter.ExportAll(breakdowns, board, "./output/starting_position");

            // Print to console
            Console.WriteLine("Best move: " + breakdowns[0].MoveUCI);
            Console.WriteLine("Score: " + breakdowns[0].TotalScore / 100.0);
            Console.WriteLine("\nTop 3 moves:");
            for (int i = 0; i < Math.Min(3, breakdowns.Count); i++)
            {
                Console.WriteLine($"{i + 1}. {breakdowns[i].MoveUCI} ({breakdowns[i].TotalScore / 100.0:+0.00})");
            }
            Console.WriteLine();
        }

        public static void AnalyzeSpecificPosition()
        {
            Console.WriteLine("=== Example 2: Specific Position ===\n");

            var myBot = new MyBot();

            // Italian Game position
            var board = Board.CreateBoardFromFEN("r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R b KQkq - 3 3");

            var analyzer = new EvalAnalyzer(myBot);
            var breakdowns = analyzer.AnalyzeTopMoves(board, numMoves: 5);

            EvalExporter.ExportAll(breakdowns, board, "./output/italian_game");

            Console.WriteLine($"Position: Italian Game");
            Console.WriteLine($"Best move: {breakdowns[0].MoveUCI} with score {breakdowns[0].TotalScore / 100.0:+0.00}");
            Console.WriteLine();
        }

        public static void AnalyzeMoveSequence()
        {
            Console.WriteLine("=== Example 3: Move Sequence Analysis ===\n");

            var myBot = new MyBot();
            var board = Board.CreateBoardFromFEN("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
            var analyzer = new EvalAnalyzer(myBot);

            // Get evaluation before move
            var beforeEval = analyzer.AnalyzeCurrentPosition(board);
            Console.WriteLine($"Position before move: {beforeEval.TotalScore / 100.0:+0.00}");

            // Make a move
            var move = new Move("e2e4", board);
            board.MakeMove(move);

            // Get evaluation after move
            var afterEval = analyzer.AnalyzeCurrentPosition(board);
            Console.WriteLine($"Position after e2e4: {afterEval.TotalScore / 100.0:+0.00}");
            Console.WriteLine($"Change: {(afterEval.TotalScore - beforeEval.TotalScore) / 100.0:+0.00}");

            // Export comparison
            EvalExporter.ExportMoveComparison(beforeEval, afterEval, "./output/move_e2e4_comparison.txt");
            Console.WriteLine();
        }

        public static void ComparePositions()
        {
            Console.WriteLine("=== Example 4: Position Comparison ===\n");

            var myBot = new MyBot();
            var analyzer = new EvalAnalyzer(myBot);

            // Compare starting position with a few moves played
            var positions = new Dictionary<string, string>
            {
                ["Starting"] = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
                ["After 1.e4"] = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
                ["After 1.e4 e5"] = "rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq e6 0 2",
                ["After 1.e4 e5 2.Nf3"] = "rnbqkbnr/pppp1ppp/8/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2"
            };

            foreach (var pos in positions)
            {
                var board = Board.CreateBoardFromFEN(pos.Value);
                var eval = analyzer.AnalyzeCurrentPosition(board);
                Console.WriteLine($"{pos.Key,-20} Score: {eval.TotalScore / 100.0,+6:0.00}");
            }
            Console.WriteLine();
        }

        // Helper: Analyze a position and send to LLM
        public static void AnalyzeForLLM(string fen, string outputName)
        {
            var myBot = new MyBot();
            var board = Board.CreateBoardFromFEN(fen);
            var analyzer = new EvalAnalyzer(myBot);

            var breakdowns = analyzer.AnalyzeTopMoves(board, numMoves: 5);

            EvalExporter.ExportAll(breakdowns, board, $"./output/{outputName}");

            Console.WriteLine($"Analysis ready for LLM at: ./output/{outputName}_llm_prompt.txt");
            Console.WriteLine("Copy the contents and send to Claude or GPT for explanation.");
        }

        // Helper: Batch analyze multiple positions
        public static void BatchAnalyze(Dictionary<string, string> positions, string outputFolder)
        {
            var myBot = new MyBot();
            var analyzer = new EvalAnalyzer(myBot);

            foreach (var kvp in positions)
            {
                Console.WriteLine($"Analyzing {kvp.Key}...");
                var board = Board.CreateBoardFromFEN(kvp.Value);
                var breakdowns = analyzer.AnalyzeTopMoves(board, numMoves: 5);
                EvalExporter.ExportAll(breakdowns, board, $"{outputFolder}/{kvp.Key}");
            }

            Console.WriteLine($"Batch analysis complete! Files saved to {outputFolder}/");
        }

        // Helper: Interactive analysis
        public static void InteractiveAnalysis()
        {
            var myBot = new MyBot();
            var analyzer = new EvalAnalyzer(myBot);

            Console.WriteLine("Enter FEN string (or 'start' for starting position, 'quit' to exit):");

            while (true)
            {
                Console.Write("> ");
                string input = Console.ReadLine()?.Trim();

                if (input == "quit") break;

                string fen = input == "start"
                    ? "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
                    : input;

                try
                {
                    var board = Board.CreateBoardFromFEN(fen);
                    var breakdowns = analyzer.AnalyzeTopMoves(board, numMoves: 5);

                    Console.WriteLine("\nTop 5 moves:");
                    for (int i = 0; i < breakdowns.Count; i++)
                    {
                        Console.WriteLine($"{i + 1}. {breakdowns[i].MoveUCI,-6} {breakdowns[i].TotalScore / 100.0,+6:0.00}");
                    }
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}\n");
                }
            }
        }
    }

    // Quick test class for debugging
    public class QuickTest
    {
        public static void TestEvaluation()
        {
            var myBot = new MyBot();
            var board = Board.CreateBoardFromFEN("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");

            // Test the detailed evaluation
            var components = myBot.GetDetailedEval(board);

            Console.WriteLine("=== Evaluation Test ===");
            Console.WriteLine(components.ToString());

            // Test the analyzer
            var analyzer = new EvalAnalyzer(myBot);
            var breakdown = analyzer.AnalyzeCurrentPosition(board);

            Console.WriteLine("\n=== Breakdown Test ===");
            Console.WriteLine(breakdown.ToString());
        }
    }
}