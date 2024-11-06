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
using System.Collections.Generic;
using UserTesterFunction;

namespace FunctionApp
{
    public static class Function2
    {
        [FunctionName("RegisterProduct")]
        public static async Task<IActionResult> RegisterProduct(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "registerProduct")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Processing product registration request.");

            // Parse request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string productName = data?.productName;
            string priceStr = data?.price;
            string quantityStr = data?.quantity;
            string imageUrl = data?.imageUrl;

            if (string.IsNullOrEmpty(productName) || string.IsNullOrEmpty(priceStr) || string.IsNullOrEmpty(quantityStr))
            {
                return new BadRequestObjectResult("Please provide productName, price, and quantity.");
            }

            // Parse and validate price and quantity
            if (!double.TryParse(priceStr.ToString(), out double price) || !int.TryParse(quantityStr.ToString(), out int quantity))
            {
                return new BadRequestObjectResult("Price must be a number, and quantity must be an integer.");
            }

            // Generate a unique Product ID
            string productId = Guid.NewGuid().ToString();

            // Create product entity
            var productEntity = new ProductEntity(productId)
            {
                ProductId = productId,
                ProductName = productName,
                Price = price,
                Quantity = quantity,
                ImageUrl = imageUrl,
                CreatedDate = DateTime.UtcNow
            };

            // Save to Azure Table Storage
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());
            CloudTable table = tableClient.GetTableReference("ProductsTable");

            await table.CreateIfNotExistsAsync();
            TableOperation insertOperation = TableOperation.Insert(productEntity);
            await table.ExecuteAsync(insertOperation);

            return new OkObjectResult($"Product {productName} registered successfully.");
        }

        [FunctionName("GetAllProducts")]
        public static async Task<IActionResult> GetAllProducts(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "products")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Retrieving all products.");

            // Connect to Azure Table Storage
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());
            CloudTable table = tableClient.GetTableReference("ProductsTable");

            // Query all products from the ProductsTable
            var query = new TableQuery<ProductEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "Products"));
            var products = new List<ProductEntity>();

            TableContinuationToken token = null;
            do
            {
                var queryResult = await table.ExecuteQuerySegmentedAsync(query, token);
                products.AddRange(queryResult.Results);
                token = queryResult.ContinuationToken;
            } while (token != null);

            return new OkObjectResult(products);
        }
    }
}
