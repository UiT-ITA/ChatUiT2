using ChatUiT2.Interfaces;
using ChatUiT2.Models.OpenAI;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;
using OpenAI.Chat;
using MediatR;
using ChatUiT2.Models.RagProject;
using ChatUiT2.Services;
using ChatUiT2.Models;

namespace ChatUiT2.Controllers;

[ApiController]
[Route("v1/chat")]
public class ChatController : ControllerBase
{
    private readonly IChatToolsService _chatToolsService;
    private readonly IRagSearchService _ragSearchService;
    private readonly IRagDatabaseService _ragDatabaseService;
    private readonly ISettingsService _settingsService;
    private readonly IMediator _mediator;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatToolsService chatToolsService, 
                         IRagSearchService ragSearchService,
                         IRagDatabaseService ragDatabaseService,
                         ISettingsService settingsService,
                         IMediator mediator,
                         ILogger<ChatController> logger)
    {
        _chatToolsService = chatToolsService;
        _ragSearchService = ragSearchService;
        _ragDatabaseService = ragDatabaseService;
        _settingsService = settingsService;
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("completions")]
    public async Task<IActionResult> CreateChatCompletion([FromBody] ChatCompletionRequest request)
    {
        try
        {
            if (request.Model != "personalhandbok")
            {
                return BadRequest(new { error = new { message = "Model not supported. Only 'personalhandbok' is available." } });
            }

            var userMessage = request.Messages.LastOrDefault(m => m.Role == "user");
            if (userMessage == null)
            {
                return BadRequest(new { error = new { message = "No user message found in request." } });
            }

            // Get RAG search results
            var ragProject = await _ragDatabaseService.GetRagProjectByName("PersonalhandbokItems");
            if (ragProject == null)
            {
                return StatusCode(500, new { error = new { message = "PersonalhandbokItems project not found" } });
            }

            var model = _settingsService.EmbeddingModel;
            var embeddingService = new OpenAIService(model, "System", _logger, _mediator, _chatToolsService);
            var embedding = await embeddingService.GetEmbedding(userMessage.Content);
            var ragSearchResults = await _ragSearchService.DoGenericRagSearch(ragProject, embedding, 3, 0.6d);

            // Create RAG messages manually
            var defaultModel = _settingsService.DefaultModel;
            var messages = new List<OpenAI.Chat.ChatMessage>();
            
            messages.Add(new SystemChatMessage("Use the information in the knowledge articles the user provides to answer the user question. Answer in the same language as the user is asking in. IMPORTANT: Always cite your sources by referencing the specific knowledge article(s) you used in your response. Include the source information at the end of your answer with title and URL when available."));
            
            for (int i = 0; i < ragSearchResults.Count; i++)
            {
                var result = ragSearchResults[i];
                var sourceInfo = "";
                
                if (!string.IsNullOrEmpty(result.ContentTitle))
                {
                    sourceInfo = result.ContentTitle;
                    if (!string.IsNullOrEmpty(result.ContentUrl))
                    {
                        sourceInfo += $" ({result.ContentUrl})";
                    }
                }
                else if (!string.IsNullOrEmpty(result.Source))
                {
                    sourceInfo = result.Source;
                }
                else
                {
                    sourceInfo = "Unknown source";
                }
                
                messages.Add(new UserChatMessage($"## Knowledge article {i} (Source: {sourceInfo})\n\n{result.SourceContent}\n\n"));
            }
            
            messages.Add(new UserChatMessage($"My question is {userMessage.Content}"));

            var options = new ChatCompletionOptions()
            {
                MaxOutputTokenCount = defaultModel.MaxTokens,
                Temperature = 0.5f
            };

            var openAIService = new OpenAIService(defaultModel, "System", _logger, _mediator, _chatToolsService);

            if (request.Stream)
            {
                return await CreateStreamingResponse(request, userMessage.Content, messages, options, openAIService);
            }
            else
            {
                var ragResponse = await openAIService.GetResponseRaw(messages, options);
                return CreateNonStreamingResponse(request, userMessage.Content, ragResponse, openAIService);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat completion request");
            return StatusCode(500, new { error = new { message = "Internal server error" } });
        }
    }

    private IActionResult CreateNonStreamingResponse(ChatCompletionRequest request, string userContent, string ragResponse, OpenAIService openAIService)
    {
        var promptTokens = openAIService.GetTokens(userContent);
        var completionTokens = openAIService.GetTokens(ragResponse);
        
        var response = new ChatCompletionResponse
        {
            Id = $"chatcmpl-{Guid.NewGuid()}",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = request.Model,
            Choices = new List<Choice>
            {
                new Choice
                {
                    Index = 0,
                    Message = new ChatUiT2.Models.OpenAI.ChatMessage
                    {
                        Role = "assistant",
                        Content = ragResponse
                    },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage
            {
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = promptTokens + completionTokens
            }
        };

        return Ok(response);
    }

    private async Task<IActionResult> CreateStreamingResponse(ChatCompletionRequest request, string userContent, List<OpenAI.Chat.ChatMessage> messages, ChatCompletionOptions options, OpenAIService openAIService)
    {
        Response.Headers.Add("Content-Type", "text/event-stream");
        Response.Headers.Add("Cache-Control", "no-cache");
        Response.Headers.Add("Connection", "keep-alive");

        var completionId = $"chatcmpl-{Guid.NewGuid()}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            // Send initial chunk with role
            await WriteStreamChunk(new ChatCompletionStreamResponse
            {
                Id = completionId,
                Created = created,
                Model = request.Model,
                Choices = new List<StreamChoice>
                {
                    new StreamChoice
                    {
                        Index = 0,
                        Delta = new StreamDelta { Role = "assistant" }
                    }
                }
            });

            // Stream the actual AI response
            await foreach (var update in openAIService.GetStreamingResponseRaw(messages, options))
            {
                foreach (var contentUpdate in update.ContentUpdate)
                {
                    if (!string.IsNullOrEmpty(contentUpdate.Text))
                    {
                        await WriteStreamChunk(new ChatCompletionStreamResponse
                        {
                            Id = completionId,
                            Created = created,
                            Model = request.Model,
                            Choices = new List<StreamChoice>
                            {
                                new StreamChoice
                                {
                                    Index = 0,
                                    Delta = new StreamDelta { Content = contentUpdate.Text }
                                }
                            }
                        });
                    }
                }

                if (update.FinishReason != null)
                {
                    // Send final chunk
                    await WriteStreamChunk(new ChatCompletionStreamResponse
                    {
                        Id = completionId,
                        Created = created,
                        Model = request.Model,
                        Choices = new List<StreamChoice>
                        {
                            new StreamChoice
                            {
                                Index = 0,
                                Delta = new StreamDelta(),
                                FinishReason = "stop"
                            }
                        }
                    });
                    break;
                }
            }

            // Send done signal
            await Response.WriteAsync("data: [DONE]\n\n");
            await Response.Body.FlushAsync();

            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in streaming response");
            return StatusCode(500, new { error = new { message = "Streaming error" } });
        }
    }

    private async Task WriteStreamChunk(ChatCompletionStreamResponse chunk)
    {
        var json = JsonSerializer.Serialize(chunk, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }
}
