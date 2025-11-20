using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ChessChallenge.API;

namespace ChessChallenge.AI
{
    public class GroqAIHelper
    {
        private readonly string GROQ_API_KEY = "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";
        private readonly HttpClient httpClient;

        public GroqAIHelper()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {GROQ_API_KEY}");
        }

        public async Task<string> ExplainMoveShortAsync(Board board, Move move, string moveUCI)
        {
            try
            {
                var prompt = $@"Position: {board.GetFenString()}
Move: {moveUCI}
{(move.IsCastles ? "Type: Castling" : "")}
{(move.IsCapture ? $"Captures: {move.CapturePieceType}" : "")}

Explain this chess move in 2-3 SHORT sentences. Focus on what it accomplishes and the plan.";

                var requestBody = new
                {
                    model = "llama-3.3-70b-versatile",
                    messages = new[]
                    {
                new { role = "system", content = "You are a chess coach. Explain moves concisely in 2-3 sentences." },
                new { role = "user", content = prompt }
            },
                    temperature = 0.6,
                    max_tokens = 150
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(
                    "https://api.groq.com/openai/v1/chat/completions",
                    content
                );

                if (!response.IsSuccessStatusCode)
                {
                    return "Move executed.";
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);

                return doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "Move played.";
            }
            catch
            {
                return "Move executed.";
            }
        }

        public async Task<string> ExplainBlunderAsync(Board board, Move move, string moveUCI, double evalSwing)
        {
            try
            {
                var prompt = $@"🚨 Opponent blundered!

Position: {board.GetFenString()}
Your move: {moveUCI}
Advantage gained: +{evalSwing:F1}

In 2-3 sentences: What did opponent mess up, and how to capitalize?";

                var requestBody = new
                {
                    model = "llama-3.3-70b-versatile",
                    messages = new[]
                    {
                new { role = "system", content = "Explain chess blunders concisely: what went wrong and how to exploit it. 2-3 sentences max." },
                new { role = "user", content = prompt }
            },
                    temperature = 0.7,
                    max_tokens = 150
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(
                    "https://api.groq.com/openai/v1/chat/completions",
                    content
                );

                if (!response.IsSuccessStatusCode)
                {
                    return $"Opponent made a serious mistake! Capitalize on the +{evalSwing:F1} advantage.";
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);

                return doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? $"Exploit the +{evalSwing:F1} advantage!";
            }
            catch
            {
                return $"Opponent blundered! Take advantage of the +{evalSwing:F1} position.";
            }
        }

        private string BuildSimplePrompt(Board board, Move move, string moveUCI)
        {
            var sb = new StringBuilder();

            sb.AppendLine("CHESS POSITION ANALYSIS");
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"📍 Position (FEN): {board.GetFenString()}");
            sb.AppendLine($"🔄 Turn: {(board.IsWhiteToMove ? "White" : "Black")}");
            sb.AppendLine();

            // Move details
            sb.AppendLine($"✅ MOVE PLAYED: {moveUCI}");
            sb.AppendLine($"   From: {move.StartSquare.Name} → To: {move.TargetSquare.Name}");

            var piece = board.GetPiece(move.StartSquare);
            sb.AppendLine($"   Piece: {piece.PieceType}");

            if (move.IsCastles)
                sb.AppendLine("   🏰 This is a CASTLING move (king safety + rook activation)");
            if (move.IsCapture)
                sb.AppendLine($"   ⚔️ Captures: {move.CapturePieceType}");
            if (move.IsPromotion)
                sb.AppendLine($"   👑 Promotes to: {move.PromotionPieceType}");
            if (move.IsEnPassant)
                sb.AppendLine("   🎯 En passant capture");

            sb.AppendLine();

            // Position context
            sb.AppendLine("📊 Game Context:");
            sb.AppendLine($"   • Legal moves available: {board.GetLegalMoves().Length}");
            sb.AppendLine($"   • In check: {(board.IsInCheck() ? "YES" : "No")}");

            // Count material
            int whiteMaterial = 0, blackMaterial = 0;
            for (int i = 0; i < 64; i++)
            {
                var p = board.GetPiece(new Square(i));
                if (!p.IsNull && p.PieceType != PieceType.King)
                {
                    int value = p.PieceType switch
                    {
                        PieceType.Pawn => 1,
                        PieceType.Knight => 3,
                        PieceType.Bishop => 3,
                        PieceType.Rook => 5,
                        PieceType.Queen => 9,
                        _ => 0
                    };
                    if (p.IsWhite) whiteMaterial += value;
                    else blackMaterial += value;
                }
            }
            sb.AppendLine($"   • Material count: White={whiteMaterial}, Black={blackMaterial}");

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("🎯 YOUR TASK:");
            sb.AppendLine("Explain this move in 3-4 sentences covering:");
            sb.AppendLine();
            sb.AppendLine("1. WHAT does this move accomplish? (tactical/strategic goal)");
            sb.AppendLine("2. WHAT is the follow-up plan after this move?");
            sb.AppendLine("   → Is it setting up an attack on the king?");
            sb.AppendLine("   → Is it improving piece development?");
            sb.AppendLine("   → Is it defending against a threat?");
            sb.AppendLine("   → Is it preparing a pawn break or positional advantage?");
            sb.AppendLine("3. WHY is this position now favorable?");
            sb.AppendLine();
            sb.AppendLine("Be specific, confident, and insightful like a grandmaster commentator.");

            return sb.ToString();
        }
    }
}
