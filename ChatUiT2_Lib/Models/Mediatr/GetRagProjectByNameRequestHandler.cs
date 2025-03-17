using ChatUiT2.Interfaces;
using MediatR;

namespace ChatUiT2.Models.Mediatr;

public class GetRagProjectByNameRequestHandler : IRequestHandler<GetRagProjectByNameRequest, ChatUiT2.Models.RagProject.RagProject?>
{
    private readonly IRagDatabaseService _ragDatabaseService;

    public GetRagProjectByNameRequestHandler(IRagDatabaseService ragDatabaseService)
    {
        this._ragDatabaseService = ragDatabaseService;
    }
    public async Task<ChatUiT2.Models.RagProject.RagProject?> Handle(GetRagProjectByNameRequest request, CancellationToken cancellationToken)
    {
        return await _ragDatabaseService.GetRagProjectByName(request.ProjectName, request.LoadItems);
    }
}
