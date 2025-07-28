using ChatUiT2.Interfaces;
using ChatUiT2.Models.OpenAI;
using Microsoft.AspNetCore.Mvc;

namespace ChatUiT2.Controllers;

[ApiController]
[Route("v1/[controller]")]
public class ModelsController : ControllerBase
{
    private readonly ISettingsService _settingsService;

    public ModelsController(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public ActionResult<ModelListResponse> GetModels()
    {
        var models = new List<ModelInfo>
        {
            new ModelInfo
            {
                Id = "personalhandbok",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                OwnedBy = "chatuit"
            }
        };

        return Ok(new ModelListResponse { Data = models });
    }
}
