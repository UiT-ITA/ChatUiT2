using ChatUiT2_Classlib.Model.Topdesk;

namespace ChatUiT2.Interfaces
{
    public interface IRagTopdeskDatabaseService
    {
        Task<List<TopdeskArticle>> GetAllTopdeskArticles();
        Task<TopdeskArticle> GetByTopdeskId(string topdeskId);
        Task SaveTopdeskArticle(TopdeskArticle topdeskArticle);
    }
}