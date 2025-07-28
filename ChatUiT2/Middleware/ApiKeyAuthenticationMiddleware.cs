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
    if (context.Request.Path.StartsWithSegments("/v1"))
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
    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
    if (authHeader == null || !authHeader.StartsWith("Bearer "))
    {
      return false;
    }

    var apiKey = authHeader.Substring("Bearer ".Length).Trim();
    
    var validKeys = new[]
    {
      _configuration["API_KEY_1"],
      _configuration["API_KEY_2"]
    };

    return validKeys.Contains(apiKey) && !string.IsNullOrEmpty(apiKey);
  }
}
