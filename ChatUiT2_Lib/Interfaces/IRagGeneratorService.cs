using ChatUiT2.Models;
using ChatUiT2.Models.RagProject;
using OpenAI.Embeddings;

namespace ChatUiT2.Interfaces
{
    public interface IRagGeneratorService
    {
        Task<QuestionsFromTextResult?> GenerateQuestionsFromContent(string content, int numToGenerateMin = 5, int numToGenerateMax = 20);
        Task GenerateRagParagraphsFromContent(RagProject ragProject, ContentItem item, int minParagraphSize = 150);
        Task GenerateRagQuestionsFromContent(RagProject ragProject, ContentItem item);
        Task<OpenAIEmbedding> GetEmbeddingForText(string text, string username);
    }
}