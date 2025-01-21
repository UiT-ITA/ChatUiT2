using ChatUiT2_Classlib.Model.Topdesk;

namespace RagProjects.Model;

public class TopdeskTextEmbeddingViewItem
{
    public TopdeskTextEmbedding? TopdeskTextEmbedding { get; set; }
    public bool IsEditing { get; set; }
    public double MatchScore { get; set; }
}
