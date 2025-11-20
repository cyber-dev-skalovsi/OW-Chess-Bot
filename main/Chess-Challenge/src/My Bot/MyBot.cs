using ChessChallenge.API;
using System;
using static System.Math;
using static ChessChallenge.API.BitboardHelper;

public class MyBot : IChessBot
{
    public int maxDepth = 999; // #DEBUG

    public void SetMaxDepth(int depth)
    {
        maxDepth = Math.Clamp(depth, 1, 200); // Limit between 1 and 200
    }

    public ulong nodes = 0; // #DEBUG
    public int maxSearchTime, searchingDepth, lastScore;

    Timer timer;
    Board board;

    Move searchBestMove, rootBestMove;

    // #ANALYSIS - Track evaluation components
    public EvalComponents lastEvalComponents;
    public bool trackEvalComponents = false;

    // this tuple is 24 bytes, so the transposition table is precisely 192MiB (~201 MB)
    readonly (
        ulong, // hash
        ushort, // moveRaw
        int, // score
        int, // depth
        int // bound BOUND_EXACT=[1, 2147483647), BOUND_LOWER=2147483647, BOUND_UPPER=0
    )[] transpositionTable = new (ulong, ushort, int, int, int)[0x800000];

    // piece-to history tables, per-color
    readonly int[,,] history = new int[2, 7, 64];

    readonly ulong[] packedData = {
        0x0000000000000000, 0x2328170f2d2a1401, 0x1f1f221929211507, 0x18202a1c2d261507,
        0x252e3022373a230f, 0x585b47456d65321c, 0x8d986f66a5a85f50, 0x0002000300070005,
        0xfffdfffd00060001, 0x2b1f011d20162306, 0x221c0b171f15220d, 0x1b1b131b271c1507,
        0x232d212439321f0b, 0x5b623342826c2812, 0x8db65b45c8c01014, 0x0000000000000000,
        0x615a413e423a382e, 0x6f684f506059413c, 0x82776159705a5543, 0x8b8968657a6a6150,
        0x948c7479826c6361, 0x7e81988f73648160, 0x766f7a7e70585c4e, 0x6c7956116e100000,
        0x3a3d2d2840362f31, 0x3c372a343b3a3838, 0x403e2e343c433934, 0x373e3b2e423b2f37,
        0x383b433c45433634, 0x353d4b4943494b41, 0x46432e354640342b, 0x55560000504f0511,
        0x878f635c8f915856, 0x8a8b5959898e5345, 0x8f9054518f8e514c, 0x96985a539a974a4c,
        0x9a9c67659e9d5f59, 0x989c807a9b9c7a6a, 0xa09f898ba59c6f73, 0xa1a18386a09b7e84,
        0xbcac7774b8c9736a, 0xbab17b7caebd7976, 0xc9ce7376cac57878, 0xe4de6f70dcd87577,
        0xf4ef7175eedc7582, 0xf9fa8383dfe3908e, 0xfffe7a81f4ec707f, 0xdfe79b94e1ee836c,
        0x2027252418003d38, 0x4c42091d31193035, 0x5e560001422c180a, 0x6e6200004d320200,
        0x756c000e5f3c1001, 0x6f6c333f663e3f1d, 0x535b55395c293c1b, 0x2f1e3d5e22005300,
        0x004c0037004b001f, 0x00e000ca00be00ad, 0x02e30266018800eb, 0xffdcffeeffddfff3,
        0xfff9000700010007, 0xffe90003ffeefff4, 0x00000000fff5000d,
    };

    // bitshift amount is implicitly modulo 64, also used in pst part of eval function
    int EvalWeight(int item) => (int)(packedData[item >> 1] >> item * 32);

    public Move Think(Board boardOrig, Timer timerOrig)
    {
        nodes = 0; // #DEBUG
        board = boardOrig;
        timer = timerOrig;

        maxSearchTime = timer.MillisecondsRemaining / 4;
        searchingDepth = 1;
        do
            try
            {
                // Aspiration windows
                if (Abs(lastScore - Negamax(lastScore - 20, lastScore + 20, searchingDepth)) >= 20)
                    Negamax(-32000, 32000, searchingDepth);
                rootBestMove = searchBestMove;
                Console.WriteLine( // #DEBUG
                    "info depth {0} time {1} nodes {2} pv {3} score cp {4} nps {5}", // #DEBUG
                    searchingDepth, // #DEBUG
                    timer.MillisecondsElapsedThisTurn, // #DEBUG
                    nodes, // #DEBUG
                    ChessChallenge.Chess.MoveUtility.GetMoveNameUCI(new(rootBestMove.RawValue)), // #DEBUG
                    lastScore, // #DEBUG
                    nodes * 1000 / (ulong)Max(timer.MillisecondsElapsedThisTurn, 1) // #DEBUG
                ); // #DEBUG
            }
            catch
            {
                // out of time
                break;
            }
        while (
            ++searchingDepth <= 200
                && searchingDepth <= maxDepth // #DEBUG
                && timer.MillisecondsElapsedThisTurn < maxSearchTime / 10
        );

        return rootBestMove;
    }

    public int Negamax(int alpha, int beta, int depth)
    {
        // abort search if out of time, but we must search at least depth 1
        if (timer.MillisecondsElapsedThisTurn >= maxSearchTime && searchingDepth > 1)
            throw null;

        nodes++; // #DEBUG

        ref var tt = ref transpositionTable[board.ZobristKey & 0x7FFFFF];
        var (ttHash, ttMoveRaw, score, ttDepth, ttBound) = tt;

        bool
            ttHit = ttHash == board.ZobristKey,
            nonPv = alpha + 1 == beta,
            inQSearch = depth <= 0,
            pieceIsWhite;
        int
            eval = 0x000b000a, // tempo
            bestScore = board.PlyCount - 30000,
            oldAlpha = alpha,

            // search loop vars
            moveCount = 0, // quietsToCheckTable = [0, 4, 5, 10, 23]
            quietsToCheck = 0b_010111_001010_000101_000100_000000 >> depth * 6 & 0b111111,

            // temp vars
            tmp = 0;
        if (ttHit)
        {
            if (ttDepth >= depth && ttBound switch
            {
                2147483647 /* BOUND_LOWER */ => score >= beta,
                0 /* BOUND_UPPER */ => score <= alpha,
                // exact cutoffs at pv nodes causes problems, but need it in qsearch for matefinding
                _ /* BOUND_EXACT */ => nonPv || inQSearch,
            })
                return score;
        }
        else if (depth > 3)
            // Internal iterative reduction
            depth--;

        // this is a local function because the C# JIT doesn't optimize very large functions well
        // we do packed phased evaluation, so weights are of the form (eg << 16) + mg
        int Eval(ulong pieces)
        {
            // #ANALYSIS - Initialize component tracking
            EvalComponents components = trackEvalComponents ? new EvalComponents() : null;

            // use tmp as phase (initialized above)
            while (pieces != 0)
            {
                int pieceType, sqIndex;
                Piece piece = board.GetPiece(new(sqIndex = ClearAndGetIndexOfLSB(ref pieces)));
                pieceType = (int)piece.PieceType;

                // virtual pawn type
                // consider pawns on the opposite half of the king as distinct piece types (piece 0)
                int virtualPieceType = pieceType;
                pieceType -= (sqIndex & 0b111 ^ board.GetKingSquare(pieceIsWhite = piece.IsWhite).File) >> 1 >> pieceType;

                int materialValue = EvalWeight(112 + pieceType);
                int pstValue = (int)(
                    packedData[pieceType * 64 + sqIndex >> 3 ^ (pieceIsWhite ? 0 : 0b111)]
                        >> (0x01455410 >> sqIndex * 4) * 8
                        & 0xFF00FF
                );

                int mobilityCount = GetNumberOfSetBits(
                    GetSliderAttacks((PieceType)Min(5, pieceType), new(sqIndex), board)
                );
                int mobilityValue = EvalWeight(11 + pieceType) * mobilityCount;

                int pawnAheadCount = GetNumberOfSetBits(
                    (pieceIsWhite ? 0x0101010101010100UL << sqIndex : 0x0080808080808080UL >> 63 - sqIndex)
                        & board.GetPieceBitboard(PieceType.Pawn, pieceIsWhite)
                );
                int pawnAheadValue = EvalWeight(118 + pieceType) * pawnAheadCount;

                sqIndex = materialValue + pstValue + mobilityValue + pawnAheadValue;

                int sign = pieceIsWhite == board.IsWhiteToMove ? 1 : -1;
                eval += pieceIsWhite == board.IsWhiteToMove ? sqIndex : -sqIndex;

                // #ANALYSIS - Track components
                if (trackEvalComponents)
                {
                    components.Material += materialValue * sign;
                    components.PieceSquareTables += pstValue * sign;
                    components.Mobility += mobilityValue * sign;
                    components.PawnStructure += pawnAheadValue * sign;

                    // Track per-piece-type contributions
                    components.AddPieceContribution(virtualPieceType, pieceIsWhite, sqIndex * sign);

                    // Track mobility per piece type
                    components.AddMobilityContribution(virtualPieceType, pieceIsWhite, mobilityCount * sign);
                }

                // phaseWeightTable = [0, 0, 1, 1, 2, 4, 0]
                tmp += 0x0421100 >> pieceType * 4 & 0xF;
            }

            // the correct way to extract EG eval is (eval + 0x8000) >> 16, but this is shorter and
            // the off-by-one error is insignificant
            // the division is also moved outside Eval to save a token
            int finalEval = (short)eval * tmp + eval / 0x10000 * (24 - tmp);

            // #ANALYSIS - Store components
            if (trackEvalComponents)
            {
                components.Phase = tmp;
                components.Tempo = 0x000b000a;
                components.TotalMidgame = (short)eval;
                components.TotalEndgame = eval / 0x10000;
                components.FinalScore = finalEval / 24;
                lastEvalComponents = components;
            }

            return finalEval;
            // end tmp use
        }
        // using tteval in qsearch causes matefinding issues
        eval = ttHit && !inQSearch ? score : Eval(board.AllPiecesBitboard) / 24;

        if (inQSearch)
            // stand pat in quiescence search
            alpha = Max(alpha, bestScore = eval);
        else if (nonPv && eval >= beta && board.TrySkipTurn())
        {
            // Pruning based on null move observation
            bestScore = depth <= 4
                // Reverse Futility Pruning
                ? eval - 58 * depth
                // Adaptive Null Move Pruning
                : -Negamax(-beta, -alpha, (depth * 100 + beta - eval) / 186 - 1);
            board.UndoSkipTurn();
        }
        if (bestScore >= beta)
            return bestScore;

        if (board.IsInStalemate())
            return 0;

        var moves = board.GetLegalMoves(inQSearch);
        var scores = new int[moves.Length];
        // use tmp as scoreIndex
        tmp = 0;
        foreach (Move move in moves)
            // move ordering:
            // 1. hashmove
            // 2. captures (ordered by most valuable victim, least valuable attacker)
            // 3. quiets (ordered by history)
            scores[tmp++] -= ttHit && move.RawValue == ttMoveRaw ? 1000000
                : Max(
                    (int)move.CapturePieceType * 32768 - (int)move.MovePieceType - 16384,
                    HistoryValue(move)
                );
        // end tmp use

        Array.Sort(scores, moves);
        Move bestMove = default;
        foreach (Move move in moves)
        {
            // Delta pruning
            // deltas = [180, 390, 442, 718, 1332]
            // due to sharing of the top bit of each entry with the bottom bit of the next one
            // (expands the range of values for the queen) all deltas must be even (except pawn)
            if (inQSearch && eval + (0b1_0100110100_1011001110_0110111010_0110000110_0010110100_0000000000 >> (int)move.CapturePieceType * 10 & 0b1_11111_11111) <= alpha)
                break;

            board.MakeMove(move);
            int
                // Check extension
                nextDepth = board.IsInCheck() ? depth : depth - 1,
                reduction = (depth - nextDepth) * Max(
                    // Late move reduction
                    (moveCount * 93 + depth * 144) / 1000
                        // History reduction
                        + scores[moveCount] / 172,
                    0
                );
            if (board.IsRepeatedPosition())
                score = 0;
            else
            {
                // this crazy while loop does the null window searches for PVS: first it searches
                // with the reduced depth, and if it beats alpha it re-searches at full depth
                // ~alpha is equivalent to -alpha-1 under two's complement
                while (
                    moveCount != 0
                        && (score = -Negamax(~alpha, -alpha, nextDepth - reduction)) > alpha
                        && reduction != 0
                )
                    reduction = 0;
                if (moveCount == 0 || score > alpha)
                    score = -Negamax(-beta, -alpha, nextDepth);
            }

            board.UndoMove(move);

            if (score > bestScore)
            {
                alpha = Max(alpha, bestScore = score);
                bestMove = move;
            }
            if (score >= beta)
            {
                if (!move.IsCapture)
                {
                    // use tmp as change
                    // Increased history change when eval < alpha
                    // equivalent to tmp = eval < alpha ? -(depth + 1) : depth
                    // 1. eval - alpha is < 0 if eval < alpha and >= 0 otherwise
                    // 2. >> 31 maps numbers < 0 to -1 and numbers >= 0 to 0
                    // 3. -1 ^ depth = ~depth while 0 ^ depth = depth
                    // 4. ~depth = -depth - 1 = -(depth + 1)
                    // since we're squaring tmp, sign doesn't matter
                    tmp = eval - alpha >> 31 ^ depth;
                    tmp *= tmp;
                    foreach (Move malusMove in moves.AsSpan(0, moveCount))
                        if (!malusMove.IsCapture)
                            HistoryValue(malusMove) -= tmp + tmp * HistoryValue(malusMove) / 512;
                    HistoryValue(move) += tmp - tmp * HistoryValue(move) / 512;
                    // end tmp use
                }
                break;
            }

            // pruning techniques that break the move loop
            if (nonPv && depth <= 4 && !move.IsCapture && (
                // Late move pruning
                quietsToCheck-- == 1 ||
                // Futility pruning
                eval + 127 * depth < alpha
            ))
                break;

            moveCount++;
        }

        tt = (
            board.ZobristKey,
            alpha > oldAlpha // don't update best move if upper bound
                ? bestMove.RawValue
                : ttMoveRaw,
            Clamp(bestScore, -20000, 20000),
            Max(depth, 0),
            bestScore >= beta
                ? 2147483647 /* BOUND_LOWER */
                : alpha - oldAlpha /* BOUND_UPPER if alpha == oldAlpha else BOUND_EXACT */
        );

        searchBestMove = bestMove;
        return lastScore = bestScore;
    }

    ref int HistoryValue(Move move) => ref history[
        board.PlyCount & 1,
        (int)move.MovePieceType,
        move.TargetSquare.Index
    ];

    // #ANALYSIS - Public method to get detailed evaluation of a position
    public EvalComponents GetDetailedEval(Board evalBoard)
    {
        Board oldBoard = board;
        board = evalBoard;
        trackEvalComponents = true;
        lastEvalComponents = null;

        // Evaluate the position
        ulong pieces = board.AllPiecesBitboard;
        bool pieceIsWhite;
        int eval = 0x000b000a; // tempo
        int tmp = 0; // phase

        EvalComponents components = new EvalComponents();

        while (pieces != 0)
        {
            int pieceType, sqIndex;
            Piece piece = board.GetPiece(new(sqIndex = ClearAndGetIndexOfLSB(ref pieces)));
            pieceType = (int)piece.PieceType;

            int virtualPieceType = pieceType;
            pieceType -= (sqIndex & 0b111 ^ board.GetKingSquare(pieceIsWhite = piece.IsWhite).File) >> 1 >> pieceType;

            int materialValue = EvalWeight(112 + pieceType);
            int pstValue = (int)(
                packedData[pieceType * 64 + sqIndex >> 3 ^ (pieceIsWhite ? 0 : 0b111)]
                    >> (0x01455410 >> sqIndex * 4) * 8
                    & 0xFF00FF
            );

            int mobilityCount = GetNumberOfSetBits(
                GetSliderAttacks((PieceType)Min(5, pieceType), new(sqIndex), board)
            );
            int mobilityValue = EvalWeight(11 + pieceType) * mobilityCount;

            int pawnAheadCount = GetNumberOfSetBits(
                (pieceIsWhite ? 0x0101010101010100UL << sqIndex : 0x0080808080808080UL >> 63 - sqIndex)
                    & board.GetPieceBitboard(PieceType.Pawn, pieceIsWhite)
            );
            int pawnAheadValue = EvalWeight(118 + pieceType) * pawnAheadCount;

            int totalPieceValue = materialValue + pstValue + mobilityValue + pawnAheadValue;
            int sign = pieceIsWhite == board.IsWhiteToMove ? 1 : -1;

            eval += pieceIsWhite == board.IsWhiteToMove ? totalPieceValue : -totalPieceValue;

            components.Material += materialValue * sign;
            components.PieceSquareTables += pstValue * sign;
            components.Mobility += mobilityValue * sign;
            components.PawnStructure += pawnAheadValue * sign;
            components.AddPieceContribution(virtualPieceType, pieceIsWhite, totalPieceValue * sign);
            components.AddMobilityContribution(virtualPieceType, pieceIsWhite, mobilityCount * sign);

            tmp += 0x0421100 >> pieceType * 4 & 0xF;
        }

        int finalEval = (short)eval * tmp + eval / 0x10000 * (24 - tmp);

        components.Phase = tmp;
        components.Tempo = 0x000b000a;
        components.TotalMidgame = (short)eval;
        components.TotalEndgame = eval / 0x10000;
        components.FinalScore = finalEval / 24;

        trackEvalComponents = false;
        board = oldBoard;

        return components;
    }
}

// #ANALYSIS - Evaluation components tracking class
public class EvalComponents
{
    public int Material;
    public int PieceSquareTables;
    public int Mobility;
    public int PawnStructure;
    public int Tempo;
    public int Phase;
    public int TotalMidgame;
    public int TotalEndgame;
    public int FinalScore;

    // Per-piece-type tracking (indexed by PieceType enum)
    public int[] WhitePieceValues = new int[7];
    public int[] BlackPieceValues = new int[7];
    public int[] WhiteMobility = new int[7];
    public int[] BlackMobility = new int[7];

    public void AddPieceContribution(int pieceType, bool isWhite, int value)
    {
        if (isWhite)
            WhitePieceValues[pieceType] += value;
        else
            BlackPieceValues[pieceType] += value;
    }

    public void AddMobilityContribution(int pieceType, bool isWhite, int mobility)
    {
        if (isWhite)
            WhiteMobility[pieceType] += mobility;
        else
            BlackMobility[pieceType] += mobility;
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Final Score: {FinalScore}");
        sb.AppendLine($"Material: {Material}");
        sb.AppendLine($"PST: {PieceSquareTables}");
        sb.AppendLine($"Mobility: {Mobility}");
        sb.AppendLine($"Pawn Structure: {PawnStructure}");
        sb.AppendLine($"Tempo: {Tempo}");
        sb.AppendLine($"Phase: {Phase}/24");
        sb.AppendLine($"MG Eval: {TotalMidgame}");
        sb.AppendLine($"EG Eval: {TotalEndgame}");
        return sb.ToString();
    }
}