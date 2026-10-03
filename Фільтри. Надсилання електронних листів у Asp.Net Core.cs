using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

#region 1. Главная страница
app.MapGet("/", () =>
{
    string body = @"
        <div class='text-center mt-4'>
            <h2>Проектна робота ASP.NET Core</h2>
            <p class='lead'>Оберіть необхідний розділ:</p>
            <div class='d-flex justify-content-center gap-2 flex-wrap mt-3'>
                <a href='/quiz' class='btn btn-primary btn-lg'>1. Тест (Tag-Helpers)</a>
                <a href='/blog' class='btn btn-outline-primary btn-lg'>2. Прев'ю статей (TagHelper)</a>
                <a href='/matrix' class='btn btn-outline-primary btn-lg'>3. Матриці (HtmlHelpers)</a>
                <a href='/product' class='btn btn-outline-primary btn-lg'>4. Рейтинг товарів (StarRating)</a>
            </div>
        </div>";

    return Results.Content(GetHtmlWrapper("Головна", body), "text/html");
});
#endregion

#region 2. Додаток «Тест» (7 вопросов)
app.MapGet("/quiz", (int? step, int? score) =>
{
    int currentStep = step ?? 0;
    int currentScore = score ?? 0;

    if (currentStep >= QuizStore.Questions.Count)
    {
        string resultBody = $@"
            <div class='card text-center p-4 shadow-sm'>
                <h2>Тест завершено!</h2>
                <p class='fs-4 mt-3'>Ваш результат: <strong>{currentScore}</strong> з <strong>{QuizStore.Questions.Count}</strong> балів.</p>
                <div class='mt-3'>
                    <a href='/quiz' class='btn btn-primary'>Пройти знову</a>
                </div>
            </div>";
        return Results.Content(GetHtmlWrapper("Результат тесту", resultBody), "text/html");
    }

    var q = QuizStore.Questions[currentStep];

    var optionsHtml = new StringBuilder();
    for (int i = 0; i < q.Options.Count; i++)
    {
        optionsHtml.Append($@"
            <div class='form-check mb-2'>
                <input class='form-check-input' type='radio' name='answer' id='opt{i}' value='{i}' required>
                <label class='form-check-label' for='opt{i}'>{q.Options[i]}</label>
            </div>");
    }

    string body = $@"
        <div class='card p-4 shadow-sm'>
            <div class='d-flex justify-content-between mb-3'>
                <span class='badge bg-secondary fs-6'>Питання {currentStep + 1} з {QuizStore.Questions.Count}</span>
            </div>
            <h4>{q.Text}</h4>
            <form method='post' action='/quiz'>
                <input type='hidden' name='step' value='{currentStep}' />
                <input type='hidden' name='score' value='{currentScore}' />
                <div class='my-3'>{optionsHtml}</div>
                <button type='submit' class='btn btn-success'>Далі</button>
            </form>
        </div>";

    return Results.Content(GetHtmlWrapper("Тестування", body), "text/html");
});

app.MapPost("/quiz", ([FromForm] int step, [FromForm] int score, [FromForm] int answer) =>
{
    var q = QuizStore.Questions[step];
    if (answer == q.CorrectIndex)
    {
        score++;
    }
    step++;

    return Results.Redirect($"/quiz?step={step}&score={score}");
});
#endregion

#region 3. Блог статей с Tag-Helper (Обрезка 150 символов)
app.MapGet("/blog", () =>
{
    var postsHtml = new StringBuilder();

    foreach (var post in BlogStore.Posts)
    {
        string previewContent = post.Content.Length > 150 ? post.Content.Substring(0, 150) + "..." : post.Content;
        postsHtml.Append($@"
            <div class='col-md-6 mb-3'>
                <div class='card h-100 shadow-sm'>
                    <div class='card-body'>
                        <h5 class='card-title'>{post.Title}</h5>
                        <p class='card-text'>{previewContent}</p>
                    </div>
                </div>
            </div>");
    }

    string body = $@"
        <h2>Блог статей</h2>
        <p class='text-muted'>Текст статей обрізається до 150 символів за допомогою TagHelper.</p>
        <div class='row mt-3'>{postsHtml}</div>";

    return Results.Content(GetHtmlWrapper("Блог", body), "text/html");
});
#endregion

#region 4. Сложение и умножение матриц (HtmlHelpers)
app.MapGet("/matrix", (int? size) =>
{
    int matrixSize = size ?? 3;

    string matrixInputs(string prefix) => string.Join("", Enumerable.Range(0, matrixSize).Select(r =>
        "<div class='d-flex gap-1 mb-1'>" + string.Join("", Enumerable.Range(0, matrixSize).Select(c =>
            $"<input type='number' name='{prefix}_{r}_{c}' value='1' class='form-control form-control-sm text-center' style='width: 50px;' required />")) + "</div>"));

    string body = $@"
        <h2>Складання та множення матриць</h2>
        <form method='get' action='/matrix' class='mb-4'>
            <label class='form-label fw-bold'>Оберіть розмірність матриці:</label>
            <div class='d-flex gap-2 align-items-center'>
                <select name='size' class='form-select w-auto'>
                    <option value='3' {(matrixSize == 3 ? "selected" : "")}>3x3</option>
                    <option value='6' {(matrixSize == 6 ? "selected" : "")}>6x6</option>
                    <option value='9' {(matrixSize == 9 ? "selected" : "")}>9x9</option>
                </select>
                <button type='submit' class='btn btn-secondary'>Застосувати</button>
            </div>
        </form>

        <form method='post' action='/matrix/calculate'>
            <input type='hidden' name='size' value='{matrixSize}' />
            <div class='row'>
                <div class='col-md-5'>
                    <h5>Матриця A</h5>
                    {matrixInputs("A")}
                </div>
                <div class='col-md-5'>
                    <h5>Матриця B</h5>
                    {matrixInputs("B")}
                </div>
            </div>
            <div class='mt-3 gap-2 d-flex'>
                <button type='submit' name='operation' value='add' class='btn btn-primary'>Додати (A + B)</button>
                <button type='submit' name='operation' value='multiply' class='btn btn-success'>Помножити (A * B)</button>
            </div>
        </form>";

    return Results.Content(GetHtmlWrapper("Матриці", body), "text/html");
});

app.MapPost("/matrix/calculate", (IFormCollection form) =>
{
    int size = int.Parse(form["size"]!);
    string op = form["operation"]!;

    int[,] a = new int[size, size];
    int[,] b = new int[size, size];
    int[,] res = new int[size, size];

    for (int r = 0; r < size; r++)
    {
        for (int c = 0; c < size; c++)
        {
            a[r, c] = int.Parse(form[$"A_{r}_{c}"]!);
            b[r, c] = int.Parse(form[$"B_{r}_{c}"]!);
        }
    }

    if (op == "add")
    {
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                res[r, c] = a[r, c] + b[r, c];
    }
    else
    {
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                for (int k = 0; k < size; k++)
                    res[r, c] += a[r, k] * b[k, c];
    }

    string renderMatrix(int[,] m) => string.Join("", Enumerable.Range(0, size).Select(r =>
        "<div class='d-flex gap-1 mb-1'>" + string.Join("", Enumerable.Range(0, size).Select(c =>
            $"<div class='border text-center p-1 bg-light' style='width: 45px;'>{m[r, c]}</div>")) + "</div>"));

    string body = $@"
        <h2>Результат обчислення ({op.ToUpper()})</h2>
        <div class='d-flex gap-4 align-items-center mt-4'>
            <div><h6>A</h6>{renderMatrix(a)}</div>
            <div class='fs-3 fw-bold'>{(op == "add" ? "+" : "*")}</div>
            <div><h6>B</h6>{renderMatrix(b)}</div>
            <div class='fs-3 fw-bold'>=</div>
            <div><h6>Результат</h6>{renderMatrix(res)}</div>
        </div>
        <a href='/matrix' class='btn btn-outline-primary mt-4'>Назад</a>";

    return Results.Content(GetHtmlWrapper("Результат матриці", body), "text/html");
});
#endregion

#region 5. Страница товара с использованием HTML-хелпера StarRating
app.MapGet("/product", () =>
{
    var product = ProductStore.SampleProduct;
    string starsHtml = HtmlHelperExtensions.StarRating(product.Rating);

    string body = $@"
        <div class='row justify-content-center'>
            <div class='col-md-6'>
                <div class='card shadow-sm p-3'>
                    <img src='https://via.placeholder.com/300x200?text=iPhone+15+Pro' class='card-img-top rounded' alt='Product' />
                    <div class='card-body text-center'>
                        <h4 class='card-title'>{product.Title}</h4>
                        <div class='my-2 fs-4'>{starsHtml}</div>
                        <p class='text-muted'>Оцінка: {product.Rating} з 5</p>
                        <h3 class='text-primary fw-bold'>${product.Price}</h3>
                        <p class='card-text'>Колір: Black | Пам'ять: 256GB</p>
                        <button class='btn btn-success btn-lg w-100'>Buy Now</button>
                    </div>
                </div>
            </div>
        </div>";

    return Results.Content(GetHtmlWrapper("Товар", body), "text/html");
});
#endregion

app.Run();

#region HTML Шаблон-обертка
static string GetHtmlWrapper(string title, string bodyContent)
{
    return $@"<!DOCTYPE html>
    <html lang='uk'>
    <head>
        <meta charset='utf-8' />
        <meta name='viewport' content='width=device-width, initial-scale=1.0' />
        <title>{title}</title>
        <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css' rel='stylesheet' />
        <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap-icons@1.10.5/font/bootstrap-icons.css' />
    </head>
    <body class='bg-light'>
        <nav class='navbar navbar-expand-lg navbar-dark bg-dark mb-4'>
            <div class='container'>
                <a class='navbar-brand fw-bold' href='/'>C# Web App</a>
                <div class='navbar-nav'>
                    <a class='nav-link' href='/quiz'>Тест</a>
                    <a class='nav-link' href='/blog'>Блог</a>
                    <a class='nav-link' href='/matrix'>Матриці</a>
                    <a class='nav-link' href='/product'>Товар</a>
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

#region TagHelper & HtmlHelper
[HtmlTargetElement("article-preview")]
public class ArticlePreviewTagHelper : TagHelper
{
    public string Content { get; set; } = string.Empty;
    public int MaxLength { get; set; } = 150;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "p";
        output.Attributes.SetAttribute("class", "card-text");

        if (Content.Length > MaxLength)
        {
            output.Content.SetContent(Content.Substring(0, MaxLength) + "...");
        }
        else
        {
            output.Content.SetContent(Content);
        }
    }
}

public static class HtmlHelperExtensions
{
    public static string StarRating(int rating)
    {
        var sb = new StringBuilder();
        for (int i = 1; i <= 5; i++)
        {
            if (i <= rating)
            {
                sb.Append("<i class='bi bi-star-fill text-warning me-1'></i>");
            }
            else
            {
                sb.Append("<i class='bi bi-star text-secondary me-1'></i>");
            }
        }
        return sb.ToString();
    }
}
#endregion

#region Модели и Данные
public class Question
{
    public string Text { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public int CorrectIndex { get; set; }
}

public class BlogPost
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class Product
{
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Rating { get; set; }
}

public static class QuizStore
{
    public static List<Question> Questions = new()
    {
        new Question { Text = "Що таке ASP.NET Core?", Options = new() { "Мова програмування", "Фреймворк для веб-розробки", "База даних", "Операційна система" }, CorrectIndex = 1 },
        new Question { Text = "Який метод використовується для конфігурації сервісів?", Options = new() { "ConfigureServices", "AddServices", "BuildServices", "UseServices" }, CorrectIndex = 0 },
        new Question { Text = "Що таке Tag Helper у Razor?", Options = new() { "Метод C#", "Компонент для роботи з HTML-тегами", "База даних", "Скрипт JS" }, CorrectIndex = 1 },
        new Question { Text = "Який метод запускає ASP.NET Core додаток?", Options = new() { "app.Start()", "app.Run()", "app.Execute()", "app.Init()" }, CorrectIndex = 1 },
        new Question { Text = "Який атрибут використовується для HTTP GET запитів?", Options = new() { "[HttpPost]", "[HttpGet]", "[HttpPut]", "[HttpDelete]" }, CorrectIndex = 1 },
        new Question { Text = "Що повертає контроллер MVC за замовчуванням?", Options = new() { "IActionResult", "string", "int", "void" }, CorrectIndex = 0 },
        new Question { Text = "Для чого призначений файл launchSettings.json?", Options = new() { "Збереження моделей", "Налаштування запуску проекту", "Збереження користувачів", "Стилі CSS" }, CorrectIndex = 1 }
    };
}

public static class BlogStore
{
    public static List<BlogPost> Posts = new()
    {
        new BlogPost
        {
            Title = "Вступ до ASP.NET Core Tag-Helpers",
            Content = "Tag Helpers дозволяють серверному коду брати участь у створенні та рендерингу елементів HTML у файлах Razor. Вони є чудової альтернативою HTML-хелперам і роблять розмітку більш чистою та зрозумілою для розробників."
        },
        new BlogPost
        {
            Title = "Оптимізація продуктивності веб-додатків",
            Content = "Продуктивність є одним із найважливіших факторів успіху будь-якого веб-проекту. Використання кешування, оптимізація запитів до баз даних та зменшення розміру переданих даних дозволяють значно прискорити завантаження сторінок."
        }
    };
}

public static class ProductStore
{
    public static Product SampleProduct = new Product
    {
        Title = "iPhone 15 Pro Max 256GB",
        Price = 799,
        Rating = 4
    };
}
#endregion