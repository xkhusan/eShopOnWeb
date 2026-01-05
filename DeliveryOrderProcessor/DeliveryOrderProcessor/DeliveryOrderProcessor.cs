using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace DeliveryOrderProcessor
{
    public class DeliveryOrderProcessor
    {
        private readonly ILogger<DeliveryOrderProcessor> _logger;

        public DeliveryOrderProcessor(ILogger<DeliveryOrderProcessor> logger, IConfiguration config)
        {
            _logger = logger;
        }

        [Function(nameof(DeliveryOrderProcessor))]
        [CosmosDBOutput(databaseName: "OrdersDB", containerName: "Orders", Connection = "CosmosDBConnection",
            CreateIfNotExists = true, PartitionKey = "/xkhusan")]
        public async Task<object> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
            HttpRequest request,
            FunctionContext context)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            using StreamReader reader = new(request.Body);
            var requestBody = await reader.ReadToEndAsync();

            var order = JsonSerializer.Deserialize<JsonElement>(requestBody);
            _logger.LogInformation("Reserving order items: {order}", order);
            var guid = Guid.NewGuid().ToString();
            return new
            {
                id = guid,
                xkhusan = $"part-key-{guid}",
                order
            };
        }
    }
}