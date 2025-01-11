using System;
using System.Linq;
using BookStore;
using Newtonsoft.Json.Linq;

class Program
{
    private static BookContext db;
    static async Task Main(string[] args)
    {
        db = new BookContext();
        db.Database.EnsureCreated();
        

        if (!db.Books.Any())
            await TakeBooksFromInternet();
        else Console.WriteLine("Have data");

        if (!db.Books.Any())
        {
            db.Books.AddRange(
                new Book { Author = "George Orwell", Title = "Animal Farm", Year = 1945, Count = 999999 },
                new Book { Author = "George Orwell", Title = "1984", Year = 1948, Count = 999999 }
            );
            db.SaveChanges();
            Console.WriteLine("Books saved to database offline for testing");
        }
        
        EnterCommand();
    }
    

    static async Task TakeBooksFromInternet(int take = 50)
    {
        string url = $"https://openlibrary.org/search.json?q=best+sellers&limit={take}&offset=0";
        HttpClient client = new HttpClient();
        Console.WriteLine("Try to get books for base online. Please wait ...");
        try
        {
            HttpResponseMessage response = await client.GetAsync(url);
        
            string responseBody = await response.Content.ReadAsStringAsync();
            JObject json = JObject.Parse(responseBody);
        
            if (json["docs"] != null)
            {
                foreach (var item in json["docs"])
                {
                    string title = item["title"]?.ToString();
                    string author = item["author_name"]?[0]?.ToString();
                    int year = item["first_publish_year"] != null ? int.Parse(item["first_publish_year"].ToString()) : 0;

                    if (title != null && author != null)
                    {
                        db.Books.Add(new Book { Title = title, Author = author, Year = year, Count = 1 });
                    }
                }

                db.SaveChanges();
                Console.WriteLine("Books saved to database online");
            }
            else
            {
                Console.WriteLine("No results");
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Request error: {e.Message}");
        }
    }



    static void EnterCommand()
    {
        Console.WriteLine("Enter command:");
        string? command = Console.ReadLine();
        var commands = command?.Split(' ');

        if (command.Length > 0)
        {
            switch (commands.First())
            {
                case "get":
                    GetBooks(commands.Skip(1).ToArray());
                    break;
                case "buy":
                    BuyBook(commands.Skip(1).ToArray());
                    break;
                case "restock":
                    RestockBooks(commands.Skip(1).ToArray());
                    break;
                default:
                    Console.WriteLine("Invalid command");
                    break;
            }
        }
        else
        {
            Console.WriteLine("you didnt enter a command");
            EnterCommand();
        }
    }

    static void GetBooks(string[] commands)
    {
        Console.WriteLine("Getting books...");

        var query = db.Books.AsQueryable();
        string title = commands.FirstOrDefault(a => a.StartsWith("--title="))?.Split('=')[1];
        string author = commands.FirstOrDefault(a => a.StartsWith("--author="))?.Split('=')[1];
        string date = commands.FirstOrDefault(a => a.StartsWith("--date="))?.Split('=')[1];
        string orderBy = commands.FirstOrDefault(a => a.StartsWith("--order-by="))?.Split('=')[1];
        if (!string.IsNullOrEmpty(title)) query = query.Where(b => b.Title.Contains(title));
        if (!string.IsNullOrEmpty(author)) query = query.Where(b => b.Author.Contains(author));
        if (!string.IsNullOrEmpty(date))
        {
            if (DateTime.TryParse(date, out DateTime parsedDate)) query = query.Where(b => b.Year == parsedDate.Year);
            else
            {
                Console.WriteLine("Wrong format");
                return;
            }
        }

        if (!string.IsNullOrEmpty(orderBy))
        {
            switch (orderBy.ToLower())
            {
                case "title":
                    query = query.OrderBy(b => b.Title);
                    break;
                case "author":
                    query = query.OrderBy(b => b.Author);
                    break;
                case "date":
                    query = query.OrderBy(b => b.Year);
                    break;
                case "count":
                    query = query.OrderBy(b => b.Count);
                    break;
                default:
                    Console.WriteLine("Invalid order-by field.");
                    return;
            }
        }

        foreach (var book in query.ToList())
        {
            Console.WriteLine(
                $"Id: {book.Id}, Author: {book.Author}, Title: {book.Title}, Year: {book.Year}, Count: {book.Count}");
        }
        EnterCommand();
    }

    static void BuyBook(string[] commands)
    {
        Console.WriteLine("Buying book...");
        int id;
        if (int.TryParse(commands.FirstOrDefault(a => a.StartsWith("--id="))?.Split('=')[1], out id))
        {
            var book = db.Books.Find(id);
            if (book != null && book.Count > 0)
            {
                book.Count--;
                db.SaveChanges();
                Console.WriteLine($"Bought {book.Title}. Remaining count: {book.Count}");
            }
            else
            {
                Console.WriteLine("Book not found or out of stock.");
            }
        }
        else
        {
            Console.WriteLine("Invalid id.");
        }
        EnterCommand();
    }

    static void RestockBooks(string[] commands)
    {
        Console.WriteLine("Restocking books...");
        int id = 0;
        int count = new Random().Next(1, 10);
        if (commands.Any(a => a.StartsWith("--id=")))
        {
            if (!int.TryParse(commands.FirstOrDefault(a => a.StartsWith("--id="))?.Split('=')[1], out id))
            {
                Console.WriteLine("Invalid id.");
                return;
            }
        }

        if (commands.Any(a => a.StartsWith("--count=")))
        {
            if (!int.TryParse(commands.FirstOrDefault(a => a.StartsWith("--count="))?.Split('=')[1], out count))
            {
                Console.WriteLine("Invalid count.");
                return;
            }
        }

        if (id > 0)
        {
            var book = db.Books.Find(id);
            if (book != null)
            {
                book.Count += count;
                db.SaveChanges();
                Console.WriteLine($"Restocked {count} copies of {book.Title}. New count: {book.Count}");
            }
            else
            {
                Console.WriteLine("Book not found.");
            }
        }
        else
        {
            var books = db.Books.ToList();
            if (books.Any())
            {
                var randomBook = books[new Random().Next(books.Count)];
                randomBook.Count += count;
                db.SaveChanges();
                Console.WriteLine($"Restocked {count} copies of {randomBook.Title}. New count: {randomBook.Count}");
            }
            else
            {
                Console.WriteLine("No books available to restock.");
            }
        }
        EnterCommand();
    }
}