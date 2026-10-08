using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("GalleryDb"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
    if (!context.Books.Any())
    {
        var book = new Book 
        { 
            Title = "Кобзар", 
            Author = "Тарас Шевченко", 
            Genre = "Поезія", 
            Price = 350 
        };
        book.Images.Add(new BookImage { FilePath = "/images/kobzar.jpg" });
        book.Comments.Add(new Comment { User = "Олена", Text = "Чудова збірка!" });

        context.Books.Add(book);
        context.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public class Image
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public List<BookImage> Images { get; set; } = new();
    public List<Comment> Comments { get; set; } = new();
}

public class BookImage
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int BookId { get; set; }
    public Book? Book { get; set; }
}

public class Comment
{
    public int Id { get; set; }
    public string User { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int BookId { get; set; }
    public Book? Book { get; set; }
}

public class ContactFormModel
{
    [Required(ErrorMessage = "Введіть ім'я")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введіть Email"), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введіть повідомлення")]
    public string Message { get; set; } = string.Empty;
}

public class Product
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Popularity { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Book> Books => Set<Book>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<BookImage> BookImages => Set<BookImage>();
}

public class HomeController : Controller
{
    private readonly AppDbContext _context;

    public HomeController(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        return View();
    }

    // Завдання 1: Деталі книги та коментарі
    public IActionResult BookDetails(int id)
    {
        var book = _context.Books
            .Include(b => b.Images)
            .Include(b => b.Comments)
            .FirstOrDefault(b => b.Id == id);

        if (book == null) return NotFound();

        return View(book);
    }

    // Завдання 1: Додавання коментаря
    [HttpPost]
    public IActionResult AddComment(int bookId, string user, string text)
    {
        if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(user))
        {
            var comment = new Comment
            {
                BookId = bookId,
                User = user,
                Text = text,
                CreatedAt = DateTime.Now
            };

            _context.Comments.Add(comment);
            _context.SaveChanges();
        }

        return RedirectToAction("BookDetails", new { id = bookId });
    }

    [HttpPost]
    public IActionResult SubmitContact(ContactFormModel model)
    {
        if (ModelState.IsValid)
        {
            return RedirectToAction("ThankYou");
        }
        return View("Index");
    }

    public IActionResult ThankYou()
    {
        return Content("<h2>Дякуємо за ваше повідомлення!</h2><p>Ми зв'яжемося з вами найближчим часом.</p><a href='/'>На головну</a>", "text/html; charset=utf-8");
    }
}


public class GalleryViewComponent : ViewComponent
{
    private static readonly List<Image> Images = new()
    {
        new Image { Id = 1, Title = "Природа", Description = "Красивий пейзаж", FilePath = "/images/nature.jpg" },
        new Image { Id = 2, Title = "Місто", Description = "Нічне місто", FilePath = "/images/city.jpg" },
        new Image { Id = 3, Title = "Гори", Description = "Засніжені вершини", FilePath = "/images/mountains.jpg" }
    };

    public IViewComponentResult Invoke()
    {
        return View(Images);
    }
}

public class ContactFormViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        return View(new ContactFormModel());
    }
}

public class ProductListViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string? category, decimal? maxPrice, string? sortBy)
    {
        var products = GetSampleProducts().AsQueryable();

        if (!string.IsNullOrEmpty(category))
            products = products.Where(p => p.Category == category);

        if (maxPrice.HasValue)
            products = products.Where(p => p.Price <= maxPrice.Value);

        products = sortBy switch
        {
            "price_asc" => products.OrderBy(p => p.Price),
            "price_desc" => products.OrderByDescending(p => p.Price),
            "name" => products.OrderBy(p => p.Name),
            _ => products.OrderByDescending(p => p.Popularity)
        };

        return View(products.ToList());
    }

    private List<Product> GetSampleProducts() => new()
    {
        new Product { Name = "Ноутбук", Category = "Електроніка", Price = 25000, Popularity = 5, ImageUrl = "/images/laptop.jpg" },
        new Product { Name = "Смартфон", Category = "Електроніка", Price = 15000, Popularity = 9, ImageUrl = "/images/phone.jpg" },
        new Product { Name = "Книга", Category = "Освіта", Price = 400, Popularity = 7, ImageUrl = "/images/book.jpg" }
    };
}