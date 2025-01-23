using ChatUiT2.Models;
using ChatUiT2.Services;

namespace ChatUiT2.Interfaces;

public interface ISettingsService
{
    List<Model> GetModels();
    Model GetDefaultModel();
    Model GetNamingModel();
    Model GetModel(string name);
    ModelEndpoint GetEndpoint(string name);
    ModelEndpoint GetEndpoint(Model model);
}
