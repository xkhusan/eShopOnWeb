using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace OrderItemsReserver
{
    public class OrderItemsReserver
    {
        private readonly ILogger<OrderItemsReserver> _logger;
        private readonly BlobContainerClient _container;
        private readonly string? _logicAppUrl;

        public OrderItemsReserver(ILogger<OrderItemsReserver> logger, IConfiguration config)
        {
            _logger = logger;

            var conn = config["AzureWebJobsStorage"];
            var containerName = config["OrdersContainer"] ?? "orders";
            _container = new BlobContainerClient(conn, containerName);
            _container.CreateIfNotExists();

            _logicAppUrl = Environment.GetEnvironmentVariable("LOGIC_APP_URL");
        }

        [Function(nameof(OrderItemsReserver))]
        public async Task Run(
            [ServiceBusTrigger("orderitemreserverbus", Connection = "ServiceBusConnection")]
            string message)
        {
            _logger.LogInformation("Function triggered. Message length: {Length}", message?.Length ?? 0);

            try
            {
                var blobName = $"order-{DateTime.UtcNow:yyyyMMddHHmmssfff}.json";
                var blob = _container.GetBlobClient(blobName);
                _logger.LogInformation("Uploading blob {BlobName}", blobName);

                for (var i = 0; i < 3; i++)
                {
                    _logger.LogInformation("Upload attempt {Attempt} for blob {BlobName}", i + 1, blobName);
                    using var ms = new MemoryStream(Encoding.UTF8.GetBytes(message));
                    Azure.Response<BlobContentInfo>? result = await blob.UploadAsync(ms, overwrite: false);

                    if (result.GetRawResponse().Status == 201)
                    {
                        _logger.LogInformation("Blob uploaded successfully: {BlobName}", blobName);
                        return;
                    }
                }
                
                _logger.LogError("Blob upload failed after retries for blob {BlobName}", blobName);
                throw new Exception("Blob upload failed after retries.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during processing. Sending message to Logic App.");
                using var client = new HttpClient();
                await client.PostAsync(
                    _logicAppUrl,
                    new StringContent(message, Encoding.UTF8, "application/json"));
            }
        }
    }
}
