using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Подключение базы данных Entity Framework (In-Memory)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("TaskManagerDb"));

// Настройка Аутентификации через Cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });

builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

#region 1. Главная страница
app.MapGet("/", (HttpContext context) =>
{
    bool isAuthenticated = context.User.Identity?.IsAuthenticated ?? false;
    string username = context.User.Identity?.Name ?? "";

    string authButtons = isAuthenticated
        ? $@"<span class='navbar-text me-3'>Вітаємо, <strong>{username}</strong>!</span>
             <a href='/Account/Profile' class='btn btn-outline-light me-2'>Профіль</a>
             <a href='/Account/Logout' class='btn btn-danger'>Вийти</a>"
        : @"<a href='/Account/Login' class='btn btn-outline-light me-2'>Увійти</a>
           <a href='/Account/Register' class='btn btn-primary'>Реєстрація</a>";

    string body = $@"
        <div class='text-center mt-5'>
            <h1 class='display-4 fw-bold'>Система керування завданнями (Task Manager)</h1>
            <p class='lead mt-3'>Авторизуйтесь, щоб створювати, шукати та керувати власними нотатками.</p>
            {(isAuthenticated ? "<a href='/Tasks' class='btn btn-success btn-lg mt-3'>Перейти до моїх нотаток</a>" : "")}
        </div>";

    return Results.Content(GetHtmlWrapper("Головна", body, authButtons), "text/html");
});
#endregion

#region 2. Авторизация, Регистрация и Профиль
app.MapGet("/Account/Register", () =>
{
    string formHtml = @"
        <div class='row justify-content-center'>
            <div class='col-md-5 card p-4 shadow-sm'>
                <h3 class='text-center mb-3'>Реєстрація</h3>
                <form method='post' action='/Account/Register'>
                    <div class='mb-3'>
                        <label class='form-label'>Логін</label>
                        <input name='username' class='form-control' required />
                    </div>
                    <div class='mb-3'>
                        <label class='form-label'>Email</label>
                        <input type='email' name='email' class='form-control' required />
                    </div>
                    <div class='mb-3'>
                        <label class='form-label'>Пароль</label>
                        <input type='password' name='password' class='form-control' required />
                    </div>
                    <button type='submit' class='btn btn-primary w-100'>Зареєструватися</button>
                </form>
            </div>
        </div>";

    return Results.Content(GetHtmlWrapper("Реєстрація", formHtml), "text/html");
});

app.MapPost("/Account/Register", async ([FromForm] string username, [FromForm] string email, [FromForm] string password, AppDbContext db) =>
{
    if (await db.Users.AnyAsync(u => u.Username == username))
    {
        return Results.Content(GetHtmlWrapper("Помилка", "<div class='alert alert-danger'>Користувач із таким логіном вже існує!</div><a href='/Account/Register' class='btn btn-secondary'>Назад</a>"), "text/html");
    }

    var user = new User { Username = username, Email = email, Password = password };
    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Redirect("/Account/Login");
});

app.MapGet("/Account/Login", () =>
{
    string formHtml = @"
        <div class='row justify-content-center'>
            <div class='col-md-5 card p-4 shadow-sm'>
                <h3 class='text-center mb-3'>Вхід у систему</h3>
                <form method='post' action='/Account/Login'>
                    <div class='mb-3'>
                        <label class='form-label'>Логін</label>
                        <input name='username' class='form-control' required />
                    </div>
                    <div class='mb-3'>
                        <label class='form-label'>Пароль</label>
                        <input type='password' name='password' class='form-control' required />
                    </div>
                    <button type='submit' class='btn btn-success w-100'>Увійти</button>
                </form>
            </div>
        </div>";

    return Results.Content(GetHtmlWrapper("Вхід", formHtml), "text/html");
});

app.MapPost("/Account/Login", async ([FromForm] string username, [FromForm] string password, HttpContext context, AppDbContext db) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username && u.Password == password);
    if (user == null)
    {
        return Results.Content(GetHtmlWrapper("Помилка", "<div class='alert alert-danger'>Невірний логін або пароль!</div><a href='/Account/Login' class='btn btn-secondary'>Назад</a>"), "text/html");
    }

    // Сохранение идентификатора пользователя в Claim (ClaimTypes.NameIdentifier)
    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(ClaimTypes.Email, user.Email)
    };

    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

    return Results.Redirect("/Tasks");
});

app.MapGet("/Account/Logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapGet("/Account/Profile", [Authorize] async (HttpContext context, AppDbContext db) =>
{
    // Получение ID из Claim
    int userId = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var user = await db.Users.FindAsync(userId);

    string authButtons = $@"<span class='navbar-text me-3'>Вітаємо, <strong>{user?.Username}</strong>!</span>
                         <a href='/Account/Logout' class='btn btn-danger'>Вийти</a>";

    string body = $@"
        <div class='card p-4 shadow-sm mx-auto' style='max-width: 500px;'>
            <h3>Профіль користувача</h3>
            <hr/>
            <p><strong>ID в базі даних:</strong> {user?.Id}</p>
            <p><strong>Логін:</strong> {user?.Username}</p>
            <p><strong>Email:</strong> {user?.Email}</p>
            <a href='/Tasks' class='btn btn-primary mt-2'>Перейти до нотаток</a>
        </div>";

    return Results.Content(GetHtmlWrapper("Профіль", body, authButtons), "text/html");
});
#endregion

#region 3. Управление заметками (Task Manager)
app.MapGet("/Tasks", [Authorize] async (string? search, HttpContext context, AppDbContext db) =>
{
    // Получение ID пользователя из Claims
    int userId = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    string username = context.User.Identity?.Name ?? "";

    var query = db.UserTasks.Where(t => t.UserId == userId);

    // Поиск по заметкам
    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(t => t.Title.Contains(search) || t.Description.Contains(search));
    }

    var tasks = await query.ToListAsync();

    string taskCards = string.Join("", tasks.Select(t => $@"
        <div class='col-md-4 mb-3'>
            <div class='card h-100 shadow-sm'>
                <div class='card-body d-flex flex-column justify-content-between'>
                    <div>
                        <h5 class='card-title'>{t.Title}</h5>
                        <p class='card-text text-muted'>{t.Description}</p>
                    </div>
                    <div class='mt-3 d-flex justify-content-between align-items-center'>
                        <small class='text-secondary'>{t.CreatedAt:dd.MM.yyyy HH:mm}</small>
                        <form method='post' action='/Tasks/Delete' style='margin:0;'>
                            <input type='hidden' name='id' value='{t.Id}' />
                            <button type='submit' class='btn btn-sm btn-outline-danger'>Видалити</button>
                        </form>
                    </div>
                </div>
            </div>
        </div>"));

    string authButtons = $@"<span class='navbar-text me-3'>Вітаємо, <strong>{username}</strong>!</span>
                         <a href='/Account/Profile' class='btn btn-outline-light me-2'>Профіль</a>
                         <a href='/Account/Logout' class='btn btn-danger'>Вийти</a>";

    string body = $@"
        <h2>Мої нотатки</h2>
        
        <!-- Форма добавления -->
        <div class='card p-3 mb-4 shadow-sm bg-light'>
            <h5>Додати нову нотатку</h5>
            <form method='post' action='/Tasks/Create' class='row g-2 mt-1'>
                <div class='col-md-4'>
                    <input name='title' class='form-control' placeholder='Заголовок' required />
                </div>
                <div class='col-md-6'>
                    <input name='description' class='form-control' placeholder='Опис...' required />
                </div>
                <div class='col-md-2'>
                    <button type='submit' class='btn btn-success w-100'>Додати</button>
                </div>
            </form>
        </div>

        <!-- Форма поиска -->
        <form method='get' action='/Tasks' class='row g-2 mb-4'>
            <div class='col-md-10'>
                <input name='search' value='{search}' class='form-control' placeholder='Пошук за заголовком або описом...' />
            </div>
            <div class='col-md-2'>
                <button type='submit' class='btn btn-primary w-100'>Шукати</button>
            </div>
        </form>

        <!-- Список заметок -->
        <div class='row'>
            {(tasks.Any() ? taskCards : "<p class='text-muted'>Нотаток не знайдено.</p>")}
        </div>";

    return Results.Content(GetHtmlWrapper("Task Manager", body, authButtons), "text/html");
});

app.MapPost("/Tasks/Create", [Authorize] async ([FromForm] string title, [FromForm] string description, HttpContext context, AppDbContext db) =>
{
    int userId = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    var task = new UserTask
    {
        Title = title,
        Description = description,
        UserId = userId,
        CreatedAt = DateTime.Now
    };

    db.UserTasks.Add(task);
    await db.SaveChangesAsync();

    return Results.Redirect("/Tasks");
});

app.MapPost("/Tasks/Delete", [Authorize] async ([FromForm] int id, HttpContext context, AppDbContext db) =>
{
    int userId = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var task = await db.UserTasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

    if (task != null)
    {
        db.UserTasks.Remove(task);
        await db.SaveChangesAsync();
    }

    return Results.Redirect("/Tasks");
});
#endregion

app.Run();

#region HTML Шаблон-обертка
static string GetHtmlWrapper(string title, string bodyContent, string authButtons = "")
{
    return $@"<!DOCTYPE html>
    <html lang='uk'>
    <head>
        <meta charset='utf-8' />
        <meta name='viewport' content='width=device-width, initial-scale=1.0' />
        <title>{title}</title>
        <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css' rel='stylesheet' />
    </head>
    <body class='bg-light'>
        <nav class='navbar navbar-expand-lg navbar-dark bg-dark mb-4'>
            <div class='container'>
                <a class='navbar-brand fw-bold' href='/'>Task Manager</a>
                <div class='d-flex align-items-center'>
                    {authButtons}
                </div>
            </div>
        </nav>
        <div class='container pb-5'>
            {bodyContent}
        </div>
    </body>
    </html>";
}
#endregion

#region База данных (EF Core) и Сущности
public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class UserTask
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserTask> UserTasks => Set<UserTask>();
}
#endregion