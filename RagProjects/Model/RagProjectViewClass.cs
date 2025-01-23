using ChatUiT2_Classlib.Model.RagProject;

namespace RagProjects.Model;

public class RagProjectViewClass
{
    public RagProjectViewClass(RagProject ragProject)
    {
        RagProject = ragProject;
    }

    public RagProject RagProject { get; set; }
}
