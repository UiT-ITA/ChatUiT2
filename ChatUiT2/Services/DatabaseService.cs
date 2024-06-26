using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using ChatUiT2.Tools;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.Text.Json;

namespace ChatUiT2.Services;

public class DatabaseService : IDatabaseService
{
    // Services
    private readonly IKeyVaultService _keyVaultService;
    private readonly IEncryptionService _encryptionService;
    private StorageService _storageService;

    // Collections
    private readonly IMongoCollection<BsonDocument> _configCollection;
    private readonly IMongoCollection<BsonDocument> _userCollection;
    private readonly IMongoCollection<BsonDocument> _workItemCollection;
    private readonly IMongoCollection<BsonDocument> _chatMessageCollection;

    // Settings
    private readonly bool _useEncryption;

    public DatabaseService(IConfiguration configuration, 
                           IEncryptionService encryptionService, 
                           IKeyVaultService keyVaultService,
                           StorageService storageService)
    {
        _keyVaultService = keyVaultService;
        _encryptionService = encryptionService;
        _storageService = storageService;

        var connectionString = configuration.GetConnectionString("MongoDb");

        var client = new MongoClient(connectionString);
        //var systemDatabase = client.GetDatabase(configuration["DBSettings:SystemDatabaseName"]);
        //var userDatabase = client.GetDatabase(configuration["DBSettings:UserDatabaseName"]);

        //_configCollection = systemDatabase.GetCollection<BsonDocument>(configuration["DBSettings:ConfigCollectionName"]);
        //_userCollection = userDatabase.GetCollection<BsonDocument>(configuration["DBSettings:UserCollectionName"]);
        //_workItemCollection = userDatabase.GetCollection<BsonDocument>(configuration["DBSettings:WorkItemCollectionName"]);
        //_chatMessageCollection = userDatabase.GetCollection<BsonDocument>(configuration["DBSettings:ChatMessageCollectionName"]);

        var systemDatabase = client.GetDatabase("System");
        var userDatabase = client.GetDatabase("Users");

        _configCollection = systemDatabase.GetCollection<BsonDocument>("Configuration");
        _userCollection = userDatabase.GetCollection<BsonDocument>("Users");
        _workItemCollection = userDatabase.GetCollection<BsonDocument>("WorkItems");
        _chatMessageCollection = userDatabase.GetCollection<BsonDocument>("ChatMessages");

        _useEncryption = configuration.GetValue<bool>("DBSettings:UseEncryption", defaultValue: true);
    }


    // Users
    /// <summary>
    /// Get user preferences from the database
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    public async Task<Preferences> GetUserPreferences(string username)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("Username", username);
        var document = await _userCollection.Find(filter).FirstOrDefaultAsync();

        if (document != null)
        {
            return BsonSerializer.Deserialize<Preferences>(document["Preferences"].AsBsonDocument);
        }

        // Load default preferences
        var filterDefault = Builders<BsonDocument>.Filter.Eq("Username", "default");
        var documentDefault = await _userCollection.Find(filterDefault).FirstOrDefaultAsync();

        Preferences preferences;

        if (documentDefault != null) {
            preferences = BsonSerializer.Deserialize<Preferences>(documentDefault["Preferences"].AsBsonDocument);
        }
        else
        {
            // No default preferences found, create new
            preferences = new Preferences();
            await SaveUserPreferences("default", preferences);
        }
        await SaveUserPreferences(username, preferences);
        return preferences;
    }
    
    /// <summary>
    /// Save user preferences to the database
    /// </summary>
    /// <param name="username"></param>
    /// <param name="preferences"></param>
    /// <returns></returns>
    public async Task SaveUserPreferences(string username, Preferences preferences)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("Username", username);
        var update = Builders<BsonDocument>.Update.Set("Preferences", preferences);
        await _userCollection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
    }

    /// <summary>
    /// Delete user and all associated data from the database
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    public async Task DeleteUser(string username)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("Username", username);
        await _userCollection.DeleteManyAsync(filter);
        await _workItemCollection.DeleteManyAsync(filter);
        await _chatMessageCollection.DeleteManyAsync(filter);
    }


    // WorkItems
    /// <summary>
    /// Get a list of work items for a user from the database
    /// </summary>
    /// <param name="user"></param>
    /// <returns>List of work items belonging to a user</returns>
    public async Task<List<IWorkItem>> GetWorkItemList(User user)
    {
        var workItems = new List<IWorkItem>();
        var filter = Builders<BsonDocument>.Filter.Eq("Username", user.Username);
        var sort = Builders<BsonDocument>.Sort.Descending("Updated");
        var documents = await _workItemCollection.Find(filter).ToListAsync();
        var tasks = documents.Select(async doc =>
        {
            if (doc["Type"] == WorkItemType.Chat.ToString())
            {
                try
                {
                    var workItem = JsonSerializer.Deserialize<WorkItemChat>(doc["Data"].AsString);
                    if (workItem != null)
                    {
                        workItem.Messages = await GetChatMessages(user, workItem.Id);
                        return workItem;
                    }
                }
                catch (Exception ex)
                {
                    await DeleteWorkItem(user, new WorkItemChat { Id = doc["_id"].AsString });
                    Console.WriteLine("Error loading chat: " + ex.Message);
                }
            }
            else
            {
                throw new Exception("Unknown work item type");
            }
            return null;
        }).ToList();
        var results = await Task.WhenAll(tasks);
        workItems.AddRange(results.Where(item => item != null));
        return workItems;
    }

    public async Task<List<IWorkItem>> GetWorkITemListLazy(User user, Action onWorkItemLoaded)
    {
        var workItems = new List<IWorkItem>();
        var filter = Builders<BsonDocument>.Filter.Eq("Username", user.Username);
        var sort = Builders<BsonDocument>.Sort.Descending("Updated");
        var documents = await _workItemCollection.Find(filter).ToListAsync();
        foreach (var doc in documents)
        {
            if (doc["Type"] == WorkItemType.Chat.ToString())
            {
                var workItem = JsonSerializer.Deserialize<WorkItemChat>(doc["Data"].AsString);
                if (workItem != null)
                {
                    workItem.Loading = true;
                    workItems.Add(workItem);
                    // Start loading messages in the background
                    _ = LoadWorkItemComponentsAsync(user, workItem, onWorkItemLoaded);
                }
            }
            else
            {
                throw new Exception("Unknown work item type");
            }
        }
        return workItems;
    }

    private async Task LoadWorkItemComponentsAsync(User user, WorkItemChat workItem, Action onWorkItemLoaded)
    {
        try
        {
            workItem.Messages = await GetChatMessages(user, workItem.Id);
        }
        catch (Exception ex)
        {
            // TODO: Better error handling on workitem load
            //await DeleteWorkItem(user, new WorkItemChat { Id = workItem.Id });
            Console.WriteLine("Error loading chat: " + ex.Message);
        }
        finally
        {
            workItem.Loading = false;
            onWorkItemLoaded?.Invoke();
        }
    }

    /// <summary>
    /// Save a single work item to the database
    /// </summary>
    /// <param name="user"></param>
    /// <param name="workItem"></param>
    /// <returns></returns>
    public async Task SaveWorkItem(User user, IWorkItem workItem)
    {
        if (workItem.Persistant == false)
        {
            await DeleteWorkItem(user, workItem);
            return;
        }
        string jsonText = string.Empty;

        if (workItem.Type == WorkItemType.Chat)
        {
            jsonText = JsonSerializer.Serialize((WorkItemChat)workItem);
        }
        else
        {
            throw new Exception("Unknown work item type");
        }

        // Why not this?
        //var document = BsonDocument.Parse(jsonText);

        // Why was this done?
        var document = new BsonDocument
        {
            {"_id", workItem.Id},
            {"Username", user.Username},
            {"Data", jsonText},
            {"Type", workItem.Type.ToString()},
            {"Updated", workItem.Updated},
            {"Permanent", workItem.IsFavorite}
        };

        var filter = Builders<BsonDocument>.Filter.Eq("_id", workItem.Id);
        var options = new ReplaceOptions { IsUpsert = true };

        await _workItemCollection.ReplaceOneAsync(filter, document, options);
        await SaveChatMessages(user, (WorkItemChat)workItem);
    }

    /// <summary>
    /// Delete a single work item from the database
    /// </summary>
    /// <param name="user"></param>
    /// <param name="workItem"></param>
    /// <returns></returns>
    public async Task DeleteWorkItem(User user, IWorkItem workItem)
    {
        if (workItem.Type == WorkItemType.Chat)
        {
            await DeleteChat((WorkItemChat)workItem);
        }

        var filter = Builders<BsonDocument>.Filter.Eq("_id", workItem.Id);
        try
        {
            await _workItemCollection.DeleteOneAsync(filter);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error deleting work item: " + ex.Message);
        }
    }

    // ChatMessages
    /// <summary>
    /// Get chat messages for a chat from the database
    /// </summary>
    /// <param name="user"></param>
    /// <param name="chatId"></param>
    /// <returns>List of chat messages belonging to the chat</returns>
    private async Task<List<ChatMessage>> GetChatMessages(User user, string chatId)
    {
        var messages = new List<ChatMessage>();

        var filter = Builders<BsonDocument>.Filter.Eq("ChatId", chatId)
            & Builders<BsonDocument>.Filter.Eq("Username", user.Username);

        var documents = await _chatMessageCollection.Find(filter).ToListAsync();

        foreach (var doc in documents)
        {
            string content;

            if (_useEncryption)
            {
                if (user.AesKey == null)
                {
                    user.AesKey = await _keyVaultService.GetKeyAsync(user.Username);
                }
                content = _encryptionService.Decrypt(doc["Content"].AsByteArray, user.AesKey);
            }
            else
            {
                content = doc["Content"].AsString;
            }

            // Get files
            var files = new List<ChatFile>();
            if (doc.Contains("Files"))
            {
                foreach (var file in doc["Files"].AsBsonArray)
                {
                    files.Add(await _storageService.GetFile(chatId, file.AsString));
                }
            }

            messages.Add(new ChatMessage
            {
                Id = doc["_id"].AsString,
                Role = (ChatMessageRole)doc["Role"].AsInt32,
                Content = content,
                Status = (ChatMessageStatus)doc["Status"].AsInt32,
                Created = doc["Created"].ToUniversalTime(),
                Files = files

            });
        }

        return messages.OrderBy(i => i.Created).ToList();
    }

    /// <summary>
    /// Save chat messages to the database
    /// </summary>
    /// <param name="user"></param>
    /// <param name="chat"></param>
    /// <returns></returns>
    public async Task SaveChatMessages(User user, WorkItemChat chat)
    {
        List<Task> tasks = new List<Task>();

        foreach (var message in chat.Messages)
        {
            if (chat.SavedTime != null && message.Created < chat.SavedTime)
            {
                continue;
            }

            tasks.Add(SaveChatMessage(user, message, chat));
        }

        await Task.WhenAll(tasks);
        chat.SavedTime = DateTimeTools.GetTimestamp();
    }

    /// <summary>
    /// Save a single chat message to the database
    /// </summary>
    /// <param name="user"></param>
    /// <param name="message"></param>
    /// <param name="chatId"></param>
    /// <returns></returns>
    private async Task SaveChatMessage(User user, ChatMessage message, WorkItemChat chat)
    {
        if (_useEncryption && user.AesKey == null)
        {
            user.AesKey = await _keyVaultService.GetKeyAsync(user.Username);
        }
        // save files
        List<Task> tasks = new List<Task>();
        foreach (ChatFile file in message.Files)
        {
            tasks.Add(_storageService.UploadFile(chat, file));
        }
        await Task.WhenAll(tasks);

        var document = new BsonDocument
        {
            {"_id", message.Id},
            {"ChatId", chat.Id},
            {"Username", user.Username},
            {"Content", _useEncryption ? _encryptionService.Encrypt(message.Content, user.AesKey!) : message.Content},
            {"Role", (int)message.Role},
            {"Status", (int)message.Status},
            {"Created", message.Created},
            {"Files", new BsonArray(message.Files.Select(f => f.FileName)) }
        };

        var fileter = Builders<BsonDocument>.Filter.Eq("_id", message.Id);
        var options = new ReplaceOptions { IsUpsert = true };

        await _chatMessageCollection.ReplaceOneAsync(fileter, document, options);


    }

    /// <summary>
    /// Delete any missing messages
    /// </summary>
    /// <param name="user"></param>
    /// <param name="chat"></param>
    /// <returns></returns>
    public async Task DeleteMissingMessages(User user, WorkItemChat chat)
    {
        var existingMessages = await GetChatMessages(user, chat.Id);
        var messagesToDelete = existingMessages.Where(m => !chat.Messages.Any(m2 => m2.Id == m.Id)).ToList();

        List<Task> tasks = new List<Task>();
        foreach (var message in messagesToDelete)
        {
            tasks.Add(DeleteChatMessage(message, chat));
        }
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Delete a single chat message from the database
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public async Task DeleteChatMessage(ChatMessage message, WorkItemChat chat)
    {
        // Delete files
        List<Task> tasks = new List<Task>();
        foreach (ChatFile file in message.Files)
        {
            tasks.Add(_storageService.DeleteFile(chat, file.FileName));
        }
        await Task.WhenAll(tasks);

        var filter = Builders<BsonDocument>.Filter.Eq("_id", message.Id);
        await _chatMessageCollection.DeleteOneAsync(filter);
    }

    /// <summary>
    /// Delete all chat messages for a chat from the database
    /// </summary>
    /// <param name="chat"></param>
    /// <returns></returns>
    private async Task DeleteChat(WorkItemChat chat)
    {
        await _storageService.DeleteContainer(chat);

        var filter = Builders<BsonDocument>.Filter.Eq("_id", chat.Id);
        await _chatMessageCollection.DeleteManyAsync(filter);

    }
}
