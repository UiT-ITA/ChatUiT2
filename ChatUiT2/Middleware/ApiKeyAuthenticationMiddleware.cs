namespace ChatUiT2.Middleware;

public class ApiKeyAuthenticationMiddleware
{
  private readonly RequestDelegate _next;
  private readonly IConfiguration _configuration;
  private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

  public ApiKeyAuthenticationMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyAuthenticationMiddleware> logger)
  {
    _next = next;
    _configuration = configuration;
    _logger = logger;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    if (context.Request.Path.StartsWithSegments("/v1") || 
        context.Request.Path.StartsWithSegments("/openai"))
    {
      if (!IsValidApiKey(context))
      {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized");
        return;
      }
    }

    await _next(context);
  }

  private bool IsValidApiKey(HttpContext context)
  {
    // Check for Bearer token (standard OpenAI format)
    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
    if (authHeader != null && authHeader.StartsWith("Bearer "))
    {
      var apiKey = authHeader.Substring("Bearer ".Length).Trim();
      return IsKeyValid(apiKey);
    }

    // Check for api-key header (Azure OpenAI format)
    var apiKeyHeader = context.Request.Headers["api-key"].FirstOrDefault();
    if (!string.IsNullOrEmpty(apiKeyHeader))
    {
      return IsKeyValid(apiKeyHeader);
    }

    return false;
  }

  private bool IsKeyValid(string apiKey)
  {
    var validKeys = new[]
    {
      _configuration["API_KEY_1"],
      _configuration["API_KEY_2"]
    };

    return validKeys.Contains(apiKey) && !string.IsNullOrEmpty(apiKey);
  }
}
