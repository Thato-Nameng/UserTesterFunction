using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos.Table;
using Azure.Storage.Queues;
using System.Collections.Generic;

namespace UserTesterFunction
{
    public static class Function3
    {
        [FunctionName("PlaceOrder")]
        public static async Task<IActionResult> PlaceOrder(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "placeOrder")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Processing order placement request.");

            // Parse request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string customerName = data?.customerName;
            string customerEmail = data?.customerEmail;
            string customerPhone = data?.customerPhone;
            double totalAmount = data?.totalAmount;
            var products = data?.products?.ToObject<List<OrderProduct>>();

            if (string.IsNullOrEmpty(customerName) || string.IsNullOrEmpty(customerEmail) || string.IsNullOrEmpty(customerPhone) || products == null || products.Count == 0)
            {
                return new BadRequestObjectResult("Please provide customer details and at least one product.");
            }

            // Generate a unique Order ID
            string orderId = Guid.NewGuid().ToString();

            // Create order entity
            var orderEntity = new OrderEntity(orderId)
            {
                CustomerName = customerName,
                CustomerEmail = customerEmail,
                CustomerPhone = customerPhone,
                TotalAmount = totalAmount,
                Products = JsonConvert.SerializeObject(products), // Store products as JSON string
                Date = DateTime.UtcNow,
                OrderStatus = "Processing"
            };

            // Save order to Azure Table Storage
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());
            CloudTable table = tableClient.GetTableReference("OrdersTable");

            await table.CreateIfNotExistsAsync();
            TableOperation insertOperation = TableOperation.Insert(orderEntity);
            await table.ExecuteAsync(insertOperation);

            // Prepare the queue message with all required details
            var queueMessage = new
            {
                OrderId = orderId,
                CustomerName = customerName,
                CustomerPhone = customerPhone,
                CustomerEmail = customerEmail,
                Products = products,
                TotalAmount = totalAmount,
                Date = orderEntity.Date,
                OrderStatus = orderEntity.OrderStatus
            };
            string messageContent = JsonConvert.SerializeObject(queueMessage);

            // Add message to the ordersqueue
            QueueClient queueClient = new QueueClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"), "ordersqueue");
            await queueClient.CreateIfNotExistsAsync(); // Ensure the queue exists
            await queueClient.SendMessageAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(messageContent)));

            return new OkObjectResult($"Order placed successfully with Order ID {orderId}.");
        }
    }
}
