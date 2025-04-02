using ChatUiT2.Models;
using ChatUiT2.Services;

namespace ChatUiT2.Interfaces;

public interface ISettingsService
{
    AiModel DefaultModel { get; set; }
    AiModel NamingModel { get; set; }
    AiModel EmbeddingModel { get; set; }

    AiModel GetModel(string name);
    List<AiModel> GetModels(IUserService user);
    //ModelEndpoint GetEndpoint(string name);
    //ModelEndpoint GetEndpoint(AiModel model);
}
