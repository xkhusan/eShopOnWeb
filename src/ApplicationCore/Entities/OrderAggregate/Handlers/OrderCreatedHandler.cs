using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate.Events;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate.Handlers;

public class OrderCreatedHandler(ILogger<OrderCreatedHandler> logger, IEmailSender emailSender, HttpClient httpClient) : INotificationHandler<OrderCreatedEvent>
{
    public async Task Handle(OrderCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation("Order #{orderId} placed: ", domainEvent.Order.Id);

        await emailSender.SendEmailAsync("to@test.com",
                                         "Order Created",
                                         $"Order with id {domainEvent.Order.Id} was created.");
        
        var orderRecord = new
        {
            OrderId = domainEvent.Order.Id,
            OrderDate = domainEvent.Order.OrderDate.ToString("dd/MM/yyyy HH:mm:ss"),
            ShippingAddress = domainEvent.Order.ShipToAddress,
            ListOfItems = domainEvent.Order.OrderItems,
            QuantityOfItems = domainEvent.Order.OrderItems.Select(i => new {
                ItemId = i.ItemOrdered.CatalogItemId.ToString(),
                Quantity = i.Units
            }),
            FinalPrice = domainEvent.Order.Total()
        };

        try
        {
            // Send HTTP POST request to the Azure Function.
            var response = await httpClient.PostAsJsonAsync("https://eshop-delivery-order-processor-function-app.azurewebsites.net/api/DeliveryOrderProcessor",
                orderRecord, cancellationToken);
            response.EnsureSuccessStatusCode();

            logger.LogInformation("Order details successfully sent to DeliveryOrderProcessor.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send order details to DeliveryOrderProcessor.");
        }

        var json = JsonSerializer.Serialize(orderRecord);

        try
        {
            var serviceBusConnectionString = Environment.GetEnvironmentVariable("SERVICE_BUS_CONNECTION_STRING");
            if (string.IsNullOrEmpty(serviceBusConnectionString))
            {
                throw new InvalidOperationException("Service Bus connection string is not set in environment variables.");
            }

            var client = new ServiceBusClient(serviceBusConnectionString);
            var sender = client.CreateSender("orderitemreserverbus");

            await sender.SendMessageAsync(new ServiceBusMessage(json), cancellationToken);
            logger.LogInformation("Order details sent to Service Bus.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send order details to Service Bus.");
        }
    }
}
