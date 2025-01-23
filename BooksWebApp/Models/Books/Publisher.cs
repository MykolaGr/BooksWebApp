using System;
using System.Collections.Generic;

namespace BooksWebApp.Models.Books;

public partial class Publisher
{
    public int PublisherId { get; set; }

    public string? PublisherName { get; set; }

    public virtual ICollection<Book> Books { get; set; } = new List<Book>();
}
