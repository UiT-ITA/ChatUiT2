using ChatUiT2.Models;
using ChatUiT2.Services;

namespace ChatUiT2.Interfaces;

public interface ISettingsService
{
    List<AiModel> Models { get; set; }
    AiModel DefaultModel { get; set; }
    AiModel NamingModel { get; set; }
    AiModel EmbeddingModel { get; set; }

    AiModel GetModel(string name);
    //ModelEndpoint GetEndpoint(string name);
    //ModelEndpoint GetEndpoint(AiModel model);
}
