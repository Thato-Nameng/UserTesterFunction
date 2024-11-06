using Azure.Storage.Queues;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UserTesterFunction
{
    public static class OrderFunctions
    {
        // Function to place an order, save to OrdersTable, and enqueue to ordersqueue
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
            await queueClient.SendMessageAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(messageContent)));

            return new OkObjectResult($"Order placed successfully with Order ID {orderId}.");
        }

        // Function to retrieve all orders from OrdersTable
        [FunctionName("GetAllOrdersFromTable")]
        public static async Task<IActionResult> GetAllOrdersFromTable(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orders/table")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Retrieving all orders from OrdersTable.");

            // Connect to Azure Table Storage
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());
            CloudTable table = tableClient.GetTableReference("OrdersTable");

            // Query all orders from the OrdersTable
            var query = new TableQuery<OrderEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "Orders"));
            var orders = new List<OrderEntity>();

            TableContinuationToken token = null;
            do
            {
                var queryResult = await table.ExecuteQuerySegmentedAsync(query, token);
                orders.AddRange(queryResult.Results);
                token = queryResult.ContinuationToken;
            } while (token != null);

            return new OkObjectResult(orders);
        }

        // Function to retrieve all messages from ordersqueue
        [FunctionName("GetAllOrdersFromQueue")]
        public static async Task<IActionResult> GetAllOrdersFromQueue(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orders/queue")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Retrieving all orders from ordersqueue.");

            // Initialize QueueClient
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            QueueClient queueClient = new QueueClient(connectionString, "ordersqueue");

            // Ensure the queue exists
            if (!await queueClient.ExistsAsync())
            {
                return new NotFoundObjectResult("Queue 'ordersqueue' not found.");
            }

            // Fetch messages from the queue
            var messages = await queueClient.ReceiveMessagesAsync(maxMessages: 32); // Adjust maxMessages as needed
            var orderMessages = new List<string>();

            foreach (var message in messages.Value)
            {
                // Decode the message from base64
                string decodedMessage = Encoding.UTF8.GetString(Convert.FromBase64String(message.MessageText));
                orderMessages.Add(decodedMessage);
            }

            return new OkObjectResult(orderMessages);
        }
    }
}
