using ChatUiT2.Integration.Tests.TestStaging;
using ChatUiT2.Interfaces;
using ChatUiT2.Models.RagProject;
using ChatUiT2.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace ChatUiT2.Integration.Tests.Services;

public class RagDatabaseServiceCosmosDbNoSqlTests : IAsyncDisposable
{
    private readonly string _ragProjectDefDbName = string.Empty;
    private readonly string _ragProjectDefContainerName = string.Empty;
    private readonly string _ragItemDbName = string.Empty;
    private readonly string _ragItemContainerName = string.Empty;
    private readonly IConfiguration _configuration;
    private readonly CosmosClient _cosmosClient;

    private IRagDatabaseService _service;
    private Database _ragProjectDefDatabase;
    private Container _ragProjectDefContainer;
    private Database _ragItemDatabase;
    private Container _ragItemContainer;

    public RagDatabaseServiceCosmosDbNoSqlTests()
    {
        this._configuration = ConfigurationStaging.GetConfiguration("Development");
        this._ragProjectDefDbName = _configuration["RagProjectDefDatabaseName"] ?? string.Empty;
        if(string.IsNullOrEmpty(_ragProjectDefDbName))
        {
            throw new Exception("RagProjectDefDatabaseName is not set in appsettings.json");
        }
        this._ragProjectDefContainerName = _configuration["RagProjectDefContainerName"] ?? string.Empty;
        if (string.IsNullOrEmpty(_ragProjectDefContainerName))
        {
            throw new Exception("RagProjectDefContainerName is not set in appsettings.json");
        }
        this._ragItemDbName = _configuration["RagItemDatabaseName"] ?? string.Empty;
        if (string.IsNullOrEmpty(_ragItemDbName))
        {
            throw new Exception("RagItemDatabaseName is not set in appsettings.json");
        }
        this._ragItemContainerName = _configuration["RagItemContainerName"] ?? string.Empty;
        if (string.IsNullOrEmpty(_ragItemContainerName))
        {
            throw new Exception("RagItemContainerName is not set in appsettings.json");
        }
        var connectionString = _configuration["ConnectionStrings:RagProjectDef"];
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("ConnectionStrings:RagProjectDef is not set in appsettings.json");
        }
        this._cosmosClient = new CosmosClient(connectionString);
        SetupDatabase().GetAwaiter().GetResult();
    }

    private async Task SetupDatabase()
    {
        // Delete any existing project def db if exists
        var existingRagProjectDefdatabase = _cosmosClient.GetDatabase(_ragProjectDefDbName);
        try
        {
            await existingRagProjectDefdatabase.DeleteAsync();
        }
        catch (Exception)
        {
            // Ignore, most likely db does not exist            
        }
        // Create project def database
        this._ragProjectDefDatabase = await _cosmosClient.CreateDatabaseAsync(_ragProjectDefDbName);

        // Delete any existing project def container if exists
        var existingRagProjectDefContainer = _ragProjectDefDatabase.GetContainer(_ragProjectDefContainerName);
        try
        {
            await existingRagProjectDefContainer.DeleteContainerAsync();
        }
        catch (Exception)
        {
            // Ignore, most likely db does not exist
        }
        // Create project def container
        this._ragProjectDefContainer = await _ragProjectDefDatabase.CreateContainerAsync(_ragProjectDefContainerName, "/id");



        // Delete any existing item db if exists
        var existingRagItemDatabase = _cosmosClient.GetDatabase(_ragItemDbName);
        try
        {
            await existingRagItemDatabase.DeleteAsync();
        }
        catch (Exception)
        {
            // Ignore, most likely db does not exist
        }
        // Create project def database
        this._ragItemDatabase = await _cosmosClient.CreateDatabaseAsync(_ragItemDbName);

        // Delete any existing project def container if exists
        var existingRagItemContainer = _ragProjectDefDatabase.GetContainer(_ragItemContainerName);
        try
        {
            await existingRagItemContainer.DeleteContainerAsync();
        }
        catch (Exception)
        {
            // Ignore, most likely db does not exist
        }
        // Create project def container
        this._ragItemContainer = await _ragItemDatabase.CreateContainerAsync(_ragItemContainerName, "/id");

        this._service = RagDatabaseServiceCosmosDbNoSqlStaging.GetRagDatabaseServiceCosmosDbNoSqlStaging("Development");
    }

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
                DbName = _ragItemDbName,
                ItemCollectionName = _ragItemContainerName
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

        // Act
        await _service.SaveRagProject(ragProject);

        // Assert
        var response = await _ragProjectDefContainer.ReadItemAsync<RagProject>(ragProject.Id, new PartitionKey(ragProject.Id));

        Assert.NotNull(response.Resource);
        Assert.Equal(ragProject.Name, response.Resource.Name);
        Assert.Equal(ragProject.Description, response.Resource.Description);

        // Check content items
        var contentItems = _ragItemContainer.GetItemLinqQueryable<ContentItem>().Where(i => i.RagProjectId == ragProject.Id).ToFeedIterator<ContentItem>();
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

    [Fact]
    public async Task GetRagProjectById_NormalRun_ShouldReturnItem()
    {
        // Arrange
        var ragProject = new RagProject
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Project",
            Description = "Test Description",
            Configuration = new RagConfiguration
            {
                DbName = _ragItemDbName,
                ItemCollectionName = _ragItemContainerName
            },
            ContentItems = new List<ContentItem>
            {
                new ContentItem
                {
                    Id = Guid.NewGuid().ToString(),
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = "Test Content 1"
                },
                new ContentItem
                {
                    Id = Guid.NewGuid().ToString(),
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = "Test Content 2"
                }
            }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var item in ragProject.ContentItems)
        {
            item.RagProjectId = ragProject.Id;
            await _ragItemContainer.CreateItemAsync(item, new PartitionKey(item.Id));
        }
        var result = await _service.GetRagProjectById(ragProject.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ragProject.Name, result.Name);
        Assert.Equal(ragProject.Description, result.Description);
        Assert.Empty(result.ContentItems);
    }

    [Fact]
    public async Task GetRagProjectById_WithContentItems_ShouldReturnItemAndContentItems()
    {
        // Arrange
        var ragProject = new RagProject
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Project",
            Description = "Test Description",
            Configuration = new RagConfiguration
            {
                DbName = _ragItemDbName,
                ItemCollectionName = _ragItemContainerName
            },
            ContentItems = new List<ContentItem>
            {
                new ContentItem
                {
                    Id = Guid.NewGuid().ToString(),
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = "Test Content 1"
                },
                new ContentItem
                {
                    Id = Guid.NewGuid().ToString(),
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = "Test Content 2"
                }
            }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var item in ragProject.ContentItems)
        {
            item.RagProjectId = ragProject.Id;
            await _ragItemContainer.CreateItemAsync(item, new PartitionKey(item.Id));
        }
        var result = await _service.GetRagProjectById(ragProject.Id, true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ragProject.Name, result.Name);
        Assert.Equal(ragProject.Description, result.Description);
        Assert.Equal(2, result.ContentItems.Count());
    }

    [Fact]
    public async Task GetAllRagProjects_NormalRun_ShouldReturnAllProjects()
    {
        // Arrange
        var ragProjects = new List<RagProject>();
        for (int i = 1; i < 6; i++)
        {
            ragProjects.Add(new RagProject
            {
                Id = $"project{i}",
                Name = $"name{i}",
                Description = $"desc{i}",
                Configuration = new RagConfiguration
                {
                    DbName = "",
                    ItemCollectionName = _ragItemContainerName
                }
            });
        }

        // Act
        foreach (var project in ragProjects)
        {
            await _ragProjectDefContainer.CreateItemAsync(project, new PartitionKey(project.Id));
        }
        var result = await _service.GetAllRagProjects();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.Count);
        Assert.Equal("project1", result[0].Id);
        Assert.Equal("project2", result[1].Id);
        Assert.Equal("project3", result[2].Id);
        Assert.Equal("project4", result[3].Id);
        Assert.Equal("project5", result[4].Id);        
    }

    public async ValueTask DisposeAsync()
    {
        if(_ragProjectDefContainer != null)
        {
            await _ragProjectDefContainer.DeleteContainerAsync();
        }
        if (_ragProjectDefDatabase != null)
        {
            await _ragProjectDefDatabase.DeleteAsync();
        }
        if (_ragItemContainer != null)
        {
            await _ragItemContainer.DeleteContainerAsync();
        }
        if (_ragItemDatabase != null)
        {
            await _ragItemDatabase.DeleteAsync();
        }
    }
}
