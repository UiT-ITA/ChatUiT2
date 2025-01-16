using ChatUiT2_Classlib.Model.Topdesk;

namespace ChatUiT2.Interfaces
{
    public interface IRagTopdeskDatabaseService
    {
        Task<List<TopdeskArticle>> GetAllTopdeskArticles();
        Task SaveTopdeskArticle(TopdeskArticle topdeskArticle);
    }
}