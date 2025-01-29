using ChatUiT2.Interfaces;
using ChatUiT2_Classlib.Model.RabbitMq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using UiT.ChatUiT2.MaintenanceFunctions.Model;

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
        _factory.Ssl.Version = SslProtocols.Tls12;
        _factory.Ssl.ServerName = _configuration["RabbitMq:HostName"];
    }
    public void SendRagMessage(RagMqMessage message)
    {
        if (message == null)
        {
            throw new ArgumentException("Message can not be null", "message");
        }
        using (var connection = _factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            string jsonString = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(jsonString);
            var ex = _configuration["RabbitMq:ExchangeName"];
            channel.BasicPublish(exchange: _configuration["RabbitMq:ExchangeName"],
                                 routingKey: GetRoutingKey(message),
                                 basicProperties: null,
                                 body: body);
        }
    }

    public string GetRoutingKey(RagMqMessage message)
    {
        switch (message.Operation)
        {
            case RagMqMessageOperations.GenerateQuestionEmbeddings:
                string operationName = Enum.GetName(typeof(RagMqMessageOperations), message.Operation) ?? "Unknown";
                //return $"{_configuration["RabbitMq:BaseRoutingKey"]}.{operationName}";
                return "#";
            default:
                return $"#";
        }
    }
}
