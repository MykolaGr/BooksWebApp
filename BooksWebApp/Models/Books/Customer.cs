using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BooksWebApp.Models.Books
{
    public partial class Customer
    {
  
        public int CustomerId { get; set; }
        public string FirstName { get; set; } 
        public string LastName { get; set; } 
        public string Email { get; set; }
        public virtual ICollection<CustOrder> CustOrders { get; set; } = new List<CustOrder>();
        public virtual ICollection<CustomerAddress> CustomerAddresses { get; set; } = new List<CustomerAddress>();
    }

}