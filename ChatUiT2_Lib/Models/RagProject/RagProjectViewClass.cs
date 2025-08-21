namespace ChatUiT2.Models.RagProject;

public class RagProjectViewClass
{
    public RagProjectViewClass(RagProject ragProject)
    {
        RagProject = ragProject;
        ContentItemsCount = ragProject.ContentItems?.Count ?? 0;
    }

    public RagProject RagProject { get; set; }
    
    public int ContentItemsCount { get; set; }
}
