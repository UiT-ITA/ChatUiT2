using ChatUiT2.Models;
using ChatUiT2.Models.RagProject;
using OpenAI.Embeddings;

namespace ChatUiT2.Interfaces;

public interface IRagSearchService
{
    Task<List<RagSearchResult>> DoGenericRagSearch(RagProject ragProject, OpenAIEmbedding userPhraseEmbedding, int numResults = 3, double minMatchScore = 0.8);
    Task<string> GetChatResponseAsString(WorkItemChat chat, AiModel? model = null);
    Task<string> SendRagSearchToLlm(List<RagSearchResult> ragSearchResults, string searchTerm);
}