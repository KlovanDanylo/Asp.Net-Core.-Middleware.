using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// Додаємо сервіси
builder.Services.AddSingleton<IUserRepository, UserRepository>(); // Singleton зберігає даних у пам'яті під час сеансу

var app = builder.Build();

app.UseStaticFiles(); // Дозволяє відкривати завантажені картинки через браузер

// =========================================================================
// ГОЛОВНЕ ЗАВДАННЯ: Веб-сайт управління користувачами (HTML)
// =========================================================================

// 1. Головна сторінка — список користувачів та форма додавання
app.MapGet("/", (IUserRepository repo) =>
{
    var users = repo.GetAll();
    
    string html = "<html><head><meta charset='utf-8'><title>Користувачі</title></head><body>";
    html += "<h1>Управління користувачами</h1>";
    
    // Форма додавання
    html += "<h3>Додати користувача:</h3>";
    html += "<form action='/users/add' method='post'>";
    html += "Ім'я: <input type='text' name='name' required /> ";
    html += "Email: <input type='email' name='email' required /> ";
    html += "<button type='submit'>Зберегти</button>";
    html += "</form><hr/>";

    // Список користувачів
    html += "<h3>Список користувачів:</h3><table border='1'><tr><th>ID</th><th>Ім'я</th><th>Email</th><th>Дії</th></tr>";
    foreach (var u in users)
    {
        html += $"<tr><td>{u.Id}</td><td>{u.Name}</td><td>{u.Email}</td>";
        html += $"<td><a href='/users/edit/{u.Id}'>Редагувати</a> | <a href='/users/delete/{u.Id}'>Видалити</a></td></tr>";
    }
    html += "</table></body></html>";

    return Results.Content(html, "text/html");
});

// 2. Додавання користувача
app.MapPost("/users/add", (HttpRequest request, IUserRepository repo) =>
{
    string name = request.Form["name"];
    string email = request.Form["email"];
    
    repo.Add(new User { Name = name, Email = email });
    return Results.Redirect("/");
});

// 3. Форма редагування користувача (з обробкою неіснуючого ID)
app.MapGet("/users/edit/{id}", (string id, IUserRepository repo) =>
{
    if (!int.TryParse(id, out int userId))
    {
        return Results.Content("<h1>Помилка: Невірний формат ID!</h1>", "text/html");
    }

    var user = repo.GetById(userId);
    if (user == null)
    {
        return Results.Content("<h1>Помилка: Користувача з таким ID не знайдено!</h1>", "text/html");
    }

    string html = "<html><head><meta charset='utf-8'></head><body>";
    html += "<h1>Редагування користувача</h1>";
    html += $"<form action='/users/edit/{user.Id}' method='post'>";
    html += $"Ім'я: <input type='text' name='name' value='{user.Name}' required /> ";
    html += $"Email: <input type='email' name='email' value='{user.Email}' required /> ";
    html += "<button type='submit'>Зберегти зміни</button>";
    html += "</form></body></html>";

    return Results.Content(html, "text/html");
});

// 4. Збереження змін після редагування
app.MapPost("/users/edit/{id}", (int id, HttpRequest request, IUserRepository repo) =>
{
    string name = request.Form["name"];
    string email = request.Form["email"];

    repo.Update(new User { Id = id, Name = name, Email = email });
    return Results.Redirect("/");
});

// 5. Видалення користувача
app.MapGet("/users/delete/{id}", (int id, IUserRepository repo) =>
{
    repo.Delete(id);
    return Results.Redirect("/");
});


// =========================================================================
// ЗАВДАННЯ 1: API для онлайн-магазину (з авторизацією за токеном)
// =========================================================================

var products = new List<Product>
{
    new Product { Id = 1, Name = "Ноутбук", Price = 25000 },
    new Product { Id = 2, Name = "Телефон", Price = 12000 }
};

// Middleware для перевірки авторизації за токеном в API
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        string token = context.Request.Query["token"];
        
        if (token != "secret123")
        {
            context.Response.StatusCode = 401; // Unauthorized
            await context.Response.WriteAsJsonAsync(new { error = "Неавторизовано! Передайте токен ?token=secret123" });
            return;
        }
    }
    await next();
});

// 1) Отримати всі продукти
app.MapGet("/api/products", () => Results.Ok(products));

// 2) Отримати один продукт за ID
app.MapGet("/api/products/{id}", (int id) =>
{
    var product = products.FirstOrDefault(p => p.Id == id);
    if (product == null) return Results.Json(new { error = "Продукт не знайдено" }, statusCode: 404);
    return Results.Ok(product);
});

// 3) Додати продукт
app.MapPost("/api/products/add", (HttpRequest request) =>
{
    string name = request.Form["name"];
    decimal price = decimal.Parse(request.Form["price"]);
    
    int newId = products.Count > 0 ? products.Max(p => p.Id) + 1 : 1;
    var newProduct = new Product { Id = newId, Name = name, Price = price };
    
    products.Add(newProduct);
    return Results.Ok(new { message = "Продукт додано", product = newProduct });
});

// 4) Видалити продукт
app.MapPost("/api/products/delete", (HttpRequest request) =>
{
    int id = int.Parse(request.Form["id"]);
    var product = products.FirstOrDefault(p => p.Id == id);
    
    if (product == null) return Results.Json(new { error = "Продукт не знайдено" }, statusCode: 404);
    
    products.Remove(product);
    return Results.Ok(new { message = "Продукт видалено" });
});


// =========================================================================
// ЗАВДАННЯ 3: Завантаження зображень через IWebHostEnvironment
// =========================================================================

app.MapPost("/upload", async (IFormFile file, IWebHostEnvironment env) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest("Файл не вибрано!");
    }

    // Папка wwwroot/uploads
    string uploadsFolder = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");
    
    if (!Directory.Exists(uploadsFolder))
    {
        Directory.CreateDirectory(uploadsFolder);
    }

    // Зберігаємо файл
    string filePath = Path.Combine(uploadsFolder, file.FileName);
    using (var stream = new FileStream(filePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    // Повертаємо URL зображення
    string fileUrl = $"/uploads/{file.FileName}";
    return Results.Ok(new { message = "Зображення завантажено успішно", url = fileUrl });
});

app.Run();


// =========================================================================
// МОДЕЛІ ТА РЕПОЗИТОРІЙ
// =========================================================================

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

public interface IUserRepository
{
    List<User> GetAll();
    User? GetById(int id);
    void Add(User user);
    void Update(User user);
    void Delete(int id);
}

public class UserRepository : IUserRepository
{
    private readonly List<User> _users = new()
    {
        new User { Id = 1, Name = "Іван", Email = "ivan@gmail.com" },
        new User { Id = 2, Name = "Марія", Email = "maria@gmail.com" }
    };

    public List<User> GetAll() => _users;

    public User? GetById(int id) => _users.FirstOrDefault(u => u.Id == id);

    public void Add(User user)
    {
        user.Id = _users.Count > 0 ? _users.Max(u => u.Id) + 1 : 1;
        _users.Add(user);
    }

    public void Update(User user)
    {
        var existing = GetById(user.Id);
        if (existing != null)
        {
            existing.Name = user.Name;
            existing.Email = user.Email;
        }
    }

    public void Delete(int id)
    {
        var user = GetById(id);
        if (user != null) _users.Remove(user);
    }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}