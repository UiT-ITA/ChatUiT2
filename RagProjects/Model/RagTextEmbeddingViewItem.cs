using ChatUiT2_Classlib.Model.RagProject;

namespace RagProjects.Model;

public class RagTextEmbeddingViewItem
{
    public RagTextEmbedding? RagTextEmbedding { get; set; }
    public bool IsEditing { get; set; }
    public double MatchScore { get; set; }
}
