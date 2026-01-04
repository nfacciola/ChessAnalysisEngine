using ChessAnalysis.Api.Models.Analysis;
using ChessDotNet;
using ChessDotNet.Pieces;

namespace ChessAnalysis.Api.Services;

public class BoardAnalysisService
{
    public BoardContext Analyze(string fen)
    {
        var game = new ChessGame(fen);
        var player = game.WhoseTurn;

        return new BoardContext
        {
            Fen = fen,
            Turn = player.ToString(),
            Material = GetMaterialData(game),
            Geometry = GetGeometryData(game),
            KingSafety = GetKingSafetyData(game, player)
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