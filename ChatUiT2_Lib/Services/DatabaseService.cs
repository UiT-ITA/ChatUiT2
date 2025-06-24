using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using ChatUiT2.Tools;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.Text;
using System.Text.Json;
using static ChatUiT2.Models.ChatFile;

namespace ChatUiT2.Services;

public class DatabaseService : IDatabaseService
{
    // Services
    private readonly IKeyVaultService _keyVaultService;
    private readonly IEncryptionService _encryptionService;
    private readonly IDateTimeProvider _dateTimeProvider;
    //private IStorageService _storageService;

    // Collections
    private readonly IMongoCollection<BsonDocument> _userCollection;
    private readonly IMongoCollection<BsonDocument> _chatCollection;
    private readonly IMongoCollection<BsonDocument> _chatMessageCollection;
    private readonly IMongoCollection<BsonDocument> _fileCollection;

    // Settings
    private readonly bool _useEncryption;

    public DatabaseService(IConfiguration configuration, 
                           IEncryptionService encryptionService, 
                           IKeyVaultService keyVaultService,
                           IDateTimeProvider dateTimeProvider)
    {
        _keyVaultService = keyVaultService;
        _encryptionService = encryptionService;
        this._dateTimeProvider = dateTimeProvider;
        //_storageService = storageService;

        var connectionString = configuration.GetConnectionString("MongoDb");

        var client = new MongoClient(connectionString);

        // Load database and collection names from configuration
        //var systemDatabase = client.GetDatabase(configuration["DBSettings:SystemDatabaseName"]);
        //var userDatabase = client.GetDatabase(configuration["DBSettings:UserDatabaseName"]);

        //_configCollection = systemDatabase.GetCollection<BsonDocument>(configuration["DBSettings:ConfigCollectionName"]);
        //_userCollection = userDatabase.GetCollection<BsonDocument>(configuration["DBSettings:UserCollectionName"]);
        //_workItemCollection = userDatabase.GetCollection<BsonDocument>(configuration["DBSettings:WorkItemCollectionName"]);
        //_chatMessageCollection = userDatabase.GetCollection<BsonDocument>(configuration["DBSettings:ChatMessageCollectionName"]);

        var userDatabase = client.GetDatabase("Users");

        _userCollection = userDatabase.GetCollection<BsonDocument>("Users");
        _chatCollection = userDatabase.GetCollection<BsonDocument>("Chats");
        _chatMessageCollection = userDatabase.GetCollection<BsonDocument>("ChatMessages");
        _fileCollection = userDatabase.GetCollection<BsonDocument>("Files");

        _useEncryption = configuration.GetValue<bool>("UseEncryption", defaultValue: true);
    
        //Console.WriteLine("DatabaseService created");
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
        await _chatCollection.DeleteManyAsync(filter);
        await _chatMessageCollection.DeleteManyAsync(filter);
    }

    /// <summary>
    /// Gets a list of usernames for for users that have at least one expired chat (not updated in 7 days)
    /// </summary>
    /// <returns></returns>
    public async Task<List<string>> GetUsersWithWorkItemsExpired()
    {
        var workItems = new List<IWorkItem>();

        DateTime olderThan = _dateTimeProvider.UtcNow.AddDays(-7);
        var filter = Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Lt("Updated", olderThan),
                                                       Builders<BsonDocument>.Filter.Ne("Permanent", true));

        var projection = Builders<BsonDocument>.Projection.Include("Username").Exclude("_id");

        var usernames = await _chatCollection
            .Find(filter)
            .Project(projection)
            .ToListAsync();

        var uniqueUsernames = new HashSet<string>();

        foreach (var doc in usernames)
        {
            uniqueUsernames.Add(doc["Username"].AsString);
        }
        return uniqueUsernames.ToList();
    }

    public async Task<List<IWorkItem>> GetUsersExpiredWorkItems(string username)
    {
        // Get user
        var filter = Builders<BsonDocument>.Filter.Eq("Username", username);
        var document = await _userCollection.Find(filter).FirstOrDefaultAsync();

        if (document != null)
        {
            User userObj = new User()
            {
                Username = document["Username"].AsString
            };
            var workItems = new List<IWorkItem>();
            DateTime cutoffDate = _dateTimeProvider.UtcNow.AddDays(-7);
            var chatFilter = Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("Username", username),
                                                               Builders<BsonDocument>.Filter.Ne("Permanent", true),
                                                               Builders<BsonDocument>.Filter.Lt("Updated", cutoffDate));
            var documents = await _chatCollection.FindAsync(chatFilter);
            foreach (var doc in documents.ToList())
            {
                if (doc["Type"] == WorkItemType.Chat.ToString())
                {
                    var workItem = JsonSerializer.Deserialize<WorkItemChat>(doc["Data"].AsString);
                    if(workItem != null)
                    {
                        workItems.Add(workItem);
                    }
                }
                else
                {
                    // Ignore unknown types
                }
            }
            return workItems;
        }
        else
        {
            throw new Exception("User not found");
        }
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
        var documents = await _chatCollection.Find(filter).ToListAsync();
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

        workItems.AddRange(results.Where(item => item != null).Select(item => item!));
        return workItems;
    }

    public async Task<List<IWorkItem>> GetWorkItemListLazy(User user, IUpdateService updateService)
    {
        var workItems = new List<IWorkItem>();
        var filter = Builders<BsonDocument>.Filter.Eq("Username", user.Username);
        var sort = Builders<BsonDocument>.Sort.Descending("Updated");
        var documents = await _chatCollection.Find(filter).ToListAsync();
        foreach (var doc in documents)
        {
            if (doc["Type"] == WorkItemType.Chat.ToString())
            {
                var workItem = JsonSerializer.Deserialize<WorkItemChat>(doc["Data"].AsString);
                if (workItem != null)
                {
                    workItem.State = WorkItemState.Unloaded;
                    workItems.Add(workItem);
                    // Start loading messages in the background
                    //_ = LoadWorkItemComponentsAsync(user, workItem, updateService);
                }
            }
            else
            {
                throw new Exception("Unknown work item type");
            }
        }
        return workItems;
    }

    public async Task LoadWorkItemComponentsAsync(User user, WorkItemChat workItem, IUpdateService updateService)
    {
        Console.WriteLine("Loading chat: " + workItem.Name);
        try
        {
            workItem.Messages = await GetChatMessages(user, workItem.Id);
        }
        catch (Exception ex)
        {
            await DeleteWorkItem(user, new WorkItemChat { Id = workItem.Id });
            Console.WriteLine("Error loading chat: " + ex.Message);
        }
        finally
        {
            workItem.State = WorkItemState.Ready;
            updateService.Update(UpdateType.All);
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

        await _chatCollection.ReplaceOneAsync(filter, document, options);
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
            await DeleteChat((WorkItemChat)workItem, user);
        }

        var filter = Builders<BsonDocument>.Filter.Eq("_id", workItem.Id);
        try
        {
            await _chatCollection.DeleteOneAsync(filter);
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

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("ChatId", chatId),
            Builders<BsonDocument>.Filter.Eq("Username", user.Username) // Add partition key
        );

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
                content = _encryptionService.DecryptString(doc["Content"].AsByteArray, user.AesKey);
            }
            else
            {
                content = doc["Content"].AsString;
            }

            // Get files
            var files = new List<ChatFile>();
            if (doc.Contains("Files"))
            {
                foreach (var fileId in doc["Files"].AsBsonArray)
                {
                    files.Add(await GetChatFile(fileId.AsString, doc["_id"].AsString, user));
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

    private async Task<ChatFile> GetChatFile(string id, string messageId, User user)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Filter.Eq("MessageId", messageId)
        );
        var fileDocument = await _fileCollection.Find(filter).FirstOrDefaultAsync();
        if (fileDocument == null)
        {
            throw new Exception("File not found");
        }
        // Extract file details
        string fileName = fileDocument["FileName"].AsString;
        var partsArray = fileDocument["Parts"].AsBsonArray;
        // Reconstruct the file parts
        var parts = new List<ChatFilePart>();
        foreach (var partDoc in partsArray)
        {
            FilePartType type = (FilePartType)partDoc["Type"].AsInt32;
            ChatFilePart part;
            if (type == FilePartType.Text)
            {
                string text;
                if (_useEncryption)
                {
                    text = _encryptionService.DecryptString(partDoc["Data"].AsByteArray, user.AesKey!);
                }
                else
                {
                    text = partDoc["Data"].AsString;
                }
                part = new TextFilePart(text);
            }
            else if (type == FilePartType.Image)
            {
                byte[] imagebytes;
                if (_useEncryption)
                {
                    imagebytes = _encryptionService.Decrypt(partDoc["Data"].AsByteArray, user.AesKey!);
                }
                else
                {
                    imagebytes = partDoc["Data"].AsByteArray;
                }
                part = new ImageFilePart(imagebytes);
            }
            else
            {
                throw new Exception("Unknown file part type");
            }
            parts.Add(part);
        }
        // Create and return the ChatFile object
        return new ChatFile(id, fileName, parts);
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
            tasks.Add(SaveChatFile(file, message, chat, user));
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
            {"Files", new BsonArray(message.Files.Select(f => f.Id)) }
        };

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", message.Id),
            Builders<BsonDocument>.Filter.Eq("ChatId", chat.Id), // Match the chat ID
            Builders<BsonDocument>.Filter.Eq("Username", user.Username) // Add partition key
        );

        var options = new ReplaceOptions { IsUpsert = true };

        await _chatMessageCollection.ReplaceOneAsync(filter, document, options);
    }

    /// <summary>
    /// Save a single chat file to the database
    /// <param name="file"></param>
    /// <param name="message"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    private async Task SaveChatFile(ChatFile file, ChatMessage message, WorkItemChat chat, User user)
    {
        var existingFile = await _fileCollection.Find(Builders<BsonDocument>.Filter.Eq("_id", file.Id)).FirstOrDefaultAsync();
        if (existingFile != null)
        {
            Console.WriteLine($"File with ID {file.Id} already exists. Skipping insert.");
            return;
        }
        var filePartArray = new BsonArray();
        foreach (var part in file.Parts)
        {
            BsonDocument partDocument;
            if (part is ImageFilePart imgPart)
            {
                partDocument = new BsonDocument
                {
                    {"Type", (int)part.Type},
                    {"Data", _useEncryption ? _encryptionService.Encrypt(imgPart.Data, user.AesKey!) : imgPart.Data}
                };
            }
            else if (part is TextFilePart txtPart)
            {
                partDocument = new BsonDocument
                {
                    {"Type", (int)part.Type},
                    {"Data", _useEncryption ? _encryptionService.Encrypt(txtPart.Data, user.AesKey!) : txtPart.Data},
                };
            }
            else
            {
                throw new Exception("Unknown file part type");
            }
            filePartArray.Add(partDocument);
        }
        var fileDocument = new BsonDocument
            {
                {"_id", file.Id},
                {"MessageId", message.Id},
                {"ChatId", chat.Id},
                {"Username", user.Username },
                {"FileName", file.FileName},
                {"Parts", filePartArray}
            };
        await _fileCollection.InsertOneAsync(fileDocument);
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
            tasks.Add(DeleteChatMessage(message, chat, user));
        }
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Delete a single chat message from the database
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public async Task DeleteChatMessage(ChatMessage message, WorkItemChat chat, User user)
    {
        // Delete files
        List<Task> tasks = new List<Task>();
        foreach (ChatFile file in message.Files)
        {
            //tasks.Add(_storageService.DeleteFile(chat, file.FileName));
            tasks.Add(DeleteChatFile(file, user));
        }
        await Task.WhenAll(tasks);

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", message.Id),
            Builders<BsonDocument>.Filter.Eq("Username", user.Username) // Add partition key
        );
        await _chatMessageCollection.DeleteOneAsync(filter);
    }

    public async Task DeleteChatFile(ChatFile file, User user)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", file.Id),
            Builders<BsonDocument>.Filter.Eq("Username", user.Username) // Add partition key
        );
        await _fileCollection.DeleteOneAsync(filter);
    }

    /// <summary>
    /// Delete all chat messages for a chat from the database
    /// </summary>
    /// <param name="chat"></param>
    /// <returns></returns>
    private async Task DeleteChat(WorkItemChat chat, User user)
    {
        var msgFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("ChatId", chat.Id),
            Builders<BsonDocument>.Filter.Eq("Username", user.Username) // partition key
        );

        var messages = await _chatMessageCollection.Find(msgFilter).ToListAsync();
        var msgIds = messages.Select(msg => msg["_id"]).ToList();

        var fileFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.In("MessageId", msgIds),
            Builders<BsonDocument>.Filter.Eq("Username", user.Username) // partition key
        );

        var results = await _chatMessageCollection.DeleteManyAsync(msgFilter);
        results = await _fileCollection.DeleteManyAsync(fileFilter);

    }


    public async Task<List<User>> GetUsers()
    {
        List<User> users = new List<User>();

        // Get all user objects in the user database
        var documents = await _userCollection.Find(new BsonDocument()).ToListAsync();
        foreach (var document in documents) {
            var user = new User
            {
                Username = document["Username"].AsString,
                Preferences = BsonSerializer.Deserialize<Preferences>(document["Preferences"].AsBsonDocument)

            };
            users.Add(user);
        }
        return users;
    }
}
