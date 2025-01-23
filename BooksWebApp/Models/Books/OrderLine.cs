using System;
using System.Collections.Generic;

namespace BooksWebApp.Models.Books;

public partial class OrderLine
{
    public int LineId { get; set; }

    public int? OrderId { get; set; }

    public int? BookId { get; set; }

    public double? Price { get; set; }

    public virtual Book? Book { get; set; }

    public virtual CustOrder? Order { get; set; }
}
