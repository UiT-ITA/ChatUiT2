using ChatUiT2.Services;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver.Core.Configuration;
using MongoDB.Driver;
using Moq;
using UiT.ChatUiT2.MaintenanceFunctions.Functions;
using UiT.ChatUiT2.MaintenanceFunctions.Tests.TestStaging;
using UiT.CommonToolsLib.Services;
using MongoDB.Bson;
using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using MongoDB.Bson.Serialization;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;

namespace UiT.ChatUiT2.MaintenanceFunctions.Tests.Functions;

public class DeleteAbandonedChatsTests
{
    /// <summary>
    /// Test that chats that have not been updated in seven days and that are not marked as favorite are deleted.
    /// Testing with one chat that match and one that does not match the criteria.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task DeleteAbandonedChats_NormalRun_ShouldDeleteExpiredWorkItems()
    {
        // Arrange
        var logger = new TestLogger<DeleteAbandonedChats>();
        DateTime utcNow = new DateTime(2024, 6, 8, 14, 0, 0, DateTimeKind.Utc);
        DateTime cutoffTime = new DateTime(2024, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(x => x.UtcNow).Returns(utcNow);
        string connStr = "mongodb://localhost:27017";
        string dbName = "Users";
        var inMemorySettings = new Dictionary<string, string> {
            {"ConnectionStrings:MongoDb", connStr},
            {"UseEncryption", "false"}
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        var databaseService = new DatabaseService(configuration,
                                                  null,
                                                  null,
                                                  dateTimeProvider.Object);

        var client = new MongoClient(connStr);
        client.DropDatabase(dbName);
        var userDatabase = client.GetDatabase(dbName);

        IMongoCollection<BsonDocument> _userCollection = userDatabase.GetCollection<BsonDocument>("Users");
        IMongoCollection<BsonDocument> _chatCollection = userDatabase.GetCollection<BsonDocument>("Chats");
        IMongoCollection<BsonDocument> _chatMessageCollection = userDatabase.GetCollection<BsonDocument>("ChatMessages");
        IMongoCollection<BsonDocument> _fileCollection = userDatabase.GetCollection<BsonDocument>("Files");

        #region user1
        var userPref1 = new Preferences()
        {            
            ChatWidth = ChatWidth.Medium,
            Language = "en",
            SmoothOutput = true,
            UseMarkdown = true,
            SaveHistory = true,
            DarkMode = true,
            DefaultChatSettings = new()
            {
                MaxTokens = 1024,
                Temperature = 0.5f,
                Prompt = "Prompt",
                Model = "GPT-40",
            }
        };
        string jsonText = JsonSerializer.Serialize(userPref1);
        string user1Username = "Username1";
        var document = new BsonDocument
        {
            {"_id", Guid.NewGuid().ToString()},
            {"Username", user1Username},
            {"Preferences", jsonText}
        };
        _userCollection.InsertOne(document);
        #endregion
        #region user1chat1
        WorkItemChat chat1 = new()
        {
            IsFavorite = false,
            Name = "Chat1",
            Settings = new()
            {
                MaxTokens = 1024,
                Temperature = 0.5f,
                Prompt = "Prompt",
                Model = "GPT-40",
            },
            Messages = new List<ChatMessage>()
            {
                new ChatMessage()
                {
                    Role = ChatMessageRole.User,
                    Content = "Content1",
                    Status = ChatMessageStatus.Done,
                    Files = new List<ChatFile>()
                    {
                        new("File1", new byte[] { 1, 2, 3}),
                        new("File2", new byte[] { 4, 5, 6})
                    }
                },
                new ChatMessage()
                {
                    Role = ChatMessageRole.User,
                    Content = "Content1",
                    Status = ChatMessageStatus.Done,
                    Files = new List<ChatFile>()
                    {
                        new("File3", new byte[] { 1, 2, 3}),
                        new("File4", new byte[] { 4, 5, 6})
                    }
                }
            },
            Created = cutoffTime.AddSeconds(-1),
            Updated = cutoffTime.AddSeconds(-1),
            Persistant = true,
            State = WorkItemState.Ready,
            Type = WorkItemType.Chat
        };
        string chatJsonText = JsonSerializer.Serialize(chat1);
        var chatDocument = new BsonDocument
        {
            {"_id", chat1.Id},
            {"Username", user1Username},
            {"Data", chatJsonText},
            {"Type", chat1.Type.ToString()},
            {"Updated", cutoffTime.AddSeconds(-1)},
            {"Permanent", false}
        };
        _chatCollection.InsertOne(chatDocument);
        foreach(var chatObj in chat1.Messages)
        {
            var chatMsgDocument = new BsonDocument
            {
                {"_id", chatObj.Id},
                {"ChatId", chat1.Id},
                {"Username", user1Username},
                {"Content", chatObj.Content},
                {"Role", (int)chatObj.Role},
                {"Status", (int)chatObj.Status},
                {"Created", chatObj.Created},
                {"Files", new BsonArray(chatObj.Files.Select(f => f.Id)) }
            };
            _chatMessageCollection.InsertOne(chatMsgDocument);
            foreach (var file in chatObj.Files)
            {
                var fileDocument = new BsonDocument
                {
                    {"_id", file.Id},
                    {"MessageId", chatObj.Id},
                    {"ChatId", chat1.Id},
                    {"Username", user1Username },
                    {"FileName", file.FileName},
                    {"Parts", new BsonArray()}
                };
                await _fileCollection.InsertOneAsync(fileDocument);
            }
        }
        #endregion
        
        #region user1chat2
        WorkItemChat chat2 = new()
        {
            IsFavorite = false,
            Name = "Chat2",
            Settings = new()
            {
                MaxTokens = 1024,
                Temperature = 0.5f,
                Prompt = "Prompt",
                Model = "GPT-40",
            },
            Messages = new List<ChatMessage>()
            {
                new ChatMessage()
                {
                    Role = ChatMessageRole.User,
                    Content = "Content1",
                    Status = ChatMessageStatus.Done,
                    Files = new List<ChatFile>()
                    {
                        new("File1", new byte[] { 1, 2, 3}),
                        new("File2", new byte[] { 4, 5, 6})
                    }
                },
                new ChatMessage()
                {
                    Role = ChatMessageRole.User,
                    Content = "Content1",
                    Status = ChatMessageStatus.Done,
                    Files = new List<ChatFile>()
                    {
                        new("File3", new byte[] { 1, 2, 3}),
                        new("File4", new byte[] { 4, 5, 6})
                    }
                }
            },
            Created = cutoffTime.AddSeconds(-1),
            Updated = cutoffTime.AddSeconds(-1),
            Persistant = true,
            State = WorkItemState.Ready,
            Type = WorkItemType.Chat
        };
        string chatJsonText2 = JsonSerializer.Serialize(chat2);
        var chatDocument2 = new BsonDocument
        {
            {"_id", chat2.Id},
            {"Username", user1Username},
            {"Data", chatJsonText2},
            {"Type", chat2.Type.ToString()},
            {"Updated", cutoffTime.AddSeconds(-1)},
            {"Permanent", true}
        };
        _chatCollection.InsertOne(chatDocument2);
        foreach (var chatObj in chat2.Messages)
        {
            var chatMsgDocument = new BsonDocument
            {
                {"_id", chatObj.Id},
                {"ChatId", chat2.Id},
                {"Username", user1Username},
                {"Content", chatObj.Content},
                {"Role", (int)chatObj.Role},
                {"Status", (int)chatObj.Status},
                {"Created", chatObj.Created},
                {"Files", new BsonArray(chatObj.Files.Select(f => f.Id)) }
            };
            _chatMessageCollection.InsertOne(chatMsgDocument);
            foreach (var file in chatObj.Files)
            {
                var fileDocument = new BsonDocument
                {
                    {"_id", file.Id},
                    {"MessageId", chatObj.Id},
                    {"ChatId", chat2.Id},
                    {"Username", user1Username },
                    {"FileName", file.FileName},
                    {"Parts", new BsonArray()}
                };
                await _fileCollection.InsertOneAsync(fileDocument);
            }
        }
        #endregion

        #region user2
        var userPref2 = new Preferences()
        {
            ChatWidth = ChatWidth.Medium,
            Language = "en",
            SmoothOutput = true,
            UseMarkdown = true,
            SaveHistory = true,
            DarkMode = true,
            DefaultChatSettings = new()
            {
                MaxTokens = 1024,
                Temperature = 0.5f,
                Prompt = "Prompt",
                Model = "GPT-40",
            }
        };
        string jsonText2 = JsonSerializer.Serialize(userPref2);
        string user2Username = "Username2";
        var document2 = new BsonDocument
        {
            {"_id", Guid.NewGuid().ToString()},
            {"Username", user2Username},
            {"Preferences", jsonText}
        };
        _userCollection.InsertOne(document2);
        #endregion
        
        #region user2chat1
        WorkItemChat chat3 = new()
        {
            IsFavorite = false,
            Name = "Chat3",
            Settings = new()
            {
                MaxTokens = 1024,
                Temperature = 0.5f,
                Prompt = "Prompt",
                Model = "GPT-40",
            },
            Messages = new List<ChatMessage>()
            {
                new ChatMessage()
                {
                    Role = ChatMessageRole.User,
                    Content = "Content1",
                    Status = ChatMessageStatus.Done,
                    Files = new List<ChatFile>()
                    {
                        new("File1", new byte[] { 1, 2, 3}),
                        new("File2", new byte[] { 4, 5, 6})
                    }
                },
                new ChatMessage()
                {
                    Role = ChatMessageRole.User,
                    Content = "Content1",
                    Status = ChatMessageStatus.Done,
                    Files = new List<ChatFile>()
                    {
                        new("File3", new byte[] { 1, 2, 3}),
                        new("File4", new byte[] { 4, 5, 6})
                    }
                }
            },
            Created = cutoffTime.AddSeconds(-1),
            Updated = cutoffTime.AddSeconds(-1),
            Persistant = true,
            State = WorkItemState.Ready,
            Type = WorkItemType.Chat
        };
        string chatJsonText3 = JsonSerializer.Serialize(chat3);
        var chatDocument3 = new BsonDocument
        {
            {"_id", chat3.Id},
            {"Username", user2Username},
            {"Data", chatJsonText3},
            {"Type", chat3.Type.ToString()},
            {"Updated", cutoffTime.AddSeconds(-1)},
            {"Permanent", false}
        };
        _chatCollection.InsertOne(chatDocument3);
        foreach (var chatObj in chat3.Messages)
        {
            var chatMsgDocument = new BsonDocument
            {
                {"_id", chatObj.Id},
                {"ChatId", chat3.Id},
                {"Username", user2Username},
                {"Content", chatObj.Content},
                {"Role", (int)chatObj.Role},
                {"Status", (int)chatObj.Status},
                {"Created", chatObj.Created},
                {"Files", new BsonArray(chatObj.Files.Select(f => f.Id)) }
            };
            _chatMessageCollection.InsertOne(chatMsgDocument);
            foreach (var file in chatObj.Files)
            {
                var fileDocument = new BsonDocument
                {
                    {"_id", file.Id},
                    {"MessageId", chatObj.Id},
                    {"ChatId", chat3.Id},
                    {"Username", user2Username },
                    {"FileName", file.FileName},
                    {"Parts", new BsonArray()}
                };
                await _fileCollection.InsertOneAsync(fileDocument);
            }
        }
        #endregion

        #region user2chat2
        WorkItemChat chat4 = new()
        {
            IsFavorite = false,
            Name = "Chat4",
            Settings = new()
            {
                MaxTokens = 1024,
                Temperature = 0.5f,
                Prompt = "Prompt",
                Model = "GPT-40",
            },
            Messages = new List<ChatMessage>()
            {
                new ChatMessage()
                {
                    Role = ChatMessageRole.User,
                    Content = "Content1",
                    Status = ChatMessageStatus.Done,
                    Files = new List<ChatFile>()
                    {
                        new("File1", new byte[] { 1, 2, 3}),
                        new("File2", new byte[] { 4, 5, 6})
                    }
                },
                new ChatMessage()
                {
                    Role = ChatMessageRole.User,
                    Content = "Content1",
                    Status = ChatMessageStatus.Done,
                    Files = new List<ChatFile>()
                    {
                        new("File3", new byte[] { 1, 2, 3}),
                        new("File4", new byte[] { 4, 5, 6})
                    }
                }
            },
            Created = cutoffTime,
            Updated = cutoffTime,
            Persistant = true,
            State = WorkItemState.Ready,
            Type = WorkItemType.Chat
        };
        string chatJsonText4 = JsonSerializer.Serialize(chat4);
        var chatDocument4 = new BsonDocument
        {
            {"_id", chat4.Id},
            {"Username", user2Username},
            {"Data", chatJsonText4},
            {"Type", chat4.Type.ToString()},
            {"Updated", cutoffTime},
            {"Permanent", false}
        };
        _chatCollection.InsertOne(chatDocument4);
        foreach (var chatObj in chat4.Messages)
        {
            var chatMsgDocument = new BsonDocument
            {
                {"_id", chatObj.Id},
                {"ChatId", chat4.Id},
                {"Username", user2Username},
                {"Content", chatObj.Content},
                {"Role", (int)chatObj.Role},
                {"Status", (int)chatObj.Status},
                {"Created", chatObj.Created},
                {"Files", new BsonArray(chatObj.Files.Select(f => f.Id)) }
            };
            _chatMessageCollection.InsertOne(chatMsgDocument);
            foreach (var file in chatObj.Files)
            {
                var fileDocument = new BsonDocument
                {
                    {"_id", file.Id},
                    {"MessageId", chatObj.Id},
                    {"ChatId", chat4.Id},
                    {"Username", user2Username },
                    {"FileName", file.FileName},
                    {"Parts", new BsonArray()}
                };
                await _fileCollection.InsertOneAsync(fileDocument);
            }
        }
        #endregion

        DeleteAbandonedChats function = new(logger, dateTimeProvider.Object, databaseService);

        // Act
        var chatsInDbBefore = _chatCollection.Find(new BsonDocument()).ToList();
        var msgInDbBefore = _chatMessageCollection.Find(new BsonDocument()).ToList();
        var filesInDbBefore = _fileCollection.Find(new BsonDocument()).ToList();
        await function.Run(new TimerInfo());
        var chatsInDbAfter = _chatCollection.Find(new BsonDocument()).ToList();
        var msgInDbAfter = _chatMessageCollection.Find(new BsonDocument()).ToList();
        var filesInDbAfter = _fileCollection.Find(new BsonDocument()).ToList();

        // Assert
        Assert.Equal(4, chatsInDbBefore.Count);
        Assert.Equal(2, chatsInDbAfter.Count);
        Assert.Contains(chatsInDbAfter, x => x["_id"].AsString == chat2.Id);
        Assert.Contains(chatsInDbAfter, x => x["_id"].AsString == chat4.Id);
        Assert.Contains(chatsInDbAfter, x => x["Username"].AsString == user1Username);
        Assert.Contains(chatsInDbAfter, x => x["Username"].AsString == user2Username);
        Assert.Equal(8, msgInDbBefore.Count);
        Assert.Equal(4, msgInDbAfter.Count);
        Assert.Equal(16, filesInDbBefore.Count);
        Assert.Equal(8, filesInDbAfter.Count);
        Assert.DoesNotContain(filesInDbAfter, x => x["_id"].AsString == chat1.Messages[0].Files[0].Id);
        Assert.DoesNotContain(filesInDbAfter, x => x["_id"].AsString == chat1.Messages[0].Files[1].Id);
        Assert.DoesNotContain(filesInDbAfter, x => x["_id"].AsString == chat1.Messages[1].Files[0].Id);
        Assert.DoesNotContain(filesInDbAfter, x => x["_id"].AsString == chat1.Messages[1].Files[1].Id);
        Assert.Contains(filesInDbAfter, x => x["_id"].AsString == chat2.Messages[0].Files[0].Id);
        Assert.Contains(filesInDbAfter, x => x["_id"].AsString == chat2.Messages[0].Files[1].Id);
        Assert.Contains(filesInDbAfter, x => x["_id"].AsString == chat2.Messages[1].Files[0].Id);
        Assert.Contains(filesInDbAfter, x => x["_id"].AsString == chat2.Messages[1].Files[1].Id);
        Assert.DoesNotContain(filesInDbAfter, x => x["_id"].AsString == chat3.Messages[0].Files[0].Id);
        Assert.DoesNotContain(filesInDbAfter, x => x["_id"].AsString == chat3.Messages[0].Files[1].Id);
        Assert.DoesNotContain(filesInDbAfter, x => x["_id"].AsString == chat3.Messages[1].Files[0].Id);
        Assert.DoesNotContain(filesInDbAfter, x => x["_id"].AsString == chat3.Messages[1].Files[1].Id);
        Assert.Contains(filesInDbAfter, x => x["_id"].AsString == chat4.Messages[0].Files[0].Id);
        Assert.Contains(filesInDbAfter, x => x["_id"].AsString == chat4.Messages[0].Files[1].Id);
        Assert.Contains(filesInDbAfter, x => x["_id"].AsString == chat4.Messages[1].Files[0].Id);
        Assert.Contains(filesInDbAfter, x => x["_id"].AsString == chat4.Messages[1].Files[1].Id);

    }
}
