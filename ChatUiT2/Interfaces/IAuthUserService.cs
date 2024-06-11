namespace ChatUiT2.Interfaces;

public interface IAuthUserService
{
    Task<string?> GetUsername();
    Task<bool> TestInRole(string[] role);

    Task<string?> GetName();
}
