using ChatUiT2.Integration.Tests.TestStaging;
using ChatUiT2.Interfaces;
using ChatUiT2.Models.RagProject;
using ChatUiT2.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using OpenAI.Embeddings;
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
    private readonly string _ragEmbeddingEventContainerName = string.Empty;
    private readonly IConfiguration _configuration;
    private readonly CosmosClient _cosmosClient;

    private IRagDatabaseService _service;
    private Database _ragProjectDefDatabase;
    private Container _ragProjectDefContainer;
    private Database _ragItemDatabase;
    private Container _ragItemContainer;
    private Container _ragEmbeddingContainer;
    private Container _ragEmbeddingEventContainer;

    private List<DateTimeOffset> _dateTimeOffsets = new List<DateTimeOffset>()
    {
        new DateTimeOffset(2023, 5, 16, 13, 10, 0, TimeSpan.Zero),
        new DateTimeOffset(2023, 5, 16, 13, 11, 0, TimeSpan.Zero),
        new DateTimeOffset(2023, 5, 16, 13, 12, 0, TimeSpan.Zero),
        new DateTimeOffset(2023, 5, 16, 13, 13, 0, TimeSpan.Zero),
        new DateTimeOffset(2023, 5, 16, 13, 14, 0, TimeSpan.Zero),
        new DateTimeOffset(2023, 5, 16, 13, 15, 0, TimeSpan.Zero),
        new DateTimeOffset(2023, 5, 16, 13, 16, 0, TimeSpan.Zero),
        new DateTimeOffset(2023, 5, 16, 13, 17, 0, TimeSpan.Zero),
        new DateTimeOffset(2023, 5, 16, 13, 18, 0, TimeSpan.Zero),
        new DateTimeOffset(2023, 5, 16, 13, 19, 0, TimeSpan.Zero),
    };

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
        this._ragEmbeddingEventContainerName = _configuration["RagEmbeddingEventContainerName"] ?? string.Empty;
        if (string.IsNullOrEmpty(_ragEmbeddingEventContainerName))
        {
            throw new Exception("RagEmbeddingEventContainerName is not set in appsettings.json");
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

        // Delete any existing embedding event container if exists
        var existingEmbeddingEventContainer = _ragItemDatabase.GetContainer(_ragEmbeddingEventContainerName);
        try
        {
            await existingEmbeddingEventContainer.DeleteContainerAsync();
        }
        catch (Exception)
        {
            // Ignore, most likely db does not exist
        }
        // Create project def container
        this._ragEmbeddingEventContainer = await _ragItemDatabase.CreateContainerAsync(_ragEmbeddingEventContainerName, "/RagProjectId");

        // Prepare IDateTimeProvider with predefined sequence of values
        Mock<IDateTimeProvider> dateTimeProviderMock = new();
        DateTimeOffset now1 = new DateTimeOffset(2023, 5, 16, 13, 10, 0, TimeSpan.Zero);
        DateTimeOffset now2 = new DateTimeOffset(2023, 5, 16, 13, 11, 0, TimeSpan.Zero);
        DateTimeOffset now3 = new DateTimeOffset(2023, 5, 16, 13, 12, 0, TimeSpan.Zero);
        DateTimeOffset now4 = new DateTimeOffset(2023, 5, 16, 13, 13, 0, TimeSpan.Zero);
        DateTimeOffset now5 = new DateTimeOffset(2023, 5, 16, 13, 14, 0, TimeSpan.Zero);
        DateTimeOffset now6 = new DateTimeOffset(2023, 5, 16, 13, 15, 0, TimeSpan.Zero);
        DateTimeOffset now7 = new DateTimeOffset(2023, 5, 16, 13, 16, 0, TimeSpan.Zero);
        DateTimeOffset now8 = new DateTimeOffset(2023, 5, 16, 13, 17, 0, TimeSpan.Zero);
        DateTimeOffset now9 = new DateTimeOffset(2023, 5, 16, 13, 18, 0, TimeSpan.Zero);
        dateTimeProviderMock.SetupSequence(m => m.OffsetUtcNow)
            .Returns(_dateTimeOffsets[0])
            .Returns(_dateTimeOffsets[1])
            .Returns(_dateTimeOffsets[2])
            .Returns(_dateTimeOffsets[3])
            .Returns(_dateTimeOffsets[4])
            .Returns(_dateTimeOffsets[5])
            .Returns(_dateTimeOffsets[6])
            .Returns(_dateTimeOffsets[7])
            .Returns(_dateTimeOffsets[8]);

        this._service = RagDatabaseServiceCosmosDbNoSqlStaging.GetRagDatabaseServiceCosmosDbNoSqlStaging("Development", dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task SaveRagProject_NormalRun_ShouldCreateDbEntry()
    {
        // Arrange
        var ragProject = new RagProject
        {
            Id = null,
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
                    ContentText = "Test Content 1",
                    SourceSystemId = "source1"
                },
                new ContentItem
                {
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = "Test Content 2",
                    SourceSystemId = "source2"
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
        Assert.Equal(_dateTimeOffsets[0], response.Resource.Updated.UtcDateTime);
        Assert.Equal(_dateTimeOffsets[1], response.Resource.Created.UtcDateTime);

        // Check content items
        var contentItems = _ragItemContainer.GetItemLinqQueryable<ContentItem>().ToFeedIterator<ContentItem>();
        List<ContentItem> contentItemList = new List<ContentItem>();        
        while (contentItems.HasMoreResults)
        {
            var contentItem = await contentItems.ReadNextAsync();
            contentItemList.AddRange(contentItem);
        }
        Assert.Equal(2, contentItemList.Count());
        Assert.Contains(contentItemList, x => x.ContentText == "Test Content 1");
        Assert.Contains(contentItemList, x => x.ContentText == "Test Content 2");
        Assert.Contains(contentItemList, x => x.SourceSystemId == "source1");
        Assert.Contains(contentItemList, x => x.SourceSystemId == "source2");
        var item1 = contentItemList.FirstOrDefault(x => x.SourceSystemId == "source1");
        var item2 = contentItemList.FirstOrDefault(x => x.SourceSystemId == "source2");
        Assert.Equal(ragProject.Id, item1.RagProjectId);
        Assert.Equal(ragProject.Id, item2.RagProjectId);
        Assert.Equal(_dateTimeOffsets[2], item1.Created.UtcDateTime);
        Assert.Equal(_dateTimeOffsets[2], item1.Updated.UtcDateTime);
        Assert.Equal(_dateTimeOffsets[3], item2.Created.UtcDateTime);
        Assert.Equal(_dateTimeOffsets[3], item2.Updated.UtcDateTime);
        Assert.True(item1.ContentNeedsEmbeddingUpdate);
        Assert.True(item2.ContentNeedsEmbeddingUpdate);
    }

    [Fact]
    public async Task SaveRagProject_ProjectWithSameNameExists_ShouldUpdateExisting()
    {
        // Arrange
        var ragProject = new RagProject
        {
            Name = "Test Project",
            Description = "Test Description updated",
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
                    ContentText = "Test Content 1",
                    SourceSystemId = "source1"
                },
                new ContentItem
                {
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = "Test Content 2",
                    SourceSystemId = "source2"
                }
            }
        };
        DateTimeOffset existingCreatedTime = new DateTimeOffset(2022, 1, 1, 10, 10, 0, TimeSpan.Zero);
        var existingRagProject = new RagProject()
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Project",
            Description = "OldDescription",
            Created = existingCreatedTime,
            Updated = existingCreatedTime
        };

        // Act
        var projectInDb = await _ragProjectDefContainer.CreateItemAsync(existingRagProject, new PartitionKey(existingRagProject.Id));
        await _service.SaveRagProject(ragProject);

        // Assert
        var projectItems = _ragProjectDefContainer.GetItemLinqQueryable<RagProject>().ToFeedIterator<RagProject>();
        List<RagProject> projectList = new List<RagProject>();
        while (projectItems.HasMoreResults)
        {
            var projectItem = await projectItems.ReadNextAsync();
            projectList.AddRange(projectItem);
        }
        Assert.Single(projectList);
        Assert.Equal(projectInDb.Resource.Id, projectList[0].Id);
        Assert.Equal(ragProject.Name, projectList[0].Name);
        Assert.Equal(ragProject.Description, projectList[0].Description);
        Assert.Equal(_dateTimeOffsets[0], projectList[0].Updated);
        Assert.Equal(existingCreatedTime, projectList[0].Created.UtcDateTime);

        // Check content items
        var contentItems = _ragItemContainer.GetItemLinqQueryable<ContentItem>().ToFeedIterator<ContentItem>();
        List<ContentItem> contentItemList = new List<ContentItem>();
        while (contentItems.HasMoreResults)
        {
            var contentItem = await contentItems.ReadNextAsync();
            contentItemList.AddRange(contentItem);
        }
        Assert.Equal(2, contentItemList.Count());
        Assert.Contains(contentItemList, x => x.ContentText == "Test Content 1");
        Assert.Contains(contentItemList, x => x.ContentText == "Test Content 2");
        Assert.Contains(contentItemList, x => x.SourceSystemId == "source1");
        Assert.Contains(contentItemList, x => x.SourceSystemId == "source2");
        var item1 = contentItemList.FirstOrDefault(x => x.SourceSystemId == "source1");
        var item2 = contentItemList.FirstOrDefault(x => x.SourceSystemId == "source2");
        Assert.Equal(ragProject.Id, item1.RagProjectId);
        Assert.Equal(ragProject.Id, item2.RagProjectId);
        Assert.Equal(_dateTimeOffsets[1], item1.Created.UtcDateTime);
        Assert.Equal(_dateTimeOffsets[1], item1.Updated.UtcDateTime);
        Assert.Equal(_dateTimeOffsets[2], item2.Created.UtcDateTime);
        Assert.Equal(_dateTimeOffsets[2], item2.Updated.UtcDateTime);
        Assert.True(item1.ContentNeedsEmbeddingUpdate);
        Assert.True(item2.ContentNeedsEmbeddingUpdate);
    }

    [Fact]
    public async Task SaveRagProject_OneContentItemAlreadyExist_ShouldUpdateExistingContentItem()
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
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = "Test Content 1",
                    SourceSystemId = "source1"
                },
                new ContentItem
                {
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = "Test Content 2",
                    SourceSystemId = "source2"
                }
            }
        };
        var existingContentItem = new ContentItem
        {
            Id = Guid.NewGuid().ToString(),
            SystemName = "TestSystem",
            ContentType = "INLINE",
            ContentText = "Old content",
            SourceSystemId = "source1",
            RagProjectId = ragProject.Id
        };

        // Act
        var existingInDb = await _ragItemContainer.CreateItemAsync(existingContentItem, new PartitionKey(existingContentItem.Id));
        await _service.SaveRagProject(ragProject);

        // Assert
        var response = await _ragProjectDefContainer.ReadItemAsync<RagProject>(ragProject.Id, new PartitionKey(ragProject.Id));

        Assert.NotNull(response.Resource);
        Assert.Equal(ragProject.Name, response.Resource.Name);
        Assert.Equal(ragProject.Description, response.Resource.Description);

        // Check content items
        var contentItems = _ragItemContainer.GetItemLinqQueryable<ContentItem>().ToFeedIterator<ContentItem>();
        List<ContentItem> contentItemList = new List<ContentItem>();
        while (contentItems.HasMoreResults)
        {
            var contentItem = await contentItems.ReadNextAsync();
            contentItemList.AddRange(contentItem);
        }
        Assert.Equal(2, contentItemList.Count());
        Assert.Equal(existingInDb.Resource.Id, contentItemList[0].Id);
        Assert.Equal("Test Content 1", contentItemList[0].ContentText);
        Assert.Equal("Test Content 2", contentItemList[1].ContentText);
    }

    [Fact]
    public async Task SaveRagProject_OneContentItemRemovedFromSource_ShouldDeleteRemovedContentItem()
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
                ItemCollectionName = _ragItemContainerName,
                EmbeddingCollectioName = _ragEmbeddingContainerName,
            },
            ContentItems = new List<ContentItem>
            {
                new ContentItem
                {
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = "Test Content 1",
                    SourceSystemId = "source1"
                },
                new ContentItem
                {
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = "Test Content 2",
                    SourceSystemId = "source2"
                }
            }
        };
        var existingContentItem = new ContentItem
        {
            Id = Guid.NewGuid().ToString(),
            SystemName = "TestSystem",
            ContentType = "INLINE",
            ContentText = "Content nr 3",
            SourceSystemId = "source3",
            RagProjectId = ragProject.Id
        };

        // Act
        var existingInDb = await _ragItemContainer.CreateItemAsync(existingContentItem, new PartitionKey(existingContentItem.Id));
        await _service.SaveRagProject(ragProject);

        // Assert
        var response = await _ragProjectDefContainer.ReadItemAsync<RagProject>(ragProject.Id, new PartitionKey(ragProject.Id));

        Assert.NotNull(response.Resource);
        Assert.Equal(ragProject.Name, response.Resource.Name);
        Assert.Equal(ragProject.Description, response.Resource.Description);

        // Check content items
        var contentItems = _ragItemContainer.GetItemLinqQueryable<ContentItem>().ToFeedIterator<ContentItem>();
        List<ContentItem> contentItemList = new List<ContentItem>();
        while (contentItems.HasMoreResults)
        {
            var contentItem = await contentItems.ReadNextAsync();
            contentItemList.AddRange(contentItem);
        }
        // Should be only two content items now
        Assert.Equal(2, contentItemList.Count());
        Assert.Equal("Test Content 1", contentItemList[0].ContentText);
        Assert.Equal("Test Content 2", contentItemList[1].ContentText);
    }

    [Fact]
    public async Task SaveRagProject_OneOtherContentItemAlreadyExist_ShouldNotChangeTheExistingOne()
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
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = "Test Content 1",
                    SourceSystemId = "source1"
                },
                new ContentItem
                {
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = "Test Content 2",
                    SourceSystemId = "source2"
                }
            }
        };
        var existingContentItem = new ContentItem
        {
            Id = Guid.NewGuid().ToString(),
            SystemName = "TestSystem",
            ContentType = "INLINE",
            ContentText = "Old content",
            SourceSystemId = "source3",
            RagProjectId = ragProject.Id
        };

        // Act
        var existingInDb = await _ragItemContainer.CreateItemAsync(existingContentItem, new PartitionKey(existingContentItem.Id));
        await _service.SaveRagProject(ragProject);

        // Assert
        var response = await _ragProjectDefContainer.ReadItemAsync<RagProject>(ragProject.Id, new PartitionKey(ragProject.Id));

        Assert.NotNull(response.Resource);
        Assert.Equal(ragProject.Name, response.Resource.Name);
        Assert.Equal(ragProject.Description, response.Resource.Description);

        // Check content items
        var contentItems = _ragItemContainer.GetItemLinqQueryable<ContentItem>().ToFeedIterator<ContentItem>();
        List<ContentItem> contentItemList = new List<ContentItem>();
        while (contentItems.HasMoreResults)
        {
            var contentItem = await contentItems.ReadNextAsync();
            contentItemList.AddRange(contentItem);
        }
        Assert.Equal(3, contentItemList.Count());
        Assert.Contains(contentItemList, x => x.ContentText == "Old content");
        Assert.Contains(contentItemList, x => x.ContentText == "Test Content 1");
        Assert.Contains(contentItemList, x => x.ContentText == "Test Content 2");
        Assert.Contains(contentItemList, x => x.Id == existingInDb.Resource.Id);
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

    [Fact]
    public async Task SaveRagEmbeddingEvent_NewEvent_CreatesItem()
    {
        // Arrange
        string ragProjectId = "projectId";
        var ragProject = CreateTestRagProject(ragProjectId, "projectName", "projectDescription", 1);
        string embedId = "EmbeddingId";
        var textEmbeddingEvent = CreateTestRagTextEmbeddingEvent(ragProject.Id, "item1", embedId);
        textEmbeddingEvent.EmbeddingSourceType = EmbeddingSourceType.Paragraph;

        // Act
        var embeddingsBefore = await GetAllEmbeddingEvents();
        await _service.SaveRagEmbeddingEvent(ragProject, textEmbeddingEvent);
        var embeddingsAfter = await GetAllEmbeddingEvents();

        // Assert
        Assert.Empty(embeddingsBefore);
        Assert.Single(embeddingsAfter);
        Assert.Equal(embedId, embeddingsAfter[0].Id);
        Assert.Equal(ragProjectId, embeddingsAfter[0].RagProjectId);
        Assert.Equal("item1", embeddingsAfter[0].ContentItemId);
        Assert.Equal(EmbeddingSourceType.Paragraph, embeddingsAfter[0].EmbeddingSourceType);
        Assert.Equal(embeddingsAfter[0].Created, embeddingsAfter[0].Created);
        Assert.Equal(embeddingsAfter[0].Updated, embeddingsAfter[0].Updated);
    }

    [Fact]
    public async Task SaveRagEmbeddingEvent_ExistingEvent_UpdatesItem()
    {
        // Arrange
        string ragProjectId = "projectId";
        var ragProject = CreateTestRagProject(ragProjectId, "projectName", "projectDescription", 1);
        string embedId = "EmbeddingId";
        var textEmbeddingEvent = CreateTestRagTextEmbeddingEvent(ragProject.Id, "item1", embedId);
        textEmbeddingEvent.EmbeddingSourceType = EmbeddingSourceType.Paragraph;

        // Act
        await _ragEmbeddingEventContainer.CreateItemAsync(textEmbeddingEvent, new PartitionKey(textEmbeddingEvent.RagProjectId));
        var embeddingsBefore = await GetAllEmbeddingEvents();
        textEmbeddingEvent.EmbeddingSourceType = EmbeddingSourceType.Question;
        await _service.SaveRagEmbeddingEvent(ragProject, textEmbeddingEvent);
        var embeddingsAfter = await GetAllEmbeddingEvents();

        // Assert
        Assert.Single(embeddingsBefore);
        Assert.Single(embeddingsAfter);
        Assert.Equal(embedId, embeddingsAfter[0].Id);
        Assert.Equal(ragProjectId, embeddingsAfter[0].RagProjectId);
        Assert.Equal("item1", embeddingsAfter[0].ContentItemId);
        Assert.Equal(EmbeddingSourceType.Paragraph, embeddingsBefore[0].EmbeddingSourceType);
        Assert.Equal(EmbeddingSourceType.Question, embeddingsAfter[0].EmbeddingSourceType);
        Assert.Equal(embeddingsBefore[0].Created, embeddingsAfter[0].Created);
        Assert.True(embeddingsBefore[0].Updated < embeddingsAfter[0].Updated);
    }

    [Fact]
    public async Task GetContentItemsWithNoEmbeddings_Exist_ShouldReturnList()
    {
        // Arrange
        string ragProjectId = "projectId";
        var ragProject = CreateTestRagProject(ragProjectId, "projectName", "projectDescription", 4);
        string embedId1 = "embedding1";
        string embedId2 = "embedding2";
        var textEmbedding = CreateTestRagTextEmbedding(ragProject.Id, "item0", embedId1);
        var textEmbedding2 = CreateTestRagTextEmbedding(ragProject.Id, "item1", embedId2);
        
        // Act
        await _ragEmbeddingContainer.CreateItemAsync(textEmbedding, new PartitionKey(textEmbedding.SourceItemId));
        await _ragEmbeddingContainer.CreateItemAsync(textEmbedding2, new PartitionKey(textEmbedding2.SourceItemId));
        foreach (var item in ragProject.ContentItems)
        {
            await _ragItemContainer.CreateItemAsync(item, new PartitionKey(item.Id));
        }
        var result = await _service.GetContentItemsWithNoEmbeddings(ragProject);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("item2", result[0].Id);
        Assert.Equal("item3", result[1].Id);
    }

    [Fact]
    public async Task GetContentItemsWithUpdates_Exist_ShouldReturnList()
    {
        // Arrange
        string ragProjectId = "projectId";
        var ragProject = CreateTestRagProject(ragProjectId, "projectName", "projectDescription", 4);
        ragProject.ContentItems[2].ContentNeedsEmbeddingUpdate = false;

        // Act
        foreach (var item in ragProject.ContentItems)
        {
            await _ragItemContainer.CreateItemAsync(item, new PartitionKey(item.Id));
        }
        var result = _service.GetContentItemsWithUpdates(ragProject);
        List<ContentItem> resultList = new List<ContentItem>();
        await foreach (var item in result)
        {
            resultList.Add(item);
        }

        // Assert
        Assert.Equal(3, resultList.Count());
        Assert.Equal("item0", resultList[0].Id);
        Assert.Equal("item1", resultList[1].Id);
        Assert.Equal("item3", resultList[2].Id);
    }

    [Fact]
    public async Task GetContentItemsWithUpdates_DoesNotExist_ShouldReturnEmptyList()
    {
        // Arrange
        string ragProjectId = "projectId";
        var ragProject = CreateTestRagProject(ragProjectId, "projectName", "projectDescription", 4);
        ragProject.ContentItems[2].ContentNeedsEmbeddingUpdate = false;

        // Act
        foreach (var item in ragProject.ContentItems)
        {
            item.ContentNeedsEmbeddingUpdate = false;
            await _ragItemContainer.CreateItemAsync(item, new PartitionKey(item.Id));
        }
        var result = _service.GetContentItemsWithUpdates(ragProject);
        List<ContentItem> resultList = new List<ContentItem>();
        await foreach (var item in result)
        {
            resultList.Add(item);
        }

        // Assert
        Assert.Empty(resultList);
    }

    [Fact]
    public async Task GetEmbeddingContentItemIdsByProject_ValidProject_ReturnsIds()
    {
        // Arrange
        var ragProject = CreateTestRagProject("testProjectId", "projectName", "projectDescription", 10);
        
        var embeddings = new List<RagTextEmbedding>
        {
            new RagTextEmbedding { Id = "embedding1", SourceItemId = "source1", RagProjectId = ragProject.Id },
            new RagTextEmbedding { Id = "embedding2", SourceItemId = "source2", RagProjectId = ragProject.Id },
            new RagTextEmbedding { Id = "embedding3", SourceItemId = "source3", RagProjectId = ragProject.Id }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.SourceItemId));
        }
        var result = await _service.GetEmbeddingContentItemIdsByProject(ragProject);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(embeddings.Count, result.Count());
        Assert.Equal("source1", result[0]);
        Assert.Equal("source2", result[1]);
        Assert.Equal("source3", result[2]);
    }

    [Fact]
    public async Task DeleteAllEmbeddingEvents_ValidProject_ShouldDeleteAll()
    {
        // Arrange
        var ragProject = CreateTestRagProject("testProjectId", "projectName", "projectDescription", 10);

        var embeddings = new List<EmbeddingEvent>
        {
            new EmbeddingEvent { Id = "embeddingEvent1", RagProjectId = ragProject.Id },
            new EmbeddingEvent { Id = "embeddingEvent2", RagProjectId = ragProject.Id },
            new EmbeddingEvent { Id = "embeddingEvent3", RagProjectId = ragProject.Id }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingEventContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.RagProjectId));
        }
        var eventsBeforeDelete = await GetAllEmbeddingEvents();
        await _service.DeleteAllEmbeddingEvents(ragProject);
        var eventsAfterDelete = await GetAllEmbeddingEvents();

        // Assert
        Assert.Equal(3, eventsBeforeDelete.Count);
        Assert.Empty(eventsAfterDelete);
    }

    [Fact]
    public async Task DeleteEmbeddingsForProject_ValidProject_ShouldDeleteAll()
    {
        // Arrange
        var ragProject = CreateTestRagProject("testProjectId", "projectName", "projectDescription", 10);

        var embeddings = new List<RagTextEmbedding>
        {
            new RagTextEmbedding { Id = "embeddingEvent1", RagProjectId = ragProject.Id },
            new RagTextEmbedding { Id = "embeddingEvent2", RagProjectId = ragProject.Id },
            new RagTextEmbedding { Id = "embeddingEvent3", RagProjectId = ragProject.Id }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.SourceItemId));
        }
        var eventsBeforeDelete = await GetAllEmbeddings();
        await _service.DeleteEmbeddingsForProject(ragProject);
        var eventsAfterDelete = await GetAllEmbeddings();

        // Assert
        Assert.Equal(3, eventsBeforeDelete.Count);
        Assert.Empty(eventsAfterDelete);
    }

    [Fact]
    public async Task GetEmbeddingEventById_ValidProject_ShouldGetCorrectItem()
    {
        // Arrange
        var ragProject = CreateTestRagProject("testProjectId", "projectName", "projectDescription", 10);

        var embeddings = new List<EmbeddingEvent>
        {
            new EmbeddingEvent { Id = "embeddingEvent1", RagProjectId = ragProject.Id },
            new EmbeddingEvent { Id = "embeddingEvent2", RagProjectId = ragProject.Id },
            new EmbeddingEvent { Id = "embeddingEvent3", RagProjectId = ragProject.Id }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingEventContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.RagProjectId));
        }
        var result = await _service.GetEmbeddingEventById(ragProject, "embeddingEvent2");
        var eventsInDb = await GetAllEmbeddingEvents();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("embeddingEvent2", result!.Id);
        Assert.Equal(3, eventsInDb.Count);
        Assert.NotNull(eventsInDb[0].ETag);
        Assert.NotEmpty(eventsInDb[0].ETag);
    }

    [Fact]
    public async Task GetEmbeddingEventByIdForProcessing_ValidProject_ShouldGetCorrectItem()
    {
        // Arrange
        var ragProject = CreateTestRagProject("testProjectId", "projectName", "projectDescription", 10);

        var embeddings = new List<EmbeddingEvent>
        {
            new EmbeddingEvent { Id = "embeddingEvent1", RagProjectId = ragProject.Id },
            new EmbeddingEvent { Id = "embeddingEvent2", RagProjectId = ragProject.Id },
            new EmbeddingEvent { Id = "embeddingEvent3", RagProjectId = ragProject.Id }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingEventContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.RagProjectId));
        }
        var result = await _service.GetEmbeddingEventByIdForProcessing(ragProject, "embeddingEvent2");
        var eventsInDb = await GetAllEmbeddingEvents();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("embeddingEvent2", result!.Id);
        Assert.Equal(3, eventsInDb.Count);
    }

    [Fact]
    public async Task GetEmbeddingEventByIdForProcessing_EtagChangedSignalingSomeoneElseUpdated_ShouldReturnNull()
    {
        // Arrange
        var ragProject = CreateTestRagProject("testProjectId", "projectName", "projectDescription", 10);

        var embeddings = new List<EmbeddingEvent>
        {
            new EmbeddingEvent { Id = "embeddingEvent1", RagProjectId = ragProject.Id },
            new EmbeddingEvent { Id = "embeddingEvent2", RagProjectId = ragProject.Id },
            new EmbeddingEvent { Id = "embeddingEvent3", RagProjectId = ragProject.Id }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingEventContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.RagProjectId));
        }
        var result = await _service.GetEmbeddingEventByIdForProcessing(ragProject, "embeddingEvent2", simulateEtagChanged: true);
        var eventsInDb = await GetAllEmbeddingEvents();

        // Assert
        Assert.Null(result);
        Assert.Equal(3, eventsInDb.Count);
    }

    [Fact]
    public async Task GetEmbeddingEventByIdForProcessing_EventDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var ragProject = CreateTestRagProject("testProjectId", "projectName", "projectDescription", 10);

        var embeddings = new List<EmbeddingEvent>
        {
            new EmbeddingEvent { Id = "embeddingEvent1", RagProjectId = ragProject.Id },
            new EmbeddingEvent { Id = "embeddingEvent2", RagProjectId = ragProject.Id },
            new EmbeddingEvent { Id = "embeddingEvent3", RagProjectId = ragProject.Id }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingEventContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.RagProjectId));
        }
        var result = await _service.GetEmbeddingEventByIdForProcessing(ragProject, "NonExistingId");
        var eventsInDb = await GetAllEmbeddingEvents();

        // Assert
        Assert.Null(result);
        Assert.Equal(3, eventsInDb.Count);
    }

    [Fact]
    public async Task GetExistingEmbeddingEventId_ValidProject_ShouldGetCorrectId()
    {
        // Arrange
        var ragProject = CreateTestRagProject("testProjectId", "projectName", "projectDescription", 10);
        string contentItemId = "itemId1";
        EmbeddingSourceType embeddingSourceType = EmbeddingSourceType.Paragraph;
        EmbeddingSourceType embeddingSourceType2 = EmbeddingSourceType.Question;
        var embeddings = new List<EmbeddingEvent>
        {
            new EmbeddingEvent { Id = "embeddingEvent1", RagProjectId = ragProject.Id },
            new EmbeddingEvent { Id = "embeddingEvent2", RagProjectId = ragProject.Id, ContentItemId = "itemId1", EmbeddingSourceType = embeddingSourceType },
            new EmbeddingEvent { Id = "embeddingEvent3", RagProjectId = ragProject.Id, ContentItemId = "anotherId", EmbeddingSourceType = embeddingSourceType2 }
        };

        // Act
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingEventContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.RagProjectId));
        }
        var result = await _service.GetExistingEmbeddingEventId(ragProject, contentItemId, EmbeddingSourceType.Paragraph);
        var eventsInDb = await GetAllEmbeddingEvents();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("embeddingEvent2", result);
        Assert.Equal(3, eventsInDb.Count);
    }

    [Theory]
    [InlineData("NoMatchingId", EmbeddingSourceType.Paragraph)]
    [InlineData("itemId1", EmbeddingSourceType.Question)]
    public async Task GetExistingEmbeddingEventId_NoMatch_ShouldReturnNull(string itemId, EmbeddingSourceType sourceType)
    {
        // Arrange
        var ragProject = CreateTestRagProject("testProjectId", "projectName", "projectDescription", 10);
        EmbeddingSourceType embeddingSourceType = EmbeddingSourceType.Paragraph;
        EmbeddingSourceType embeddingSourceType2 = EmbeddingSourceType.Question;
        var embeddings = new List<EmbeddingEvent>
        {
            new EmbeddingEvent { Id = "embeddingEvent1", RagProjectId = ragProject.Id },
            new EmbeddingEvent { Id = "embeddingEvent2", RagProjectId = ragProject.Id, ContentItemId = "itemId1", EmbeddingSourceType = embeddingSourceType },
            new EmbeddingEvent { Id = "embeddingEvent3", RagProjectId = ragProject.Id, ContentItemId = "anotherId", EmbeddingSourceType = embeddingSourceType2 }
        };

        // Act
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingEventContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.RagProjectId));
        }
        var result = await _service.GetExistingEmbeddingEventId(ragProject, itemId, sourceType);
        var eventsInDb = await GetAllEmbeddingEvents();

        // Assert
        Assert.Null(result);
        Assert.Equal(3, eventsInDb.Count);
    }

    [Fact]
    public async Task DeleteEmbeddingEvent_NormalRun_ShouldDelete()
    {
        // Arrange
        var ragProject = CreateTestRagProject("testProjectId", "projectName", "projectDescription", 10);
        EmbeddingSourceType embeddingSourceType = EmbeddingSourceType.Paragraph;
        EmbeddingSourceType embeddingSourceType2 = EmbeddingSourceType.Question;
        var embeddings = new List<EmbeddingEvent>
        {
            new EmbeddingEvent { Id = "embeddingEvent1", RagProjectId = ragProject.Id },
            new EmbeddingEvent { Id = "embeddingEvent2", RagProjectId = ragProject.Id, ContentItemId = "itemId1", EmbeddingSourceType = embeddingSourceType },
            new EmbeddingEvent { Id = "embeddingEvent3", RagProjectId = ragProject.Id, ContentItemId = "anotherId", EmbeddingSourceType = embeddingSourceType2 }
        };

        // Act
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingEventContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.RagProjectId));
        }
        var eventsInDbBefore = await GetAllEmbeddingEvents();
        await _service.DeleteEmbeddingEvent(ragProject, embeddings[1]);
        var eventsInDbAfter = await GetAllEmbeddingEvents();

        // Assert
        Assert.Equal(3, eventsInDbBefore.Count);
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent1");
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent2");
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent3");
        Assert.Equal(2, eventsInDbAfter.Count);
        Assert.Contains(eventsInDbAfter, x => x.Id == "embeddingEvent1");
        Assert.DoesNotContain(eventsInDbAfter, x => x.Id == "embeddingEvent2");
        Assert.Contains(eventsInDbAfter, x => x.Id == "embeddingEvent3");
    }

    [Fact]
    public async Task DeleteEmbeddingEvent_EventDoesNotExist_ShouldNotDeleteAnythingAndNoException()
    {
        // Arrange
        var ragProject = CreateTestRagProject("testProjectId", "projectName", "projectDescription", 10);
        EmbeddingSourceType embeddingSourceType = EmbeddingSourceType.Paragraph;
        EmbeddingSourceType embeddingSourceType2 = EmbeddingSourceType.Question;
        var embeddings = new List<EmbeddingEvent>
        {
            new EmbeddingEvent { Id = "embeddingEvent1", RagProjectId = ragProject.Id },
            new EmbeddingEvent { Id = "embeddingEvent2", RagProjectId = ragProject.Id, ContentItemId = "itemId1", EmbeddingSourceType = embeddingSourceType },
            new EmbeddingEvent { Id = "embeddingEvent3", RagProjectId = ragProject.Id, ContentItemId = "anotherId", EmbeddingSourceType = embeddingSourceType2 }
        };

        // Act
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingEventContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.RagProjectId));
        }
        var eventsInDbBefore = await GetAllEmbeddingEvents();
        await _service.DeleteEmbeddingEvent(ragProject, "NonExistingId");
        var eventsInDbAfter = await GetAllEmbeddingEvents();

        // Assert
        Assert.Equal(3, eventsInDbBefore.Count);
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent1");
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent2");
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent3");
        Assert.Equal(3, eventsInDbAfter.Count);
        Assert.Contains(eventsInDbAfter, x => x.Id == "embeddingEvent1");
        Assert.Contains(eventsInDbAfter, x => x.Id == "embeddingEvent2");
        Assert.Contains(eventsInDbAfter, x => x.Id == "embeddingEvent3");
    }

    [Fact]
    public async Task GetEmbeddingEventsByProjectId_NormalRun_ShouldReturnItemsForProject()
    {
        // Arrange
        var ragProject1 = CreateTestRagProject("testProjectId", "projectName", "projectDescription", 10);
        var ragProject2 = CreateTestRagProject("testProjectId2", "projectName2", "projectDescription2", 10);        
        var embeddings = new List<EmbeddingEvent>
        {
            new EmbeddingEvent { Id = "embeddingEvent1", RagProjectId = ragProject1.Id! },
            new EmbeddingEvent { Id = "embeddingEvent2", RagProjectId = ragProject1.Id! },
            new EmbeddingEvent { Id = "embeddingEvent3", RagProjectId = ragProject1.Id! },
            new EmbeddingEvent { Id = "embeddingEvent4", RagProjectId = ragProject2.Id! },
            new EmbeddingEvent { Id = "embeddingEvent5", RagProjectId = ragProject2.Id! },
            new EmbeddingEvent { Id = "embeddingEvent6", RagProjectId = ragProject2.Id! }
        };

        // Act
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingEventContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.RagProjectId));
        }
        var eventsInDbBefore = await GetAllEmbeddingEvents();
        var result = await _service.GetEmbeddingEventsByProjectId(ragProject2);
        var eventsInDbAfter = await GetAllEmbeddingEvents();

        // Assert
        Assert.Equal(3, result.Count());
        Assert.Contains(result, x => x.Id == "embeddingEvent4");
        Assert.Contains(result, x => x.Id == "embeddingEvent5");
        Assert.Contains(result, x => x.Id == "embeddingEvent6");
        Assert.Equal(6, eventsInDbBefore.Count);
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent1");
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent2");
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent3");
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent4");
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent5");
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent6");
        Assert.Equal(6, eventsInDbAfter.Count);
        Assert.Contains(eventsInDbAfter, x => x.Id == "embeddingEvent1");
        Assert.Contains(eventsInDbAfter, x => x.Id == "embeddingEvent2");
        Assert.Contains(eventsInDbAfter, x => x.Id == "embeddingEvent3");
        Assert.Contains(eventsInDbAfter, x => x.Id == "embeddingEvent4");
        Assert.Contains(eventsInDbAfter, x => x.Id == "embeddingEvent5");
        Assert.Contains(eventsInDbAfter, x => x.Id == "embeddingEvent6");
    }

    [Fact]
    public async Task SaveRagTextEmbedding_NewEmbedding_CreatesItem()
    {
        // Arrange
        string ragProjectId = "projectId";
        var ragProject = CreateTestRagProject(ragProjectId, "projectName", "projectDescription", 1);
        var textEmbedding = CreateTestRagTextEmbedding(ragProject.Id, "item1", null);
        textEmbedding.ContentHash = "hash1";

        // Act
        var embeddingsBefore = await GetAllEmbeddings();
        await _service.SaveRagTextEmbedding(ragProject, textEmbedding);
        var embeddingsAfter = await GetAllEmbeddings();

        // Assert
        Assert.Empty(embeddingsBefore);
        Assert.Single(embeddingsAfter);
        Assert.NotEmpty(embeddingsAfter[0].Id);
        Assert.Equal(ragProjectId, embeddingsAfter[0].RagProjectId);
        Assert.Equal("item1", embeddingsAfter[0].SourceItemId);
        Assert.Equal("hash1", embeddingsAfter[0].ContentHash);
    }

    [Fact]
    public async Task SaveRagTextEmbedding_ExistingEvent_UpdatesItem()
    {
        // Arrange
        string ragProjectId = "projectId";
        var ragProject = CreateTestRagProject(ragProjectId, "projectName", "projectDescription", 1);
        string embedId = "EmbeddingId";
        var textEmbeddingEvent = CreateTestRagTextEmbedding(ragProject.Id, "item1", embedId);
        textEmbeddingEvent.Originaltext = "OrigText";

        // Act
        await _ragEmbeddingContainer.CreateItemAsync(textEmbeddingEvent, new PartitionKey(textEmbeddingEvent.SourceItemId));
        var embeddingsBefore = await GetAllEmbeddings();
        textEmbeddingEvent.Originaltext = "NewOrigText";
        await _service.SaveRagTextEmbedding(ragProject, textEmbeddingEvent);
        var embeddingsAfter = await GetAllEmbeddings();

        // Assert
        Assert.Single(embeddingsBefore);
        Assert.Single(embeddingsAfter);
        Assert.Equal(embedId, embeddingsBefore[0].Id);
        Assert.Equal(embedId, embeddingsAfter[0].Id);
        Assert.Equal(ragProjectId, embeddingsBefore[0].RagProjectId);
        Assert.Equal(ragProjectId, embeddingsAfter[0].RagProjectId);
        Assert.Equal("item1", embeddingsBefore[0].SourceItemId);
        Assert.Equal("item1", embeddingsAfter[0].SourceItemId);
        Assert.Equal("OrigText", embeddingsBefore[0].Originaltext);
        Assert.Equal("NewOrigText", embeddingsAfter[0].Originaltext);
        Assert.Equal(embeddingsBefore[0].Created, embeddingsAfter[0].Created);
        Assert.True(embeddingsBefore[0].Updated < embeddingsAfter[0].Updated);
    }

    [Fact]
    public async Task DeleteRagEmbedding_NormalRun_ShouldDelete()
    {
        // Arrange
        var ragProject = CreateTestRagProject("testProjectId", "projectName", "projectDescription", 10);
        var embeddings = new List<RagTextEmbedding>
        {
            new RagTextEmbedding { Id = "embeddingEvent1", RagProjectId = ragProject.Id },
            new RagTextEmbedding { Id = "embeddingEvent2", RagProjectId = ragProject.Id },
            new RagTextEmbedding { Id = "embeddingEvent3", RagProjectId = ragProject.Id }
        };

        // Act
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.SourceItemId));
        }
        var eventsInDbBefore = await GetAllEmbeddings();
        await _service.DeleteRagEmbedding(ragProject, embeddings[1]);
        var eventsInDbAfter = await GetAllEmbeddings();

        // Assert
        Assert.Equal(3, eventsInDbBefore.Count);
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent1");
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent2");
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent3");
        Assert.Equal(2, eventsInDbAfter.Count);
        Assert.Contains(eventsInDbAfter, x => x.Id == "embeddingEvent1");
        Assert.DoesNotContain(eventsInDbAfter, x => x.Id == "embeddingEvent2");
        Assert.Contains(eventsInDbAfter, x => x.Id == "embeddingEvent3");
    }

    [Fact]
    public async Task DeleteRagEmbedding_EventDoesNotExist_ShouldNotDeleteAnythingAndNoException()
    {
        // Arrange
        var ragProject = CreateTestRagProject("testProjectId", "projectName", "projectDescription", 10);
        var embeddings = new List<RagTextEmbedding>
        {
            new RagTextEmbedding { Id = "embeddingEvent1", RagProjectId = ragProject.Id },
            new RagTextEmbedding { Id = "embeddingEvent2", RagProjectId = ragProject.Id },
            new RagTextEmbedding { Id = "embeddingEvent3", RagProjectId = ragProject.Id }
        };
        var embeddingNotInDb = new RagTextEmbedding { Id = "embeddingEvent4", RagProjectId = ragProject.Id };

        // Act
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.SourceItemId));
        }
        var eventsInDbBefore = await GetAllEmbeddings();
        await _service.DeleteRagEmbedding(ragProject, embeddingNotInDb);
        var eventsInDbAfter = await GetAllEmbeddings();

        // Assert
        Assert.Equal(3, eventsInDbBefore.Count);
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent1");
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent2");
        Assert.Contains(eventsInDbBefore, x => x.Id == "embeddingEvent3");
        Assert.Equal(3, eventsInDbAfter.Count);
        Assert.Contains(eventsInDbAfter, x => x.Id == "embeddingEvent1");
        Assert.Contains(eventsInDbAfter, x => x.Id == "embeddingEvent2");
        Assert.Contains(eventsInDbAfter, x => x.Id == "embeddingEvent3");
    }

    [Fact]
    public async Task GetAllDatabaseIdsAsync_NormalRun_ShouldReturnAllIds()
    {
        // Arrange

        // Act
        var result = await _service.GetAllDatabaseIdsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count() > 0);
        Assert.Contains("XunitRagItemDb", result);
        Assert.Contains("XunitRagProjects", result);
    }

    [Fact]
    public async Task GetEmbeddingIdsByProject_ValidProject_ReturnsIds()
    {
        // Arrange
        var ragProject = CreateTestRagProject("testProjectId", "projectName", "projectDescription", 10);
        var embeddings = new List<RagTextEmbedding>
        {
            new RagTextEmbedding { Id = "embedding1", SourceItemId = "source1", RagProjectId = ragProject.Id },
            new RagTextEmbedding { Id = "embedding2", SourceItemId = "source2", RagProjectId = ragProject.Id },
            new RagTextEmbedding { Id = "embedding3", SourceItemId = "source3", RagProjectId = ragProject.Id }
        };

        // Act
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.SourceItemId));
        }
        var result = await _service.GetEmbeddingIdsByProject(ragProject);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(embeddings.Count, result.Count());
        Assert.Contains("embedding1", result);
        Assert.Contains("embedding2", result);
        Assert.Contains("embedding3", result);
    }

    [Fact]
    public async Task DeleteDatabase_MainDatabase_ShouldDeleteDatabase()
    {
        // Arrange

        // Act
        var dbsBefore = await GetAllDatabaseNames();
        dbsBefore = dbsBefore.Where(x => x.StartsWith("Xunit")).ToList();
        await _service.DeleteDatabase(_ragProjectDefDbName);
        var dbsAfter = await GetAllDatabaseNames();
        dbsAfter = dbsAfter.Where(x => x.StartsWith("Xunit")).ToList();

        // Assert
        Assert.Equal(2, dbsBefore.Count);
        Assert.Equal(1, dbsAfter.Count);
        Assert.Contains(dbsBefore, x => x == _ragItemDbName);
        Assert.Contains(dbsBefore, x => x == _ragProjectDefDbName);
        Assert.Contains(dbsAfter, x => x == _ragItemDbName);
        Assert.DoesNotContain(dbsAfter, x => x == _ragProjectDefDbName);
    }

    [Fact]
    public async Task GetRagProjectByName_NormalRun_ShouldReturnItem()
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
        var result = await _service.GetRagProjectByName(ragProject.Name);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ragProject.Name, result.Name);
        Assert.Equal(ragProject.Description, result.Description);
        Assert.Empty(result.ContentItems);
    }

    [Fact]
    public async Task GetContentItemBySourceId_ItemExists_ReturnsContentItem()
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
                    RagProjectId = $"project1",
                    SourceSystemId = "source1"
                },
                new ContentItem
                {
                    Id = $"item2",
                    SystemName = "TestSystem",
                    ContentType = "INLINE",
                    ContentText = $"Test Content 2",
                    RagProjectId = $"project1",
                    SourceSystemId = "source2"
                }
            }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var projectItem in ragProject.ContentItems)
        {
            await _ragItemContainer.CreateItemAsync(projectItem, new PartitionKey(projectItem.Id));
        }
        var item1 = await _service.GetContentItemBySourceId(ragProject, "source1");
        var item2 = await _service.GetContentItemBySourceId(ragProject, "source2");

        // Assert
        Assert.NotNull(item1);
        Assert.Equal("source1", item1!.SourceSystemId);
        Assert.NotNull(item2);
        Assert.Equal("source2", item2!.SourceSystemId);
    }

    [Fact]
    public async Task GetContentItemBySourceId_ItemDoesNotExist_ReturnsNull()
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
                        RagProjectId = $"project1",
                        SourceSystemId = "source1"
                    },
                    new ContentItem
                    {
                        Id = $"item2",
                        SystemName = "TestSystem",
                        ContentType = "INLINE",
                        ContentText = $"Test Content 2",
                        RagProjectId = $"project1",
                        SourceSystemId = "source2"
                    }
                }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var projectItem in ragProject.ContentItems)
        {
            await _ragItemContainer.CreateItemAsync(projectItem, new PartitionKey(projectItem.Id));
        }
        var item3 = await _service.GetContentItemBySourceId(ragProject, "item3");

        // Assert
        Assert.Null(item3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetContentItemBySourceId_ItemIdNotSet_ThrowsArgumentException(string? itemId)
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
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetContentItemBySourceId(ragProject, itemId));
    }

    [Fact]
    public async Task AddRagTextEmbedding_NewEmbedding_CreatesEmbedding()
    {
        // Arrange
        string ragProjectId = "projectId";
        string originalText = "The text that was embedded";
        var ragProject = CreateTestRagProject(ragProjectId, "projectName", "projectDescription", 1);
        var contentItem = new ContentItem
        {
            Id = "item1",
            ContentType = "INLINE",
            ContentText = "Test Content 1",
            RagProjectId = ragProjectId,
            Title = "Test Title 1",
            Description = "Test Description 1",
        };
        var textEmbedding = CreateTestRagTextEmbedding(ragProject.Id, "item1", null);
        var expectedContentHash = "55158C150EB6890E87E15F935ADCE655";
        var mockEmbedding = new float[] { 0.1f, 0.2f, 0.3f };

        // Act
        var embeddingsBefore = await GetAllEmbeddings();
        await _service.AddRagTextEmbedding(ragProject, contentItem, EmbeddingSourceType.Question, mockEmbedding, originalText);
        var embeddingsAfter = await GetAllEmbeddings();

        // Assert
        Assert.Empty(embeddingsBefore);
        Assert.Single(embeddingsAfter);
        Assert.NotEmpty(embeddingsAfter[0].Id);
        Assert.Equal(ragProjectId, embeddingsAfter[0].RagProjectId);
        Assert.Equal("item1", embeddingsAfter[0].SourceItemId);
        Assert.Equal(expectedContentHash, embeddingsAfter[0].ContentHash);
        Assert.Equal(originalText, embeddingsAfter[0].Originaltext);
        Assert.Equal(0.1f, embeddingsAfter[0].Embedding[0]);
        Assert.Equal(0.2f, embeddingsAfter[0].Embedding[1]);
        Assert.Equal(0.3f, embeddingsAfter[0].Embedding[2]);
    }

    [Fact]
    public async Task GetEmbeddingEventsByItemId_ValidProject_ShouldGetCorrectItems()
    {
        // Arrange
        var ragProject = CreateTestRagProject("testProjectId", "projectName", "projectDescription", 10);

        var embeddings = new List<EmbeddingEvent>
        {
            new EmbeddingEvent { Id = "embeddingEvent1", RagProjectId = ragProject.Id, ContentItemId = "ItemId1" },
            new EmbeddingEvent { Id = "embeddingEvent2", RagProjectId = ragProject.Id, ContentItemId = "ItemId2" },
            new EmbeddingEvent { Id = "embeddingEvent3", RagProjectId = ragProject.Id, ContentItemId = "ItemId1" }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingEventContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.RagProjectId));
        }
        var result = await _service.GetEmbeddingEventsByItemId(ragProject, "ItemId1");
        var eventsInDb = await GetAllEmbeddingEvents();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, eventsInDb.Count);
        Assert.Equal(2, result.Count());
        Assert.Contains(eventsInDb, x => x.Id == "embeddingEvent1");
        Assert.Contains(eventsInDb, x => x.Id == "embeddingEvent3");
    }

    [Fact]
    public async Task GetItemSourceIdsByProject_ValidProject_ReturnsIds()
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
                        RagProjectId = $"project1",
                        SourceSystemId = "source1"
                    },
                    new ContentItem
                    {
                        Id = $"item2",
                        SystemName = "TestSystem",
                        ContentType = "INLINE",
                        ContentText = $"Test Content 2",
                        RagProjectId = $"project1",
                        SourceSystemId = "source2"
                    },
                    new ContentItem
                    {
                        Id = $"item3",
                        SystemName = "TestSystem",
                        ContentType = "INLINE",
                        ContentText = $"Test Content 3",
                        RagProjectId = $"project1",
                        SourceSystemId = "source3"
                    }
                }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var projectItem in ragProject.ContentItems)
        {
            await _ragItemContainer.CreateItemAsync(projectItem, new PartitionKey(projectItem.Id));
        }
        var result = await _service.GetItemSourceSystemIdsByProject(ragProject);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count());
        Assert.Equal("source1", result[0]);
        Assert.Equal("source2", result[1]);
        Assert.Equal("source3", result[2]);
    }

    [Fact]
    public async Task GetEmbeddingsByItemId_ValidProject_ReturnsEmbeddings()
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
            new RagTextEmbedding { Id = "embedding2", SourceItemId = "source2", RagProjectId = ragProject.Id },
            new RagTextEmbedding { Id = "embedding3", SourceItemId = "source1", RagProjectId = ragProject.Id }
        };

        // Act
        await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        foreach (var projectembeddingItem in embeddings)
        {
            await _ragEmbeddingContainer.CreateItemAsync(projectembeddingItem, new PartitionKey(projectembeddingItem.SourceItemId));
        }
        var result = await _service.GetEmbeddingsByItemId(ragProject, "source1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("embedding1", result[0].Id);
        Assert.Equal("embedding3", result[1].Id);
    }

    private async Task<List<RagTextEmbedding>> GetAllEmbeddings()
    {
        var query = "SELECT * FROM c";
        var queryDefinition = new QueryDefinition(query);
        var embeddingIterator = _ragEmbeddingContainer.GetItemQueryIterator<RagTextEmbedding>(queryDefinition);
        var result = new List<RagTextEmbedding>();
        while (embeddingIterator.HasMoreResults)
        {
            var item = await embeddingIterator.ReadNextAsync();
            result.AddRange(item);
        }
        return result;
    }

    private async Task<List<string>> GetAllDatabaseNames()
    {
        var databaseList = new List<string>();
        var iterator = _cosmosClient.GetDatabaseQueryIterator<DatabaseProperties>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var database in response)
            {
                databaseList.Add(database.Id);
            }
        }

        return databaseList;
    }

    private async Task<List<EmbeddingEvent>> GetAllEmbeddingEvents()
    {
        var query = "SELECT * FROM c";
        var queryDefinition = new QueryDefinition(query);
        var embeddingEventIterator = _ragEmbeddingEventContainer.GetItemQueryIterator<EmbeddingEvent>(queryDefinition);
        var result = new List<EmbeddingEvent>();
        while (embeddingEventIterator.HasMoreResults)
        {
            var item = await embeddingEventIterator.ReadNextAsync();
            result.AddRange(item);
        }
        return result;
    }

    private RagTextEmbedding CreateTestRagTextEmbedding(string ragProjectId,
                                                        string itemId,
                                                        string embeddingId)
    {
        return new RagTextEmbedding()
        {
            Id = embeddingId,
            SourceItemId = itemId,
            RagProjectId = ragProjectId            
        };
    }

    private EmbeddingEvent CreateTestRagTextEmbeddingEvent(string ragProjectId,
                                                           string itemId,
                                                           string embeddingId)
    {
        return new EmbeddingEvent()
        {
            Id = embeddingId,                       
            RagProjectId = "projectId",
            ContentItemId = itemId
        };
    }

    private RagProject CreateTestRagProject(string id, 
                                            string name,
                                            string description,
                                            int numItems)
    {
        var result = new RagProject
        {
            Id = id,
            Name = name,
            Description = description,
            Configuration = new RagConfiguration
            {
                DbName = _ragItemDbName,
                ItemCollectionName = _ragItemContainerName,
                EmbeddingCollectioName = _ragEmbeddingContainerName,
                EmbeddingEventCollectioName = _ragEmbeddingEventContainerName
            }
        };
        for (int i = 0; i < numItems; i++)
        {
            result.ContentItems.Add(new ContentItem
            {
                Id = $"item{i}",
                SystemName = "TestSystem",
                ContentType = "INLINE",
                ContentText = $"Test Content {i}",
                RagProjectId = id,
                ContentNeedsEmbeddingUpdate = true,
            });
        }
        return result;
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
