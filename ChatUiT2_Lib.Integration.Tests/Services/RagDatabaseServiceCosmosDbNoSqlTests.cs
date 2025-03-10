using ChatUiT2.Integration.Tests.TestStaging;
using ChatUiT2.Models.RagProject;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ChatUiT2.Integration.Tests.Services;

public class RagDatabaseServiceCosmosDbNoSqlTests
{
    [Fact]
    public async Task SaveRagProject_NormalRun_ShouldCreateDbEntry()
    {
        // Arrange
        var ragProject = new RagProject
        {
            Name = "Test Project",
            Description = "Test Description",
            Configuration = new RagConfiguration
            {
                DbName = "TestDb",
                ItemCollectionName = "TestCollection"
            },
            ContentItems = new List<ContentItem>
            {
                new ContentItem
                {
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = "Test Content 1"                    
                },
                new ContentItem
                {
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = "Test Content 2"
                }
            }
        };
        var service = RagDatabaseServiceCosmosDbNoSqlStaging.GetRagDatabaseServiceCosmosDbNoSqlStaging("Development");
        var configuration = ConfigurationStaging.GetConfiguration("Development");
        var connectionString = configuration["ConnectionStrings:RagProjectDef"];
        var cosmosClient = new CosmosClient(connectionString);
        var ragProjectDefdatabase = cosmosClient.GetDatabase(configuration["RagProjectDefCollection"]);
        var ragProjectContainer = ragProjectDefdatabase.GetContainer(configuration["RagProjectDefCollection"]);
        var ragItemDatabase = await cosmosClient.CreateDatabaseIfNotExistsAsync(ragProject.Configuration.DbName);
        var itemContainer = (await ragItemDatabase.Database.CreateContainerIfNotExistsAsync(ragProject.Configuration.ItemCollectionName, "/id")).Container;

        // Act
        await service.SaveRagProject(ragProject);

        // Assert
        var response = await ragProjectContainer.ReadItemAsync<RagProject>(ragProject.Id, new PartitionKey(ragProject.Id));

        Assert.NotNull(response.Resource);
        Assert.Equal(ragProject.Name, response.Resource.Name);
        Assert.Equal(ragProject.Description, response.Resource.Description);

        // Check content items
        var contentItems = itemContainer.GetItemLinqQueryable<ContentItem>().Where(i => i.RagProjectId == ragProject.Id).ToFeedIterator<ContentItem>();
        List<ContentItem> contentItemList = new List<ContentItem>();
        while (contentItems.HasMoreResults)
        {
            var contentItem = await contentItems.ReadNextAsync();
            contentItemList.AddRange(contentItem);
        }
        Assert.Equal(2, contentItemList.Count());
        Assert.Equal("Test Content 1", contentItemList[0].ContentText);
        Assert.Equal("Test Content 2", contentItemList[1].ContentText);
    }
}
