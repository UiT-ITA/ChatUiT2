using ChatUiT2.Interfaces;
using ChatUiT2.Models.OpenAI;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<ActionResult<ChatCompletionResponse>> CreateChatCompletion([FromBody] ChatCompletionRequest request)
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
                    PromptTokens = EstimateTokens(userMessage.Content),
                    CompletionTokens = EstimateTokens(ragResponse),
                    TotalTokens = EstimateTokens(userMessage.Content) + EstimateTokens(ragResponse)
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat completion request");
            return StatusCode(500, new { error = new { message = "Internal server error" } });
        }
    }

    private int EstimateTokens(string text)
    {
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}
