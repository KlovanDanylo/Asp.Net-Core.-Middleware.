using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("ShopDb"));

builder.Services.AddSingleton<BookingService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!db.Products.Any())
    {
        db.Products.AddRange(
            new Product { Id = 1, Name = "Ноутбук", Price = 25000 },
            new Product { Id = 2, Name = "Мишка", Price = 500 },
            new Product { Id = 3, Name = "Клавіатура", Price = 1200 }
        );
        db.SaveChanges();
    }
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();



public class ProductController : Controller
{
    private readonly AppDbContext _db;

    public ProductController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var products = _db.Products.ToList();
        return Json(products);
    }

    [HttpGet]
    public IActionResult Create()
    {
        string html = @"
            <h2>Додати товар</h2>
            <form action='/Product/Create' method='post'>
                Ім'я: <input type='text' name='name' required /><br/><br/>
                Ціна: <input type='number' step='0.01' name='price' required /><br/><br/>
                <button type='submit'>Зберегти</button>
            </form>";
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpPost]
    public IActionResult Create(string name, decimal price)
    {
        var product = new Product { Name = name, Price = price };
        _db.Products.Add(product);
        _db.SaveChanges();
        return Json(product);
    }

    [HttpGet]
    public IActionResult Search(string keyword)
    {
        if (string.IsNullOrEmpty(keyword))
            return Json(_db.Products.ToList());

        var result = _db.Products
            .Where(p => p.Name.ToLower().Contains(keyword.ToLower()))
            .ToList();

        return Json(result);
    }

    [HttpGet]
    public IActionResult Details(int id)
    {
        var product = _db.Products.FirstOrDefault(p => p.Id == id);
        if (product == null) return NotFound("Товар не знайдено");
        return Json(product);
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        var product = _db.Products.FirstOrDefault(p => p.Id == id);
        if (product == null) return NotFound("Товар не знайдено");

        _db.Products.Remove(product);
        _db.SaveChanges();
        return Json(product);
    }
}

public class RoomsController : Controller
{
    private readonly BookingService _bookingService;

    public RoomsController(BookingService bookingService)
    {
        _bookingService = bookingService;
    }

    public IActionResult Index()
    {
        return Json(_bookingService.Rooms);
    }

    public IActionResult Details(int id, DateTime? date)
    {
        var room = _bookingService.Rooms.FirstOrDefault(r => r.Id == id);
        if (room == null) return NotFound("Кімнату не знайдено");

        bool isAvailable = true;
        if (date.HasValue)
        {
            isAvailable = _bookingService.IsRoomAvailable(id, date.Value);
        }

        return Json(new { Room = room, CheckedDate = date, IsAvailable = isAvailable });
    }
}

public class BookingController : Controller
{
    private readonly BookingService _bookingService;

    public BookingController(BookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return Json(_bookingService.GetBookings());
    }

    [HttpPost]
    public IActionResult Create(int roomId, string user, DateTime bookingTime)
    {
        if (bookingTime <= DateTime.Now)
        {
            return BadRequest("Неможливо забронювати час у минулому!");
        }

        if (!_bookingService.IsRoomAvailable(roomId, bookingTime))
        {
            return BadRequest("Цей час уже зайнятий!");
        }

        _bookingService.AddBooking(roomId, user, bookingTime);

        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult Cancel(int id)
    {
        _bookingService.CancelBooking(id);
        return RedirectToAction("Index");
    }
}



public class RecipeController : Controller
{
    private readonly List<Recipe> _recipes = new()
    {
        new Recipe { Id = 1, Name = "Борщ", Category = "Супи", Ingredients = newList<string> { "буряк", "капуста", "м'ясо" } },
        new Recipe { Id = 2, Name = "Піца", Category = "Випічка", Ingredients = newList<string> { "сир", "томати", "тісто" } },
        new Recipe { Id = 3, Name = "Омлет", Category = "Сніданки", Ingredients = newList<string> { "яйця", "молоко", "сир" } },
        new Recipe { Id = 4, Name = "Суп з грибами", Category = "Супи", Ingredients = newList<string> { "гриби", "картопля", "цибуля" } }
    };

    public IActionResult Random()
    {
        var rand = new Random();
        var recipe = _recipes[rand.Next(_recipes.Count)];
        return Json(recipe);
    }

    public IActionResult Ingredient(string ingredient)
    {
        if (string.IsNullOrEmpty(ingredient)) return BadRequest("Вкажіть інгредієнт");

        var filtered = _recipes
            .Where(r => r.Ingredients.Any(i => i.Equals(ingredient, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (!filtered.Any()) return NotFound("Рецептів з таким інгредієнтом не знайдено");

        var rand = new Random();
        return Json(filtered[rand.Next(filtered.Count)]);
    }

    public IActionResult Category(string category)
    {
        if (string.IsNullOrEmpty(category)) return BadRequest("Вкажіть категорію");

        var filtered = _recipes
            .Where(r => r.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!filtered.Any()) return NotFound("Рецептів у цій категорії не знайдено");

        var rand = new Random();
        return Json(filtered[rand.Next(filtered.Count)]);
    }
}


public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Product> Products { get; set; }
}

public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Capacity { get; set; }
}

public class Booking
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public string User { get; set; } = "";
    public DateTime BookingTime { get; set; }
}

public class BookingService
{
    public List<Room> Rooms { get; } = new()
    {
        new Room { Id = 1, Name = "Переговорка №1", Capacity = 6 },
        new Room { Id = 2, Name = "Переговорка №2 (Велика)", Capacity = 12 }
    };

    private readonly List<Booking> _bookings = new();
    private int _nextId = 1;

    public List<Booking> GetBookings() => _bookings;

    public bool IsRoomAvailable(int roomId, DateTime time)
    {
        return !_bookings.Any(b => b.RoomId == roomId && b.BookingTime == time);
    }

    public void AddBooking(int roomId, string user, DateTime time)
    {
        _bookings.Add(new Booking { Id = _nextId++, RoomId = roomId, User = user, BookingTime = time });
    }

    public void CancelBooking(int id)
    {
        var booking = _bookings.FirstOrDefault(b => b.Id == id);
        if (booking != null) _bookings.Remove(booking);
    }
}

public class Recipe
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public List<string> Ingredients { get; set; } = new();
}