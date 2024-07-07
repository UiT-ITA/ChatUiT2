using ChatUiT2.Interfaces;
using ChatUiT2.Models;

namespace ChatUiT2.Services;

public class AdminService
{
    private IConfiguration _configuration;
    private IDatabaseService _databaseService;
    public AdminService(IConfiguration configuration, IDatabaseService databaseService)
    {
        _configuration = configuration;
        _databaseService = databaseService;
    }

    public async Task<List<User>> GetUsers()
    {
        return await _databaseService.GetUsers();
    }
    
}
