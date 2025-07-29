using ChatUiT2.Interfaces;
using ChatUiT2.Models.Azure;
using ChatUiT2.Models.OpenAI;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using OpenAI.Chat;
using MediatR;
using ChatUiT2.Models.RagProject;
using ChatUiT2.Services;
using ChatUiT2.Models;

namespace ChatUiT2.Controllers;

[ApiController]
[Route("openai")]
public class AzureChatController : ControllerBase
{
    private readonly IChatToolsService _chatToolsService;
    private readonly IRagSearchService _ragSearchService;
    private readonly IRagDatabaseService _ragDatabaseService;
    private readonly ISettingsService _settingsService;
    private readonly IMediator _mediator;
    private readonly ILogger<AzureChatController> _logger;

    public AzureChatController(IChatToolsService chatToolsService, 
                              IRagSearchService ragSearchService,
                              IRagDatabaseService ragDatabaseService,
                              ISettingsService settingsService,
                              IMediator mediator,
                              ILogger<AzureChatController> logger)
    {
        _chatToolsService = chatToolsService;
        _ragSearchService = ragSearchService;
        _ragDatabaseService = ragDatabaseService;
        _settingsService = settingsService;
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("deployments/{deploymentId}/chat/completions")]
    public async Task<IActionResult> CreateAzureChatCompletion(string deploymentId, [FromBody] AzureChatCompletionRequest request)
    {
        try
        {
            // Extract user message from the messages array
            var userMessage = ExtractUserMessage(request.Messages);
            if (string.IsNullOrEmpty(userMessage))
            {
                return BadRequest(new { error = new { message = "At least one user message is required." } });
            }

            var (messages, options, openAIService) = await PrepareRagResponse(userMessage, request.MaxTokens);

            if (request.Stream)
            {
                return await CreateStreamingChatResponse(deploymentId, userMessage, messages, options, openAIService);
            }
            else
            {
                var ragResponse = await openAIService.GetResponseRaw(messages, options);
                return CreateChatCompletionResponse(deploymentId, userMessage, ragResponse, openAIService);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Azure OpenAI chat completion request");
            return StatusCode(500, new { error = new { message = "Internal server error" } });
        }
    }

    private async Task<(List<OpenAI.Chat.ChatMessage> messages, ChatCompletionOptions options, OpenAIService openAIService)> PrepareRagResponse(string userInput, int? maxTokens)
    {
        var ragProject = await _ragDatabaseService.GetRagProjectByName("PersonalhandbokItems");
        if (ragProject == null)
        {
            throw new InvalidOperationException("PersonalhandbokItems project not found");
        }

        var model = _settingsService.EmbeddingModel;
        var embeddingService = new OpenAIService(model, "System", _logger, _mediator, _chatToolsService);
        var embedding = await embeddingService.GetEmbedding(userInput);
        var ragSearchResults = await _ragSearchService.DoGenericRagSearch(ragProject, embedding, 3, 0.6d);

        var defaultModel = _settingsService.DefaultModel;
        var messages = new List<OpenAI.Chat.ChatMessage>();
        
        messages.Add(new SystemChatMessage("Use the information in the knowledge articles to provide a helpful response. Answer in the same language as the user. IMPORTANT: Always cite your sources by referencing the specific knowledge article(s) you used."));

    for (int i = 0; i < ragSearchResults.Count; i++)
    {
      var result = ragSearchResults[i];
      var sourceInfo = GetSourceInfo(result);
      messages.Add(new UserChatMessage($"## Knowledge article {i} (Source: {sourceInfo})\n\n{result.SourceContent}\n\n"));
        }
        
        messages.Add(new UserChatMessage($"My question is: {userInput}"));

        var options = new ChatCompletionOptions()
        {
            MaxOutputTokenCount = maxTokens ?? defaultModel.MaxTokens,
            Temperature = 0.5f
        };

        var openAIService = new OpenAIService(defaultModel, "System", _logger, _mediator, _chatToolsService);
        
        return (messages, options, openAIService);
    }

    private string GetSourceInfo(RagSearchResult result)
    {
        if (!string.IsNullOrEmpty(result.ContentTitle))
        {
            var sourceInfo = result.ContentTitle;
            if (!string.IsNullOrEmpty(result.ContentUrl))
            {
                sourceInfo += $" ({result.ContentUrl})";
            }
            return sourceInfo;
        }
        else if (!string.IsNullOrEmpty(result.Source))
        {
            return result.Source;
        }
        return "Unknown source";
    }

    private string ExtractUserMessage(List<AzureChatMessage> messages)
    {
        // Find the last user message (most recent user input)
        var userMessage = messages.LastOrDefault(m => m.Role == "user");
        return userMessage?.Content ?? string.Empty;
    }

    private IActionResult CreateChatCompletionResponse(string model, string userMessage, string ragResponse, OpenAIService openAIService)
    {
        var promptTokens = openAIService.GetTokens(userMessage);
        var completionTokens = openAIService.GetTokens(ragResponse);

        var response = new AzureChatCompletionResponse
        {
            Id = $"chatcmpl-{Guid.NewGuid()}",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = model,
            Choices = new List<AzureChatChoice>
            {
                new AzureChatChoice
                {
                    Index = 0,
                    Message = new AzureChatMessage
                    {
                        Role = "assistant",
                        Content = ragResponse
                    },
                    FinishReason = "stop",
                    Logprobs = null
                }
            },
            Usage = new AzureUsage
            {
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = promptTokens + completionTokens
            }
        };

        return Ok(response);
    }

    private async Task<IActionResult> CreateStreamingChatResponse(string model, string userMessage, List<OpenAI.Chat.ChatMessage> messages, ChatCompletionOptions options, OpenAIService openAIService)
    {
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";

        var completionId = $"chatcmpl-{Guid.NewGuid()}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await foreach (var update in openAIService.GetStreamingResponseRaw(messages, options))
            {
                foreach (var contentUpdate in update.ContentUpdate)
                {
                    if (!string.IsNullOrEmpty(contentUpdate.Text))
                    {
                        await WriteChatStreamChunk(new AzureChatCompletionResponse
                        {
                            Id = completionId,
                            Created = created,
                            Model = model,
                            Choices = new List<AzureChatChoice>
                            {
                                new AzureChatChoice
                                {
                                    Index = 0,
                                    Message = new AzureChatMessage
                                    {
                                        Role = "assistant",
                                        Content = contentUpdate.Text
                                    },
                                    FinishReason = null
                                }
                            }
                        });
                    }
                }

                if (update.FinishReason != null)
                {
                    await WriteChatStreamChunk(new AzureChatCompletionResponse
                    {
                        Id = completionId,
                        Created = created,
                        Model = model,
                        Choices = new List<AzureChatChoice>
                        {
                            new AzureChatChoice
                            {
                                Index = 0,
                                Message = new AzureChatMessage
                                {
                                    Role = "assistant",
                                    Content = ""
                                },
                                FinishReason = "stop"
                            }
                        }
                    });
                    break;
                }
            }

            await Response.WriteAsync("data: [DONE]\n\n");
            await Response.Body.FlushAsync();
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Azure chat completion streaming response");
            return StatusCode(500, new { error = new { message = "Streaming error" } });
        }
    }

    private async Task WriteChatStreamChunk(AzureChatCompletionResponse chunk)
    {
        var json = JsonSerializer.Serialize(chunk, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }
}
