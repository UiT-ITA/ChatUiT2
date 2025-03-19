using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using ChatUiT2.Models.RagProject;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;
using Microsoft.Extensions.Configuration;
using ChatUiT2.Models.Mediatr;

namespace ChatUiT2.Services;

/// <summary>
/// Class for common rag search operations.
/// Depends on the rag database class.
/// This is separate from the database service because the database service is
/// a singleton for efficiency.
/// </summary>
public class RagSearchService : IRagSearchService
{
    private readonly IRagDatabaseService _ragDatabaseService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<RagSearchService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IUsernameService _usernameService;

    public RagSearchService(IRagDatabaseService ragDatabaseService,
                            ISettingsService settingsService,
                            ILogger<RagSearchService> logger,
                            IConfiguration configuration,
                            IUsernameService usernameService)
    {
        this._ragDatabaseService = ragDatabaseService;
        this._settingsService = settingsService;
        this._logger = logger;
        this._configuration = configuration;
        this._usernameService = usernameService;
    }

    public async Task<List<RagSearchResult>> DoGenericRagSearch(RagProject ragProject, OpenAIEmbedding userPhraseEmbedding, int numResults = 3, double minMatchScore = 0.8)
    {
        int numDimensions = int.Parse(_configuration["RagEmbeddingNumDimensions"] ?? "0");
        if(numDimensions <= 0)
        {
            throw new Exception("Invalid number of dimensions for rag embeddings. Check appsettings");
        }
        var floatsUser = userPhraseEmbedding.ToFloats().Slice(0, numDimensions).ToArray();

        return await _ragDatabaseService.DoGenericRagSearch(ragProject, floatsUser, numResults, minMatchScore);
    }

    public async Task<string> SendRagSearchToLlm(List<RagSearchResult> ragSearchResults, string searchTerm)
    {
        AiModel defaultModel = _settingsService.DefaultModel;
        WorkItemChat chat = new();
        chat.Settings = new ChatSettings()
        {
            MaxTokens = defaultModel.MaxTokens,
            Model = defaultModel.DeploymentName,
            Temperature = 0.5f
        };
        chat.Type = WorkItemType.Chat;
        chat.Settings.Prompt = $"Use the information in the knowledge articles the user provides to answer the user question. Answer in the same language as the user is asking in.\n\n";
        for (int i = 0; i < ragSearchResults.Count(); i++)
        {
            chat.Messages.Add(new ChatMessage()
            {
                Role = ChatMessageRole.User,
                Content = $"## Knowledge article {i}\n\n{ragSearchResults.ElementAt(i).SourceContent}\n\n"
            });
        }

        chat.Messages.Add(new ChatMessage()
        {
            Role = ChatMessageRole.User,
            Content = $"My question is {searchTerm}"
        });

        return await GetChatResponseAsString(chat, defaultModel);
    }

    /// <summary>
    /// When you just want the response as a string
    /// No streaming handling needed
    /// </summary>
    /// <param name="chat"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<string> GetChatResponseAsString(WorkItemChat chat, AiModel? model = null)
    {
        string result = string.Empty;
        if (model == null)
        {
            model = _settingsService.DefaultModel;
        }

        if (model.DeploymentType == DeploymentType.AzureOpenAI)
        {
            var openAIService = new OpenAIService(model, await _usernameService.GetUsername(), _logger, null!, null!);

            result = await openAIService.GetResponse(chat);
        }
        else
        {
            throw new Exception("Unsupported deployment type: " + model.DeploymentType);
        }

        return result;
    }
}
