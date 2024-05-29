namespace ChatUiT2.Services;

public class AzureOpenAIService
{
    private IConfiguration _configuration { get; set; }
    public AzureOpenAIService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<string> GetResponse()
    {
        return await Task.FromResult("Hello World");
    }
}
