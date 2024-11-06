using Microsoft.Azure.Cosmos.Table;
using System;

namespace UserTesterFunction
{
    public class ProductEntity : TableEntity
    {
        public ProductEntity(string productId)
        {
            PartitionKey = "Products";
            RowKey = productId;
            CreatedDate = DateTime.UtcNow;
        }

        public ProductEntity() { }

        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
