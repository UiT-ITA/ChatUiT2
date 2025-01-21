using UiT.RestClientTopdesk.Model;

namespace RagProjects.Model;

public class KnowledgeItemViewClass
{
    public bool IsInVectorDb { get; set; }
    /// <summary>
    /// True if the article in vector db has same values as the knowledge item
    /// </summary>
    public bool VectorDbInSync { get; set; }
    public KnowledgeItem? KnowledgeItem { get; set; }

}
