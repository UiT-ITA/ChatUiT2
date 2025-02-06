using ChatUiT2.Interfaces;
using ChatUiT2_Classlib.Model.RabbitMq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Security.Authentication;
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
        using (var connection = await _factory.CreateConnectionAsync())
        using (var channel = await connection.CreateChannelAsync())
        {
            string jsonString = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(jsonString);
            var ex = _configuration["RabbitMq:ExchangeName"];
            BasicProperties basicProperties = new();
            await channel.BasicPublishAsync<BasicProperties>(exchange: _configuration["RabbitMq:ExchangeName"],
                                                             routingKey: GetRoutingKey(message),
                                                             mandatory: false,
                                                             basicProperties: basicProperties,
                                                             body: body);
        }
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
