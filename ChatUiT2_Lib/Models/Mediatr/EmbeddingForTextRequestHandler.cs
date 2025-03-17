using ChatUiT2.Interfaces;
using MediatR;
using OpenAI.Embeddings;

namespace ChatUiT2.Models.Mediatr;
public class EmbeddingForTextRequestHandler : IRequestHandler<EmbeddingForTextRequest, OpenAIEmbedding>
{
    private readonly IChatService _chatService;

    public EmbeddingForTextRequestHandler(IChatService chatService)
    {
        _chatService = chatService;
    }
    public async Task<OpenAIEmbedding> Handle(EmbeddingForTextRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(1000);
        //return await _chatService.GetEmbedding(request.TextToEmbed, request.AiModel);
        return null;
    }
}
