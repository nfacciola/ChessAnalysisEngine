namespace ChessAnalysis.Api.Models.Analysis;

public class BoardContext
{
    public string Fen { get; set; } = string.Empty;
    public string Turn { get; set; } = string.Empty;

    // The "Modules" of the context
    public MaterialData Material { get; set; } = new();
    public GeometryData Geometry { get; set; } = new();
    // TODO add StructureData, KingSafetyData later
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