using System.Reflection;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAutoMapper(cfg =>
{
    cfg.CreateMap<Note, NoteDto>();
});

var app = builder.Build();

app.UseRouting();

#region 1. Личный блог и Настройки темы (Cookie)

app.MapGet("/", (HttpContext context) =>
{
    string theme = context.Request.Cookies["site_theme"] ?? "light";

    var newsList = DataStore.NewsList;
    string newsCards = string.Join("", newsList.Select(n => $@"
        <div class='col-md-6 mb-3'>
            <div class='card shadow-sm h-100'>
                <div class='card-body'>
                    <h5 class='card-title'>{n.Title}</h5>
                    <p class='card-text'>{n.Content}</p>
                    <small class='text-muted'>{n.PublishedAt:dd.MM.yyyy}</small>
                </div>
            </div>
        </div>"));

    string body = $@"
        <h2 class='mb-4'>Особистий блог</h2>
        <div class='row'>{newsCards}</div>";

    return Results.Content(GetHtmlWrapper("Блог", body, theme), "text/html");
});

app.MapGet("/settings", (HttpContext context) =>
{
    string currentTheme = context.Request.Cookies["site_theme"] ?? "light";

    string body = $@"
        <h2>Налаштування читання</h2>
        <form method='post' action='/settings'>
            <div class='mb-3'>
                <label class='form-label fw-bold'>Оберіть тему оформлення:</label>
                <div class='form-check'>
                    <input class='form-check-input' type='radio' name='theme' id='lightTheme' value='light' {(currentTheme == "light" ? "checked" : "")}>
                    <label class='form-check-label' for='lightTheme'>Світла тема</label>
                </div>
                <div class='form-check'>
                    <input class='form-check-input' type='radio' name='theme' id='darkTheme' value='dark' {(currentTheme == "dark" ? "checked" : "")}>
                    <label class='form-check-label' for='darkTheme'>Темна тема</label>
                </div>
            </div>
            <button type='submit' class='btn btn-primary'>Зберегти налаштування</button>
        </form>";

    return Results.Content(GetHtmlWrapper("Налаштування", body, currentTheme), "text/html");
});

app.MapPost("/settings", ([FromForm] string theme, HttpContext context) =>
{
    // Записываем куку на 30 дней
    context.Response.Cookies.Append("site_theme", theme, new CookieOptions
    {
        Expires = DateTimeOffset.Now.AddDays(30)
    });

    return Results.Redirect("/settings");
});

#endregion

#region 2. Нотатки с категориями (Cookie)

app.MapGet("/notes", (string? category, HttpContext context) =>
{
    string theme = context.Request.Cookies["site_theme"] ?? "light";

    if (string.IsNullOrEmpty(category))
    {
        category = context.Request.Cookies["last_category"] ?? "Загальне";
    }
    else
    {
        context.Response.Cookies.Append("last_category", category, new CookieOptions
        {
            Expires = DateTimeOffset.Now.AddDays(30)
        });
    }

    var categories = new[] { "Загальне", "Робота", "Навчання", "Особисте" };
    var filteredNotes = DataStore.NotesList.Where(n => n.Category == category).ToList();

    string categoryButtons = string.Join(" ", categories.Select(c => $@"
        <a href='/notes?category={c}' class='btn {(c == category ? "btn-primary" : "btn-outline-primary")} me-2 mb-2'>{c}</a>"));

    string notesListHtml = string.Join("", filteredNotes.Select(n => $@"
        <div class='list-group-item'>
            <h5 class='mb-1'>{n.Title}</h5>
            <p class='mb-1'>{n.Content}</p>
            <small class='text-muted'>Категорія: {n.Category}</small>
        </div>"));

    string body = $@"
        <h2>Нотатки</h2>
        <p>Остання вибрана категорія зберігається в Cookie.</p>
        <div class='mb-4'>{categoryButtons}</div>
        <h4>Категорія: {category}</h4>
        <div class='list-group mt-3'>
            {(filteredNotes.Any() ? notesListHtml : "<div class='alert alert-secondary'>Нотаток у цій категорії немає.</div>")}
        </div>";

    return Results.Content(GetHtmlWrapper("Нотатки", body, theme), "text/html");
});

#endregion

#region 3. Маппинг объектов (AutoMapper, Рефлексия, Метод расширения)

app.MapGet("/mapping-demo", (IMapper autoMapper, HttpContext context) =>
{
    string theme = context.Request.Cookies["site_theme"] ?? "light";
    var sourceNote = new Note { Id = 101, Title = "Тестова нотатка", Content = "Вміст для мапінгу", Category = "Тест" };

    var dtoAutoMapper = autoMapper.Map<NoteDto>(sourceNote);

    var dtoExtension = sourceNote.ToDto();

    var dtoReflection = ReflectionMapper.MapProperties<Note, NoteDto>(sourceNote);

    string body = $@"
        <h2>Демонстрація мапінгу об'єктів 3 способами</h2>
        <div class='card mb-3 p-3'>
            <h5>Вихідний об'єкт (Note):</h5>
            <code>Id: {sourceNote.Id}, Title: {sourceNote.Title}, Content: {sourceNote.Content}, Category: {sourceNote.Category}</code>
        </div>

        <div class='row g-3'>
            <div class='col-md-4'>
                <div class='card border-info h-100 p-3'>
                    <h5>1. AutoMapper</h5>
                    <p><strong>Title:</strong> {dtoAutoMapper.Title}</p>
                    <p><strong>Content:</strong> {dtoAutoMapper.Content}</p>
                </div>
            </div>
            <div class='col-md-4'>
                <div class='card border-success h-100 p-3'>
                    <h5>2. Метод розширення</h5>
                    <p><strong>Title:</strong> {dtoExtension.Title}</p>
                    <p><strong>Content:</strong> {dtoExtension.Content}</p>
                </div>
            </div>
            <div class='col-md-4'>
                <div class='card border-warning h-100 p-3'>
                    <h5>3. Рефлексія</h5>
                    <p><strong>Title:</strong> {dtoReflection.Title}</p>
                    <p><strong>Content:</strong> {dtoReflection.Content}</p>
                </div>
            </div>
        </div>";

    return Results.Content(GetHtmlWrapper("Мапінг", body, theme), "text/html");
});

#endregion

app.Run();

#region HTML Шаблон с поддержкой Light / Dark темы

static string GetHtmlWrapper(string title, string bodyContent, string theme)
{
    bool isDark = theme == "dark";
    string bgClass = isDark ? "bg-dark text-white" : "bg-light text-dark";
    string navClass = isDark ? "navbar-dark bg-secondary" : "navbar-dark bg-primary";
    string cardBg = isDark ? "bg-secondary text-white" : "bg-white";

    return $@"<!DOCTYPE html>
    <html lang='uk'>
    <head>
        <meta charset='utf-8' />
        <meta name='viewport' content='width=device-width, initial-scale=1.0' />
        <title>{title}</title>
        <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css' rel='stylesheet' />
        <style>
            body {{ min-height: 100vh; transition: background-color 0.3s, color 0.3s; }}
            {(isDark ? ".card, .list-group-item { background-color: #2c3034 !important; color: #fff !important; border-color: #454d55; }" : "")}
        </style>
    </head>
    <body class='{bgClass}'>
        <nav class='navbar navbar-expand-lg {navClass} mb-4'>
            <div class='container'>
                <a class='navbar-brand fw-bold' href='/'>Блог & Нотатки</a>
                <div class='navbar-nav'>
                    <a class='nav-link' href='/'>Блог</a>
                    <a class='nav-link' href='/notes'>Нотатки</a>
                    <a class='nav-link' href='/settings'>Налаштування теми</a>
                    <a class='nav-link' href='/mapping-demo'>Мапінг</a>
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

#region Модели данных и методы маппинга

public class News
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
}

public class Note
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class NoteDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public static class MappingExtensions
{
    public static NoteDto ToDto(this Note note)
    {
        return new NoteDto
        {
            Title = note.Title,
            Content = note.Content
        };
    }
}

public static class ReflectionMapper
{
    public static TDestination MapProperties<TSource, TDestination>(TSource source)
        where TDestination : new()
    {
        var destination = new TDestination();
        var sourceType = typeof(TSource);
        var destinationType = typeof(TDestination);

        foreach (var destProp in destinationType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!destProp.CanWrite) continue;

            var sourceProp = sourceType.GetProperty(destProp.Name, BindingFlags.Public | BindingFlags.Instance);
            if (sourceProp != null && sourceProp.CanRead && destProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
            {
                var value = sourceProp.GetValue(source);
                destProp.SetValue(destination, value);
            }
        }

        return destination;
    }
}

public static class DataStore
{
    public static List<News> NewsList = new()
    {
        new News { Id = 1, Title = "Ласкаво просимо до блогу!", Content = "Це перша новина у нашому навчальному блозі ASP.NET Core.", PublishedAt = DateTime.Now.AddDays(-2) },
        new News { Id = 2, Title = "Оновлення Cookie", Content = "Тепер ви можете обирати тему та зберігати категорію нотаток.", PublishedAt = DateTime.Now.AddDays(-1) }
    };

    public static List<Note> NotesList = new()
    {
        new Note { Id = 1, Title = "Купити продукти", Content = "Молоко, хліб, яблука", Category = "Загальне" },
        new Note { Id = 2, Title = "Зробити ТЗ", Content = "Написати проектну роботу з ASP.NET", Category = "Навчання" },
        new Note { Id = 3, Title = "Здати звіт", Content = "Підготувати звіт за місяць", Category = "Робота" },
        new Note { Id = 4, Title = "Прочитати книгу", Content = "Прочитати 2 глави про C#", Category = "Особисте" }
    };
}

#endregion