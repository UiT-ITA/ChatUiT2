using MediatR;
using OpenAI.Embeddings;

namespace ChatUiT2.Models.Mediatr;
public class EmbeddingForTextRequest : IRequest<OpenAIEmbedding>
{
    public EmbeddingForTextRequest(string textToEmbed, AiModel aiModel)
    {
        this.TextToEmbed = textToEmbed;
        this.AiModel = aiModel;
    }
    public string TextToEmbed { get; set; }
    public AiModel AiModel { get; set; }
}
