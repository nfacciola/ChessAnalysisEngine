namespace ChessAnalysis.Api.Dto;

public record ExplainRequest(
    string Fen,
    string MoveSan,
    string BestMoveSan,
    string Label,
    double ScoreBefore,
    double ScoreAfter
);

public record ExplainResponse(string Explanation);