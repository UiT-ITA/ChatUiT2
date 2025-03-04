using ChatUiT2.Interfaces;
using ChatUiT2.Models.RabbitMq;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace ChatUiT2.Services;

public class RabbitMqService : IRabbitMqService
{
    private readonly ILogger<RabbitMqService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConnectionFactory _factory;

    public RabbitMqService(ILogger<RabbitMqService> logger,
                           IConfiguration configuration)
    {
        this._logger = logger;
        this._configuration = configuration;

        _factory = new ConnectionFactory();
        _factory.Ssl.Enabled = true;
        _factory.UserName = _configuration["RabbitMq:Username"];
        _factory.Password = _configuration["RabbitMq:Password"];
        _factory.VirtualHost = _configuration["RabbitMq:VirtualHost"];
        _factory.HostName = _configuration["RabbitMq:HostName"];
        _factory.Port = _configuration.GetValue<int>("RabbitMq:Port");
        _factory.Ssl.ServerName = _configuration["RabbitMq:HostName"];        
    }
    public async Task SendRagMessage(RagMqMessage message)
    {
        if (message == null)
        {
            throw new ArgumentException("Message can not be null", "message");
        }
        await SendRagMessages(new List<RagMqMessage> { message });
    }

    public async Task SendRagMessages(IEnumerable<RagMqMessage> messages)
    {
        using (var connection = await _factory.CreateConnectionAsync())
        using (var channel = await connection.CreateChannelAsync())
        {
            int batchSize = messages.Count() > 100 ? 100 : messages.Count();
            var tasks = new List<Task>();
            foreach (var message in messages)
            {
                string jsonString = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(jsonString);
                var ex = _configuration["RabbitMq:ExchangeName"];
                BasicProperties basicProperties = new();
                tasks.Add(channel.BasicPublishAsync<BasicProperties>(exchange: _configuration["RabbitMq:ExchangeName"],
                                                                     routingKey: GetRoutingKey(message),
                                                                     mandatory: false,
                                                                     basicProperties: basicProperties,
                                                                     body: body).AsTask());
                if(tasks.Count() == batchSize)
                {
                    await Task.WhenAll(tasks);
                    tasks.Clear();
                }
            }
            if (tasks.Count() > 0)
            {
                await Task.WhenAll(tasks);
                tasks.Clear();
            }
        }
    }

    public async Task<uint> GetQueueCount(string queueName)
    {
        if (string.IsNullOrEmpty(queueName) == true)
        {
            throw new ArgumentException("Parameter can not be null", "queueName");
        }
        using (var connection = await _factory.CreateConnectionAsync())
        using (var channel = await connection.CreateChannelAsync())
        {
            var queueDeclare = await channel.QueueDeclarePassiveAsync(queueName);
            return queueDeclare.MessageCount;
        }
    }

    public async Task<IEnumerable<T>> GetAllMessagesInQueueWithoutRemoval<T>(string queueName)
    {
        List<T> messages = new();
        using (var connection = await _factory.CreateConnectionAsync())
        using (var channel = await connection.CreateChannelAsync())
        {
            while (true)
            {                
                var result = await channel.BasicGetAsync(queueName, false);
                if(result == null)
                {
                    // No more messages
                    break;
                }
                var body = result.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($"Received message: {message}");
                var msgObj = JsonSerializer.Deserialize<T>(message);
                if (msgObj != null)
                {
                    messages.Add(msgObj);
                }
                else
                {
                    // Should this be exception or just ignore?
                    throw new Exception("Failed to deserialize message, null was returned from JsonSerializer");
                }
            }
        }
        return messages;
    }

    public string GetRoutingKey(RagMqMessage message)
    {
        string opName = Enum.GetName(typeof(RagMqMessageOperations), message.Operation) ?? string.Empty;
        string baseRoutingKey = _configuration["RabbitMq:BaseRoutingKey"];
        if(string.IsNullOrEmpty(baseRoutingKey))
        {
            throw new ArgumentException("Missing operation in message");
        }
        if (string.IsNullOrEmpty(baseRoutingKey))
        {
            throw new ArgumentException("Base routing key not found in configuration");
        }
        switch (message.Operation)
        {
            case RagMqMessageOperations.GenerateEmbeddings:
                return $"{baseRoutingKey}.{opName}";
            case RagMqMessageOperations.ScanForItemsMissingEmbeddings:
                return $"{baseRoutingKey}.{opName}";
            case RagMqMessageOperations.CancelAllEmbeddingsProcessing:
                return $"{baseRoutingKey}.{opName}";
            default:
                throw new ArgumentException($"Unknown operation: {message.Operation}");
        }
    }
}
