namespace ChatUiT2.Models;

public class RagSearchResult
{
    public string EmbeddingText { get; set; }
    public double MatchScore { get; set; }
    public string Source { get; set; }
    public string SourceId { get; set; }
    public string SourceAltId { get; set; }
    public string ContentUrl { get; set; }

    public string SourceContent { get; set; }
}