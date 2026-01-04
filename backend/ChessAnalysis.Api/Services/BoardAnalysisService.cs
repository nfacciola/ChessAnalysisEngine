using ChessAnalysis.Api.Models.Analysis;
using ChessDotNet;
using ChessDotNet.Pieces;

namespace ChessAnalysis.Api.Services;

public class BoardAnalysisService
{
    public BoardContext Analyze(string fen)
    {
        var game = new ChessGame(fen);
        var playerToMove = game.WhoseTurn;

        return new BoardContext
        {
            Fen = fen,
            Turn = playerToMove.ToString(),
            Material = GetMaterialData(game),
            Geometry = GetGeometryData(game)
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