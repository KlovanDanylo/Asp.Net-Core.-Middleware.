using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IUserRepository, UserRepository>(); // Singleton зберігає даних у пам'яті під час сеансу

var app = builder.Build();

app.UseStaticFiles();
app.MapGet("/", (IUserRepository repo) =>
{
    var users = repo.GetAll();
    
    string html = "<html><head><meta charset='utf-8'><title>Користувачі</title></head><body>";
    html += "<h1>Управління користувачами</h1>";
    
    html += "<h3>Додати користувача:</h3>";
    html += "<form action='/users/add' method='post'>";
    html += "Ім'я: <input type='text' name='name' required /> ";
    html += "Email: <input type='email' name='email' required /> ";
    html += "<button type='submit'>Зберегти</button>";
    html += "</form><hr/>";

    html += "<h3>Список користувачів:</h3><table border='1'><tr><th>ID</th><th>Ім'я</th><th>Email</th><th>Дії</th></tr>";
    foreach (var u in users)
    {
        html += $"<tr><td>{u.Id}</td><td>{u.Name}</td><td>{u.Email}</td>";
        html += $"<td><a href='/users/edit/{u.Id}'>Редагувати</a> | <a href='/users/delete/{u.Id}'>Видалити</a></td></tr>";
    }
    html += "</table></body></html>";

    return Results.Content(html, "text/html");
});

app.MapPost("/users/add", (HttpRequest request, IUserRepository repo) =>
{
    string name = request.Form["name"];
    string email = request.Form["email"];
    
    repo.Add(new User { Name = name, Email = email });
    return Results.Redirect("/");
});

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

app.MapPost("/users/edit/{id}", (int id, HttpRequest request, IUserRepository repo) =>
{
    string name = request.Form["name"];
    string email = request.Form["email"];

    repo.Update(new User { Id = id, Name = name, Email = email });
    return Results.Redirect("/");
});

app.MapGet("/users/delete/{id}", (int id, IUserRepository repo) =>
{
    repo.Delete(id);
    return Results.Redirect("/");
});



var products = new List<Product>
{
    new Product { Id = 1, Name = "Ноутбук", Price = 25000 },
    new Product { Id = 2, Name = "Телефон", Price = 12000 }
};

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        string token = context.Request.Query["token"];
        
        if (token != "secret123")
        {
            context.Response.StatusCode = 401; 
            await context.Response.WriteAsJsonAsync(new { error = "Неавторизовано! Передайте токен ?token=secret123" });
            return;
        }
    }
    await next();
});

app.MapGet("/api/products", () => Results.Ok(products));

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

app.MapPost("/api/products/delete", (HttpRequest request) =>
{
    int id = int.Parse(request.Form["id"]);
    var product = products.FirstOrDefault(p => p.Id == id);
    
    if (product == null) return Results.Json(new { error = "Продукт не знайдено" }, statusCode: 404);
    
    products.Remove(product);
    return Results.Ok(new { message = "Продукт видалено" });
});

app.MapPost("/upload", async (IFormFile file, IWebHostEnvironment env) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest("Файл не вибрано!");
    }

    string uploadsFolder = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");
    
    if (!Directory.Exists(uploadsFolder))
    {
        Directory.CreateDirectory(uploadsFolder);
    }

    string filePath = Path.Combine(uploadsFolder, file.FileName);
    using (var stream = new FileStream(filePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    string fileUrl = $"/uploads/{file.FileName}";
    return Results.Ok(new { message = "Зображення завантажено успішно", url = fileUrl });
});

app.Run();
