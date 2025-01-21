namespace ChatUiT2_Classlib.Model;

public class RagSearchResult
{
    public string Text { get; set; }
    public double MatchScore { get; set; }
    public string Source { get; set; }
    public string SourceId { get; set; }
    public string ContentUrl { get; set; }
}