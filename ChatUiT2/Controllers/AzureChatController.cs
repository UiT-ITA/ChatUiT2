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
            if (request.Messages == null || !request.Messages.Any())
            {
                return BadRequest(new { error = new { message = "At least one message is required." } });
            }

            var (messages, options, openAIService) = await PrepareRagResponse(request.Messages, request.MaxTokens);

            if (request.Stream)
            {
                return await CreateStreamingChatResponse(messages, options, openAIService);
            }
            else
            {
                var ragResponse = await openAIService.GetResponseRaw(messages, options);
                return CreateChatCompletionResponse(ragResponse, openAIService, messages);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Azure OpenAI chat completion request");
            return StatusCode(500, new { error = new { message = "Internal server error" } });
        }
    }

    private async Task<(List<OpenAI.Chat.ChatMessage> messages, ChatCompletionOptions options, OpenAIService openAIService)> PrepareRagResponse(List<AzureChatMessage> requestMessages, int? maxTokens)
    {
        // Get the last user message for RAG search
        var lastUserMessage = requestMessages.LastOrDefault(m => m.Role == "user");
        if (lastUserMessage == null)
        {
            throw new InvalidOperationException("No user message found for RAG search");
        }

        var ragProject = await _ragDatabaseService.GetRagProjectByName("PersonalhandbokItems");
        if (ragProject == null)
        {
            throw new InvalidOperationException("PersonalhandbokItems project not found");
        }

        var model = _settingsService.EmbeddingModel;
        var embeddingService = new OpenAIService(model, "System", _logger, _mediator, _chatToolsService);
        var embedding = await embeddingService.GetEmbedding(lastUserMessage.Content);
        var ragSearchResults = await _ragSearchService.DoGenericRagSearch(ragProject, embedding, 3, 0.6d);

        var defaultModel = _settingsService.DefaultModel;
        var messages = new List<OpenAI.Chat.ChatMessage>();

        // Convert all request messages to OpenAI format
        foreach (var requestMessage in requestMessages)
        {
            switch (requestMessage.Role.ToLower())
            {
                case "system":
                    messages.Add(new SystemChatMessage(requestMessage.Content));
                    break;
                case "user":
                    messages.Add(new UserChatMessage(requestMessage.Content));
                    break;
                case "assistant":
                    messages.Add(new AssistantChatMessage(requestMessage.Content));
                    break;
            }
        }

        // Add RAG context after the conversation history
        if (ragSearchResults.Any())
        {
            var ragContext = "Here are relevant knowledge articles:\n\n";
            for (int i = 0; i < ragSearchResults.Count; i++)
            {
                var result = ragSearchResults[i];
                var sourceInfo = GetSourceInfo(result);
                ragContext += $"## Knowledge article {i + 1} (Source: {sourceInfo})\n{result.SourceContent}\n\n";
            }
            ragContext += "Please use this information to answer the user's question and cite your sources.";
            
            messages.Add(new SystemChatMessage(ragContext));
        }

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

    private IActionResult CreateChatCompletionResponse(string ragResponse, OpenAIService openAIService, List<OpenAI.Chat.ChatMessage> allMessages)
    {
        // Calculate actual prompt tokens from all messages
        var promptText = string.Join("\n", allMessages.Select(m => m.ToString()));
        var promptTokens = openAIService.GetTokens(promptText);
        var completionTokens = openAIService.GetTokens(ragResponse);

        var response = new AzureChatCompletionResponse
        {
            Id = $"chatcmpl-{Guid.NewGuid()}",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
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
                    FinishReason = "stop"
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

    private async Task<IActionResult> CreateStreamingChatResponse(List<OpenAI.Chat.ChatMessage> messages, ChatCompletionOptions options, OpenAIService openAIService)
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
                        await WriteChatStreamChunk(new AzureStreamCompletionResponse
                        {
                            Id = completionId,
                            Created = created,
                            Choices = new List<AzureChatDelta>
                            {
                                new AzureChatDelta {
                                    Index = 0,
                                    Delta = new AzureStreamContent
                                    {
                                        Content = contentUpdate.Text
                                    }

                                }
                            }
                        });
                   

                        //await WriteChatStreamChunk(new AzureChatCompletionResponse
                        //{
                        //    Id = completionId,
                        //    Created = created,
                        //    Choices = new List<AzureChatChoice>
                        //    {
                        //        new AzureChatChoice
                        //        {
                        //            Index = 0,
                        //            Message = new AzureChatMessage
                        //            {
                        //                Role = "assistant",
                        //                Content = contentUpdate.Text
                        //            },
                        //            FinishReason = null
                        //        }
                        //    }
                        //});
                    }
                }

                if (update.FinishReason != null)
                {
                    await WriteChatStreamChunk(new AzureStreamCompletionResponse
                    {
                        Id = completionId,
                        Created = created,
                        Choices = new List<AzureChatDelta>
                        {
                            new AzureChatDelta {
                                Index = 0,
                                Delta = new AzureStreamContent
                                {
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

    private async Task WriteChatStreamChunk(AzureStreamCompletionResponse chunk)
    {
        var json = JsonSerializer.Serialize(chunk, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }
}
