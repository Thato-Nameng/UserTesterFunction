using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;

namespace UserTesterFunction
{
    public class OrderEntity : TableEntity
    {
        public OrderEntity(string orderId)
        {
            PartitionKey = "Orders";
            RowKey = orderId;
            Date = DateTime.UtcNow;
            OrderStatus = "Processing";
        }

        public OrderEntity() { }

        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerPhone { get; set; }
        public double TotalAmount { get; set; }
        public string OrderStatus { get; set; }
        public DateTime Date { get; set; }
        public string Products { get; set; } // JSON string representing list of products
    }
}
