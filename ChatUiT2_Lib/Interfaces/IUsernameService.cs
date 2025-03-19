namespace ChatUiT2.Interfaces;
public interface IUsernameService
{
    Task<string> GetUsername();
}