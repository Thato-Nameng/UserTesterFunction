using Microsoft.Azure.Cosmos.Table;
using System;

namespace UserTesterFunction
{
    public class UserEntity : TableEntity
    {
        public UserEntity(string email)
        {
            PartitionKey = "CustomerProfile";
            RowKey = email;
            CreatedDate = DateTime.UtcNow;
        }

        public UserEntity() { }

        public string Name { get; set; }
        public string Surname { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string HashedPassword { get; set; }
        public string Role { get; set; } = "Customer";
        public string ImageUrl { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}