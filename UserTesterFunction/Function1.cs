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
using Azure.Storage.Blobs;
using System.Collections.Generic;

namespace UserTesterFunction
{
    public static class Function1
    {
        [FunctionName("RegisterUser")]
        public static async Task<IActionResult> RegisterUser(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "register")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Processing user registration request.");

            // Parse request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string name = data?.name;
            string surname = data?.surname;
            string email = data?.email;
            string password = data?.password;
            string phoneNumber = data?.phoneNumber;
            string role = data?.role ?? "Customer"; // Default to "Customer" if not provided
            string imageUrl = data?.imageUrl;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(surname) ||
                string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) ||
                string.IsNullOrEmpty(phoneNumber))
            {
                return new BadRequestObjectResult("Please provide name, surname, email, password, and phoneNumber.");
            }

            // Hash the password
            string hashedPassword = PasswordHasher.HashPassword(password);

            // Create user entity
            var userEntity = new UserEntity(email)
            {
                Name = name,
                Surname = surname,
                Email = email,
                PhoneNumber = phoneNumber,
                HashedPassword = hashedPassword,
                Role = role,
                ImageUrl = imageUrl,
                CreatedDate = DateTime.UtcNow // Automatically set created date
            };

            // Save to Azure Table Storage
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());
            CloudTable table = tableClient.GetTableReference("UserTable");

            await table.CreateIfNotExistsAsync();
            TableOperation insertOperation = TableOperation.Insert(userEntity);
            await table.ExecuteAsync(insertOperation);

            return new OkObjectResult($"User {name} {surname} registered successfully.");
        }

        [FunctionName("GetAllUsers")]
        public static async Task<IActionResult> GetAllUsers(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "users")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Retrieving all users.");

            // Connect to Azure Table Storage
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());
            CloudTable table = tableClient.GetTableReference("UserTable");

            // Query all users from the UserTable
            var query = new TableQuery<UserEntity>();
            var users = new List<UserEntity>();

            TableContinuationToken token = null;
            do
            {
                var queryResult = await table.ExecuteQuerySegmentedAsync(query, token);
                users.AddRange(queryResult.Results);
                token = queryResult.ContinuationToken;
            } while (token != null);

            return new OkObjectResult(users);
        }
    }
}





/*f3
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
                Date = DateTime.UtcNow
            };

            // Save order to Azure Table Storage
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());
            CloudTable table = tableClient.GetTableReference("OrdersTable");

            await table.CreateIfNotExistsAsync();
            TableOperation insertOperation = TableOperation.Insert(orderEntity);
            await table.ExecuteAsync(insertOperation);

            return new OkObjectResult($"Order placed successfully with Order ID {orderId}.");
        }

        [FunctionName("GetAllOrders")]
        public static async Task<IActionResult> GetAllOrders(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orders")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Retrieving all orders.");

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
    }
}


f4.2

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
                Date = DateTime.UtcNow
            };

            // Save order to Azure Table Storage
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());
            CloudTable table = tableClient.GetTableReference("OrdersTable");

            await table.CreateIfNotExistsAsync();
            TableOperation insertOperation = TableOperation.Insert(orderEntity);
            await table.ExecuteAsync(insertOperation);

            return new OkObjectResult($"Order placed successfully with Order ID {orderId}.");
        }

        [FunctionName("GetAllOrders")]
        public static async Task<IActionResult> GetAllOrders(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orders")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Retrieving all orders.");

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
    }
}
 
 
 */