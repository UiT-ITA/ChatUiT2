using MediatR;

namespace ChatUiT2.Models.Mediatr;

public class GetRagProjectByNameRequest : IRequest<ChatUiT2.Models.RagProject.RagProject?>
{
    public GetRagProjectByNameRequest(string projectName, bool loadItems)
    {
        ProjectName = projectName;
        LoadItems = loadItems;
    }
    public string ProjectName { get; set; }
    public bool LoadItems { get; set; }
}
