namespace ChessAnalysis.Api.Models.Analysis;

public class BoardContext
{
    public string Fen { get; set; } = string.Empty;
    public string Turn { get; set; } = string.Empty;

    // The "Modules" of the context
    public MaterialData Material { get; set; } = new();
    public GeometryData Geometry { get; set; } = new();
    public KingSafetyData WhiteKingSafety { get; set; } = new();
    public KingSafetyData BlackKingSafety { get; set; } = new();
    public TacticsData Tactics { get; set; } = new();
}

public class MaterialData
{
    public int MaterialBalance { get; set; } // +ve for White, -ve for Black
    public List<string> Imbalances { get; set; } = new(); // e.g. "White Bishop Pair"
}

public class GeometryData
{
    public bool WhiteControlsCenter { get; set; }
    public bool BlackControlsCenter { get; set; }
    public List<string> OpenFiles { get; set; } = new();
}

public class KingSafetyData
{
    public bool CanCastleKingside { get; set; }
    public bool CanCastleQueenside { get; set; }
    public bool IsInCheck { get; set; }
}

public class TacticsData
{
    public List<string> DirectAttacks { get; set; } = new();
    public List<string> PinnedPieces { get; set; } = new();
}