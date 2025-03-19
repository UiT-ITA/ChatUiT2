namespace ChatUiT2.Models;

public class RagSearchResult
{
    public string EmbeddingText { get; set; } = string.Empty;
    public double MatchScore { get; set; }
    public string Source { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string SourceAltId { get; set; } = string.Empty;
    public string ContentUrl { get; set; } = string.Empty;
    public string ContentTitle { get; set; } = string.Empty;

    public string SourceContent { get; set; } = string.Empty;
}