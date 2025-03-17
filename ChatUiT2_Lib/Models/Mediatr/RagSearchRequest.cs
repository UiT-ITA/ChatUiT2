using MediatR;

namespace ChatUiT2.Models.Mediatr;

public class RagSearchRequest : IRequest<List<RagSearchResult>>
{
    public RagSearchRequest(ChatUiT2.Models.RagProject.RagProject ragProject, string searchTerm, int numResults, double minMatchScore)
    {
        RagProject = ragProject;
        SearchTerm = searchTerm;
        NumResults = numResults;
        MinMatchScore = minMatchScore;
    }
    public ChatUiT2.Models.RagProject.RagProject RagProject { get; set; }
    public string SearchTerm { get; set; }
    public int NumResults { get; set; }
    public double MinMatchScore { get; set; }
}
