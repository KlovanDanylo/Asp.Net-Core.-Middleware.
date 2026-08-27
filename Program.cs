var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var books = new List<Book>
{
    new Book { Title = "Музична теорія", Category = "music" },
    new Book { Title = "Історія року", Category = "music" },
    new Book { Title = "Програмування на C#", Category = "it" }
};

app.Use(async (context, next) =>
{
    
    var timer = Stopwatch.StartNew();
    
   
    if (context.Request.ContentLength > 1048576)
    {
        context.Response.StatusCode = 413;
        await context.Response.WriteAsync("Помилка: Розмір даних перевищує 1 МБ!");
        return;
    }

    await next();

    timer.Stop();
    Console.WriteLine($"[LOG] {context.Request.Method} {context.Request.Path} - {timer.ElapsedMilliseconds} мс");
});


app.MapWhen(context => context.Request.Path.StartsWithSegments("/admin"), adminApp =>
{
    adminApp.Run(async context =>
    {
        Console.WriteLine("[LOG] Звернення до адмін-зони");
        await context.Response.WriteAsync("Ласкаво просимо до адмін-панелі");
    });
});

app.UseWhen(context => context.Request.Method == "POST", postApp =>
{
    postApp.Use(async (context, next) =>
    {
        Console.WriteLine("[LOG] Це POST-запит");
        await next();
        await context.Response.WriteAsync(" | POST оброблений");
    });
});



app.Use(async (context, next) =>
{
   
    if (context.Request.Path == "/getbooks")
    {
        string token = context.Request.Query["token"];
        string category = context.Request.Query["category"];

        if (token != "token12345")
        {
            context.Response.StatusCode = 401; 
            await context.Response.WriteAsync("Неавторизовано! Невірний або відсутній token.");
            return;
        }

        var filteredBooks = books.Where(b => string.IsNullOrEmpty(category) || b.Category.ToLower() == category.ToLower()).ToList();

        string html = "<html><body><h1>Список книг</h1><table border='1'><tr><th>Назва</th><th>Категорія</th></tr>";
        foreach (var book in filteredBooks)
        {
            html += $"<tr><td>{book.Title}</td><td>{book.Category}</td></tr>";
        }
        html += "</table></body></html>";

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(html);
        return; 
    }

    await next();
});

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/allbooks")
    {
        await next();
        return;
    }

    if (!context.Request.Headers.ContainsKey("X-Mood"))
    {
        Console.WriteLine("[LOG] Заголовок X-Mood відсутній");
        await context.Response.WriteAsync("Настрій не вказано");
        return;
    }

    string mood = context.Request.Headers["X-Mood"].ToString().ToLower();
    if (mood == "happy")
    {
        context.Items["MoodMessage"] = "Сьогодні чудовий день!";
    }
    else if (mood == "sad")
    {
        context.Items["MoodMessage"] = "Не сумуй, все налагодиться!";
    }
    else
    {
        context.Items["MoodMessage"] = "Настрій невизначений";
    }

    await next();
});

app.MapGet("/allbooks", () => books);

app.Run(async (context) =>
{
    if (context.Items.ContainsKey("MoodMessage"))
    {
        var result = new { message = context.Items["MoodMessage"] };
        await context.Response.WriteAsJsonAsync(result);
        return;
    }

    long size = context.Request.ContentLength ?? 0;
    await context.Response.WriteAsync($"Успішно оброблено. Розмір даних: {size} байт");
});

app.Run();

class Book
{
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
}
