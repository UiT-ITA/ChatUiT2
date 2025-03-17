using ChatUiT2.Interfaces;
using MediatR;

namespace ChatUiT2.Models.Mediatr;

public class RagSearchRequestHandler : IRequestHandler<RagSearchRequest, List<RagSearchResult>>
{
    private readonly IRagDatabaseService _ragDatabaseService;

    public RagSearchRequestHandler(IRagDatabaseService ragDatabaseService)
    {
        this._ragDatabaseService = ragDatabaseService;
    }
    public async Task<List<RagSearchResult>> Handle(RagSearchRequest request, CancellationToken cancellationToken)
    {
        return await _ragDatabaseService.DoGenericRagSearch(request.RagProject, request.SearchTerm, request.NumResults, request.MinMatchScore);
    }
}
