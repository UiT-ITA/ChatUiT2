using ChatUiT2.Integration.Tests.TestStaging;
using ChatUiT2.Interfaces;
using ChatUiT2.Models.RagProject;
using ChatUiT2.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace ChatUiT2.Integration.Tests.Services;

public class RagDatabaseServiceCosmosDbNoSqlTests : IAsyncDisposable
{
    private readonly string _ragProjectDefDbName = string.Empty;
    private readonly string _ragProjectDefContainerName = string.Empty;
    private readonly string _ragItemDbName = string.Empty;
    private readonly string _ragItemContainerName = string.Empty;
    private readonly string _ragEmbeddingContainerName = string.Empty;
    private readonly IConfiguration _configuration;
    private readonly CosmosClient _cosmosClient;

    private IRagDatabaseService _service;
    private Database _ragProjectDefDatabase;
    private Container _ragProjectDefContainer;
    private Database _ragItemDatabase;
    private Container _ragItemContainer;
    private Container _ragEmbeddingContainer;

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
        this._ragEmbeddingContainerName = _configuration["RagEmbeddingContainerName"] ?? string.Empty;
        if (string.IsNullOrEmpty(_ragEmbeddingContainerName))
        {
            throw new Exception("RagEmbeddingContainerName is not set in appsettings.json");
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
        // Create item database
        this._ragItemDatabase = await _cosmosClient.CreateDatabaseAsync(_ragItemDbName);

        // Delete any existing item container if exists
        var existingRagItemContainer = _ragItemDatabase.GetContainer(_ragItemContainerName);
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

        // Delete any existing embedding container if exists
        var existingEmbeddingContainer = _ragItemDatabase.GetContainer(_ragEmbeddingContainerName);
        try
        {
            await existingEmbeddingContainer.DeleteContainerAsync();
        }
        catch (Exception)
        {
            // Ignore, most likely db does not exist
        }
        // Create project def container
        this._ragEmbeddingContainer = await _ragItemDatabase.CreateContainerAsync(_ragEmbeddingContainerName, "/SourceItemId");

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

    [Fact]
    public async Task DeleteRagProject_ValidProject_DeletesProjectAndItemDatabase()
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
                    DbName = _ragItemDbName,
                    ItemCollectionName = _ragItemContainerName
                }, 
                ContentItems = new()
                {
                    new ContentItem
                    {
                        Id = $"item{i}",
                        SystemName = "TestSystem",
                        ContentType = "INLINE",
                        ContentText = $"Test Content {i}",
                        RagProjectId = $"project{i}"
                    },
                    new ContentItem
                    {
                        Id = $"item{i+5}",
                        SystemName = "TestSystem",
                        ContentType = "INLINE",
                        ContentText = $"Test Content {i}",
                        RagProjectId = $"project{i}"
                    }
                }
            });
        }

        // Act
        foreach (var project in ragProjects)
        {
            await _ragProjectDefContainer.CreateItemAsync(project, new PartitionKey(project.Id));
        }
        foreach (var projectItem in ragProjects.SelectMany(x => x.ContentItems))
        {
            await _ragItemContainer.CreateItemAsync(projectItem, new PartitionKey(projectItem.Id));
        }
        RagProject toDeleteProject = ragProjects.FirstOrDefault(x => x.Id == "project3")!;
        var query = "SELECT * FROM c";
        var queryDefinition = new QueryDefinition(query);
        var projectIterator = _ragProjectDefContainer.GetItemQueryIterator<RagProject>(queryDefinition);
        var projectsBefore = new List<RagProject>();
        while (projectIterator.HasMoreResults)
        {
            var item = await projectIterator.ReadNextAsync();
            projectsBefore.AddRange(item);
        }
        await _service.DeleteRagProject(toDeleteProject);
        var itemDbExistsAfter = await _service.DatabaseExistsAsync(_ragItemDbName);
        projectIterator = _ragProjectDefContainer.GetItemQueryIterator<RagProject>(queryDefinition);
        var projectsAfter = new List<RagProject>();
        while (projectIterator.HasMoreResults)
        {
            var item = await projectIterator.ReadNextAsync();
            projectsAfter.AddRange(item);
        }

        // Assert
        Assert.Equal(5, projectsBefore.Count);
        Assert.Equal(4, projectsAfter.Count);
        Assert.Contains(projectsAfter, x => x.Id == "project1");
        Assert.Contains(projectsAfter, x => x.Id == "project2");
        Assert.DoesNotContain(projectsAfter, x => x.Id == "project3");
        Assert.Contains(projectsAfter, x => x.Id == "project4");
        Assert.Contains(projectsAfter, x => x.Id == "project5");
        Assert.False(itemDbExistsAfter);
    }

    [Fact]
    public async Task DeleteRagProject_MissingId_ThrowsArgumentException()
    {
        // Arrange
        var ragProject = new RagProject
        {
            Id = null,
            Configuration = new RagConfiguration
            {
                DbName = "testDb"
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.DeleteRagProject(ragProject));
    }

    [Fact]
    public async Task DeleteRagProject_MissingDbName_ThrowsArgumentException()
    {
        // Arrange
        var ragProject = new RagProject
        {
            Id = "testId",
            Configuration = new RagConfiguration
            {
                DbName = null
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.DeleteRagProject(ragProject));
    }

    [Fact]
    public async Task GetContentItemById_ItemExists_ReturnsContentItem()
    {
        // Arrange
        var ragProject = new RagProject
        {
            Id = $"project1",
            Name = $"name1",
            Description = $"desc1",
            Configuration = new RagConfiguration
            {
                DbName = _ragItemDbName,
                ItemCollectionName = _ragItemContainerName
            },
            ContentItems = new()
            {
                new ContentItem
                {
                    Id = $"item1",
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = $"Test Content 1",
                    RagProjectId = $"project1"
                },
                new ContentItem
                {
                    Id = $"item2",
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = $"Test Content 2",
                    RagProjectId = $"project1"
                }
            }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var projectItem in ragProject.ContentItems)
        {
            await _ragItemContainer.CreateItemAsync(projectItem, new PartitionKey(projectItem.Id));
        }
        var item1 = await _service.GetContentItemById(ragProject, "item1");
        var item2 = await _service.GetContentItemById(ragProject, "item2");

        // Assert
        Assert.NotNull(item1);
        Assert.Equal("item1", item1!.Id);
        Assert.NotNull(item2);
        Assert.Equal("item2", item2!.Id);
    }

    [Fact]
    public async Task GetContentItemById_ItemDoesNotExist_ReturnsNull()
    {
        // Arrange
        var ragProject = new RagProject
        {
            Id = $"project1",
            Name = $"name1",
            Description = $"desc1",
            Configuration = new RagConfiguration
            {
                DbName = _ragItemDbName,
                ItemCollectionName = _ragItemContainerName
            },
            ContentItems = new()
                {
                    new ContentItem
                    {
                        Id = $"item1",
                        SystemName = "TestSystem",
                        ContentType = "INLINE",
                        ContentText = $"Test Content 1",
                        RagProjectId = $"project1"
                    },
                    new ContentItem
                    {
                        Id = $"item2",
                        SystemName = "TestSystem",
                        ContentType = "INLINE",
                        ContentText = $"Test Content 2",
                        RagProjectId = $"project1"
                    }
                }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var projectItem in ragProject.ContentItems)
        {
            await _ragItemContainer.CreateItemAsync(projectItem, new PartitionKey(projectItem.Id));
        }
        var item3 = await _service.GetContentItemById(ragProject, "item3");

        // Assert
        Assert.Null(item3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetContentItemById_ItemIdNotSet_ThrowsArgumentException(string? itemId)
    {
        // Arrange
        var ragProject = new RagProject
        {
            Id = "testProjectId",
            Configuration = new RagConfiguration
            {
                DbName = _ragItemDbName,
                ItemCollectionName = _ragItemContainerName
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetContentItemById(ragProject, itemId));
    }

    [Fact]
    public async Task GetEmbeddingsByProject_ValidProject_ReturnsEmbeddings()
    {
        // Arrange
        var ragProject = new RagProject
        {
            Id = "testProjectId",
            Configuration = new RagConfiguration
            {
                DbName = _ragItemDbName,
                ItemCollectionName = _ragItemContainerName,
                EmbeddingCollectioName = _ragEmbeddingContainerName
            }
        };
        var embeddings = new List<RagTextEmbedding>
        {
            new RagTextEmbedding { Id = "embedding1", SourceItemId = "source1", RagProjectId = ragProject.Id },
            new RagTextEmbedding { Id = "embedding2", SourceItemId = "source2", RagProjectId = ragProject.Id }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.SourceItemId));
        }
        var result = await _service.GetEmbeddingsByProject(ragProject);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(embeddings.Count, result.Count);
        Assert.Equal(embeddings[0].Id, result[0].Id);
        Assert.Equal(embeddings[1].Id, result[1].Id);
    }

    [Fact]
    public async Task GetEmbeddingsByProject_ProjectIdNotSet_ThrowsArgumentException()
    {
        // Arrange
        var ragProject = new RagProject
        {
            Id = string.Empty,
            Configuration = new RagConfiguration
            {
                DbName = _ragItemDbName,
                ItemCollectionName = _ragItemContainerName,
                EmbeddingCollectioName = _ragEmbeddingContainerName
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetEmbeddingsByProject(ragProject));
    }

    [Fact]
    public async Task GetEmbeddingsByProject_ValidProjectWithSourceItem_ReturnsEmbeddingsWithSourceItems()
    {
        // Arrange
        var ragProject = new RagProject
        {
            Id = "testProjectId",
            Configuration = new RagConfiguration
            {
                DbName = _ragItemDbName,
                ItemCollectionName = _ragItemContainerName,
                EmbeddingCollectioName = _ragEmbeddingContainerName
            },
            ContentItems = new()
            {
                new ContentItem
                {
                    Id = $"item1",
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = $"Test Content 1",
                    RagProjectId = $"project1"
                },
                new ContentItem
                {
                    Id = $"item2",
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = $"Test Content 2",
                    RagProjectId = $"project1"
                }
            }
        };
        var embeddings = new List<RagTextEmbedding>
        {
            new RagTextEmbedding { Id = "embedding1", SourceItemId = "item1", RagProjectId = ragProject.Id },
            new RagTextEmbedding { Id = "embedding2", SourceItemId = "item2", RagProjectId = ragProject.Id }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var projectItem in ragProject.ContentItems)
        {
            await _ragItemContainer.CreateItemAsync(projectItem, new PartitionKey(projectItem.Id));
        }
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.SourceItemId));
        }
        var result = await _service.GetEmbeddingsByProject(ragProject, true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(embeddings.Count, result.Count);
        Assert.Equal(embeddings[0].Id, result[0].Id);
        Assert.Equal(embeddings[1].Id, result[1].Id);
        Assert.NotNull(result[0].ContentItem);
        Assert.NotNull(result[1].ContentItem);
        Assert.Equal("item1", result[0].ContentItem!.Id);
        Assert.Equal("item2", result[1].ContentItem!.Id);
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
