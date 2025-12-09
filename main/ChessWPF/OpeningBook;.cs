using ChessChallenge.API;
using System.Collections.Generic;

namespace SystemHelper
{
    public class OpeningBook
    {
        private Dictionary<string, string> bookMoves;

        public OpeningBook()
        {
            bookMoves = new Dictionary<string, string>();
            InitializeRepertoire();
        }

        public bool TryGetBookMove(Board board, out string move)
        {
            // We use the FEN string up to the move counters to identify the position
            // Standard FEN: rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1
            // We strip the last two numbers (halfmove/fullmove) to handle transpositions better if needed,
            // though strict FEN matching is usually safer for specific opening lines.

            string fen = board.GetFenString();

            // Normalize FEN: Remove move counters for broader matching (optional but recommended)
            // Example: "rnbqkbnr... - 0 1" -> "rnbqkbnr... -"
            string key = StripMoveCounters(fen);

            if (bookMoves.TryGetValue(key, out string bookMove))
            {
                move = bookMove;
                return true;
            }

            move = "";
            return false;
        }

        private string StripMoveCounters(string fen)
        {
            string[] parts = fen.Split(' ');
            if (parts.Length >= 4)
            {
                // Return FEN including Castling rights and En Passant square
                return $"{parts[0]} {parts[1]} {parts[2]} {parts[3]}";
            }
            return fen;
        }

        private void Add(string fenKey, string move)
        {
            // The FENs below are simplified (stripped of move counters)
            if (!bookMoves.ContainsKey(fenKey))
            {
                bookMoves.Add(fenKey, move);
            }
        }

        private void InitializeRepertoire()
        {
            // --- STARTING POSITION ---
            Add("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq -", "e2e4");

            // ==========================================================
            // WHITE REPERTOIRE: Ruy Lopez (Spanish)
            // Target: 1. e4 e5 2. Nf3 Nc6 3. Bb5
            // ==========================================================

            // 1. e4 ...
            Add("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq -", "e7e5"); // Assuming opponent plays e5 (Main line)

            // 1. e4 e5 2. Nf3
            Add("rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq -", "g1f3");

            // 1. e4 e5 2. Nf3 Nc6 3. Bb5 (Ruy Lopez)
            Add("r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq -", "f1b5");

            // 3... a6 4. Ba4 (Main Line)
            Add("r1bqkbnr/1ppp1ppp/p1n5/4p3/1B2P3/5N2/PPPP1PPP/RNBQK2R b KQkq -", "b5a4"); // Black POV handling if needed, but this is White Repertoire
            Add("r1bqkbnr/1ppp1ppp/p1n5/4p3/1B2P3/5N2/PPPP1PPP/RNBQK2R w KQkq -", "b5a4");

            // 3... Nf6 4. O-O (Berlin Defense response)
            Add("r1bqkb1r/pppp1ppp/2n2n2/1B2p3/4P3/5N2/PPPP1PPP/RNBQK2R w KQkq -", "e1g1");

            // 3... d6 4. d4 (Steinitz Defense)
            Add("r1bqkbnr/ppp2ppp/2np4/1B2p3/4P3/5N2/PPPP1PPP/RNBQK2R w KQkq -", "d2d4");

            // ==========================================================
            // BLACK REPERTOIRE
            // 1. Petroff Defense (against 1. e4)
            // 2. Nimzo-Indian / QGD (against 1. d4)
            // ==========================================================

            // --- Case A: Opponent plays 1. e4 ---
            // Response: 1... e5 (Petroff setup)
            Add("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq -", "e7e5");

            // If 2. Nf3, play 2... Nf6 (Petroff Defense)
            Add("rnbqkbnr/pppp1ppp/8/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq -", "g8f6");

            // Petroff: 3. Nxe5 d6 (Standard Main Line)
            Add("rnbqkb1r/pppp1ppp/5n2/4N3/4P3/8/PPPP1PPP/RNBQKB1R b KQkq -", "d7d6");

            // Petroff: 3. d4 Nxe4 (Steinitz Variation)
            Add("rnbqkb1r/pppp1ppp/5n2/3Pp3/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq -", "f6e4");


            // --- Case B: Opponent plays 1. d4 ---
            // Response: 1... Nf6 (Flexible, leads to Nimzo)
            Add("rnbqkbnr/pppppppp/8/8/3P4/8/PPPP1PPP/RNBQKBNR b KQkq -", "g8f6");

            // If 2. c4, play 2... e6 (Setup for Nimzo or QGD)
            Add("rnbqkb1r/pppppppp/5n2/8/2PP4/8/PP2PPPP/RNBQKBNR b KQkq -", "e7e6");

            // --- Path B1: Nimzo-Indian (3. Nc3 Bb4) ---
            // Position after 1. d4 Nf6 2. c4 e6 3. Nc3
            Add("rnbqkb1r/pppp1ppp/4pn2/8/2PP4/2N5/PP2PPPP/R1BQKBNR b KQkq -", "f8b4");

            // --- Path B2: QGD (3. Nf3 d5) ---
            // Position after 1. d4 Nf6 2. c4 e6 3. Nf3
            // Note: 3. Nf3 avoids Nimzo. We switch to QGD structure.
            Add("rnbqkb1r/pppp1ppp/4pn2/8/2PP4/5N2/PP2PPPP/RNBQKB1R b KQkq -", "d7d5");

            // QGD Response to Catalans or other setups usually involves d5 or Be7

            // --- Case C: Opponent plays 1. c4 (English) ---
            // Response: 1... e5 (Reversed Sicilian) or 1... Nf6
            Add("rnbqkbnr/pppppppp/8/8/2P5/8/PP1PPPPP/RNBQKBNR b KQkq -", "e7e5");

            // --- Case D: Opponent plays 1. Nf3 (Reti) ---
            // Response: 1... d5
            Add("rnbqkbnr/pppppppp/8/8/8/5N2/PPPPPPPP/RNBQKB1R b KQkq -", "d7d5");
        }
    }
}
