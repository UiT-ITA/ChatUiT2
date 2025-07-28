using ChatUiT2.Interfaces;
using ChatUiT2.Models.OpenAI;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

namespace ChatUiT2.Controllers;

[ApiController]
[Route("v1/chat")]
public class ChatController : ControllerBase
{
    private readonly IChatToolsService _chatToolsService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatToolsService chatToolsService, ILogger<ChatController> logger)
    {
        _chatToolsService = chatToolsService;
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

            var ragResponse = await _chatToolsService.GetPersonalhandbok(userMessage.Content);

            if (request.Stream)
            {
                return await CreateStreamingResponse(request, ragResponse);
            }
            else
            {
                return CreateNonStreamingResponse(request, userMessage.Content, ragResponse);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat completion request");
            return StatusCode(500, new { error = new { message = "Internal server error" } });
        }
    }

    private IActionResult CreateNonStreamingResponse(ChatCompletionRequest request, string userContent, string ragResponse)
    {
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
                    Message = new ChatMessage
                    {
                        Role = "assistant",
                        Content = ragResponse
                    },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage
            {
                PromptTokens = EstimateTokens(userContent),
                CompletionTokens = EstimateTokens(ragResponse),
                TotalTokens = EstimateTokens(userContent) + EstimateTokens(ragResponse)
            }
        };

        return Ok(response);
    }

    private async Task<IActionResult> CreateStreamingResponse(ChatCompletionRequest request, string ragResponse)
    {
        Response.Headers.Add("Content-Type", "text/event-stream");
        Response.Headers.Add("Cache-Control", "no-cache");
        Response.Headers.Add("Connection", "keep-alive");

        var completionId = $"chatcmpl-{Guid.NewGuid()}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

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

        // Split response into words and stream them
        var words = ragResponse.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            var content = words[i];
            if (i < words.Length - 1) content += " "; // Add space except for last word

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
                        Delta = new StreamDelta { Content = content }
                    }
                }
            });

            // Small delay to simulate streaming
            await Task.Delay(50);
        }

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

        // Send done signal
        await Response.WriteAsync("data: [DONE]\n\n");
        await Response.Body.FlushAsync();

        return new EmptyResult();
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

    private int EstimateTokens(string text)
    {
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}
