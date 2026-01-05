using ChessAnalysis.Api.Models.Analysis;
using ChessDotNet;
using ChessDotNet.Pieces;

namespace ChessAnalysis.Api.Services;

public class BoardAnalysisService
{
    public BoardContext Analyze(string fen)
    {
        var game = new ChessGame(fen);
        var player = game.WhoseTurn; // Side to move NEXT (Opponent of who just moved)
        var justMovedPlayer = player == Player.White ? Player.Black : Player.White;

        return new BoardContext
        {
            Fen = fen,
            Turn = player.ToString(),
            Material = GetMaterialData(game),
            Geometry = GetGeometryData(game),
            WhiteKingSafety = GetKingSafetyData(game, Player.White),
            BlackKingSafety = GetKingSafetyData(game, Player.Black),
            Tactics = GetTacticsData(game, justMovedPlayer) // Analyze what the player just did
        };
    }

    private KingSafetyData GetKingSafetyData(ChessGame game, Player player)
    {
        // 1. Check if King is attacked
        bool isCheck = game.IsInCheck(player);

        // 2. Check if specific Castling moves are strictly legal right now
        // ChessDotNet usually represents castling moves as King moving 2 squares
        // White: e1->g1 (Kingside), e1->c1 (Queenside)
        // Black: e8->g8 (Kingside), e8->c8 (Queenside)

        var validMoves = game.GetValidMoves(player);
        bool canCastleKing = false;
        bool canCastleQueen = false;

        foreach (var move in validMoves)
        {
            // Simple heuristic: Detect castling by distance. 
            // The library might explicitly mark them, but move logic is safest.
            var diff = Math.Abs((int)move.NewPosition.File - (int)move.OriginalPosition.File);
            var isKing = game.GetPieceAt(move.OriginalPosition) is King;

            if (isKing && diff == 2)
            {
                if (move.NewPosition.File == ChessDotNet.File.G) canCastleKing = true;
                if (move.NewPosition.File == ChessDotNet.File.C) canCastleQueen = true;
            }
        }

        return new KingSafetyData
        {
            IsInCheck = isCheck,
            CanCastleKingside = canCastleKing,
            CanCastleQueenside = canCastleQueen
        };
    }

    private TacticsData GetTacticsData(ChessGame game, Player attacker)
    {
        var attacks = new List<string>();

        // FIX: ChessDotNet returns 0 moves if we ask for the player whose turn it ISN'T.
        // We must perform the analysis on a temporary game state where it IS their turn.
        ChessGame searchGame = game;

        if (game.WhoseTurn != attacker)
        {
            // 1. Get current FEN
            var fen = game.GetFen();
            var fenParts = fen.Split(' ');

            // 2. Flip the active color (w <-> b)
            fenParts[1] = attacker == Player.White ? "w" : "b";

            // 3. Create a temporary game state for calculation
            var modifiedFen = string.Join(" ", fenParts);
            searchGame = new ChessGame(modifiedFen);
        }

        // Scan the SEARCH GAME for threats
        for (int r = 0; r < 8; r++)
        {
            for (int f = 0; f < 8; f++)
            {
                var fileChar = (char)('a' + f);
                var rankNum = r + 1;
                var originStr = $"{fileChar}{rankNum}";
                var origin = new Position(originStr);

                var piece = searchGame.GetPieceAt(origin);

                // Ensure we are looking at the Attacker's pieces
                if (piece != null && piece.Owner == attacker)
                {
                    // Now this returns moves because we flipped the turn!
                    var moves = searchGame.GetValidMoves(origin);

                    foreach (var move in moves)
                    {
                        var targetPiece = searchGame.GetPieceAt(move.NewPosition);

                        // If move captures an enemy -> It is a Direct Attack
                        if (targetPiece != null && targetPiece.Owner != attacker)
                        {
                            attacks.Add($"{piece.ToString()} at {originStr} attacks {targetPiece.ToString()} at {move.NewPosition.ToString().ToLower()}");
                        }
                    }
                }
            }
        }

        return new TacticsData
        {
            DirectAttacks = attacks
        };
    }

    private MaterialData GetMaterialData(ChessGame game)
    {
        var values = new Dictionary<Type, int>
        {
            { typeof(Pawn), 1 }, { typeof(Knight), 3 },
            { typeof(Bishop), 3 }, { typeof(Rook), 5 },
            { typeof(Queen), 9 },
            { typeof(King), 0 }
        };

        int balance = 0;

        // Iterate using simple integers 0-7
        for (int r = 0; r < 8; r++)
        {
            for (int f = 0; f < 8; f++)
            {
                // TRICK: Construct the string coordinate manually (e.g., "e4")
                // 'a' + 0 = 'a', 'a' + 1 = 'b', etc.
                int rankNum = r + 1;
                var piece = game.GetPieceAt(new Position((ChessDotNet.File)f, rankNum));

                if (piece != null)
                {
                    int val = 0;
                    if (values.TryGetValue(piece.GetType(), out int baseVal))
                    {
                        val = baseVal;
                    }

                    if (piece.Owner == Player.White) balance += val;
                    else balance -= val;
                }
            }
        }

        return new MaterialData
        {
            MaterialBalance = balance,
            Imbalances = new List<string>()
        };
    }

    private GeometryData GetGeometryData(ChessGame game)
    {
        // Use strings directly to define center squares
        var centerSquares = new[] { "e4", "d4", "e5", "d5" };

        int whiteCenterPresence = 0;
        int blackCenterPresence = 0;

        foreach (var square in centerSquares)
        {
            var piece = game.GetPieceAt(new Position(square));

            if (piece != null)
            {
                if (piece.Owner == Player.White) whiteCenterPresence++;
                if (piece.Owner == Player.Black) blackCenterPresence++;
            }
        }

        return new GeometryData
        {
            WhiteControlsCenter = whiteCenterPresence >= 2,
            BlackControlsCenter = blackCenterPresence >= 2,
            OpenFiles = new List<string>()
        };
    }
}