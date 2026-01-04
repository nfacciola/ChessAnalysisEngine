using System.Text.RegularExpressions;

namespace ChessAnalysis.Api.Services;

public static class StockfishResultParser
{
    public record AnalysisResult(int? Cp, int? Mate, string? BestMove, List<string> Pv);

    public static AnalysisResult Parse(string rawOutput)
    {
        int? cp = null;
        int? mate = null;
        string? bestMove = null;
        List<string> pv = new();

        var lines = rawOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("info"))
            {
                // Score parsing
                var cpMatch = Regex.Match(line, @"score cp (-?\d+)");
                if (cpMatch.Success) cp = int.Parse(cpMatch.Groups[1].Value);

                var mateMatch = Regex.Match(line, @"score mate (-?\d+)");
                if (mateMatch.Success) mate = int.Parse(mateMatch.Groups[1].Value);

                // Principal Variation (PV) parsing
                var pvIndex = line.IndexOf(" pv ");
                if (pvIndex != -1)
                {
                    pv = line[(pvIndex + 4)..]
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Where(t => t.Length >= 4)
                        .ToList();
                }
            }

            if (line.StartsWith("bestmove"))
            {
                bestMove = line.Split(' ')[1];
            }
        }

        return new AnalysisResult(cp, mate, bestMove, pv);
    }
}