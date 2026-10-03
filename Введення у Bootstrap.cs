using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

#region 1. Главная страница и Рецепты
app.MapGet("/", () => Results.Content(GetHtmlWrapper("Главная", @"
    <div class='row text-center mt-5'>
        <h2>Проектная работа ASP.NET Core</h2>
        <p class='lead'>Выберите нужный раздел в меню выше.</p>
        <div class='d-flex justify-content-center gap-3 mt-3'>
            <a href='/recipes/create' class='btn btn-primary btn-lg'>Рецепты</a>
            <a href='/feedback' class='btn btn-outline-primary btn-lg'>Обратная связь</a>
            <a href='/exchange' class='btn btn-outline-primary btn-lg'>Обменник валют</a>
            <a href='/books' class='btn btn-outline-primary btn-lg'>Книжный магазин</a>
        </div>
    </div>"), "text/html"));

// Добавление рецепта (Форма)
app.MapGet("/recipes/create", () =>
{
    string body = @"
        <h2>Создание рецепта</h2>
        <form method='post' action='/recipes/create'>
            <div class='mb-3'>
                <label class='form-label'>Название рецепта</label>
                <input type='text' name='Title' class='form-control' required />
            </div>
            <div class='mb-3'>
                <label class='form-label'>Описание</label>
                <textarea name='Description' class='form-control' rows='3'></textarea>
            </div>
            <h4>Ингредиенты</h4>
            <div id='ingredientsContainer'>
                <div class='row ingredient-row mb-2'>
                    <div class='col-md-4'><input name='Ingredients[0].Name' class='form-control' placeholder='Название' required /></div>
                    <div class='col-md-3'><input name='Ingredients[0].Amount' type='number' step='0.1' class='form-control' placeholder='Количество' required /></div>
                    <div class='col-md-3'><input name='Ingredients[0].Unit' class='form-control' placeholder='Ед. изм.' required /></div>
                    <div class='col-md-2'><button type='button' class='btn btn-danger remove-ingredient'>Remove</button></div>
                </div>
            </div>
            <button type='button' id='addBtn' class='btn btn-secondary mt-2 mb-3'>Add</button>
            <br />
            <button type='submit' class='btn btn-success'>Сохранить рецепт</button>
        </form>

        <script>
            let index = 1;
            document.getElementById('addBtn').addEventListener('click', function() {
                const container = document.getElementById('ingredientsContainer');
                const row = document.createElement('div');
                row.className = 'row ingredient-row mb-2';
                row.innerHTML = `
                    <div class='col-md-4'><input name='Ingredients[${index}].Name' class='form-control' placeholder='Название' required /></div>
                    <div class='col-md-3'><input name='Ingredients[${index}].Amount' type='number' step='0.1' class='form-control' placeholder='Количество' required /></div>
                    <div class='col-md-3'><input name='Ingredients[${index}].Unit' class='form-control' placeholder='Ед. изм.' required /></div>
                    <div class='col-md-2'><button type='button' class='btn btn-danger remove-ingredient'>Remove</button></div>
                `;
                container.appendChild(row);
                index++;
            });

            document.getElementById('ingredientsContainer').addEventListener('click', function(e) {
                if (e.target.classList.contains('remove-ingredient')) {
                    e.target.closest('.ingredient-row').remove();
                }
            });
        </script>";

    return Results.Content(GetHtmlWrapper("Создать рецепт", body), "text/html");
});

// Сохранение рецепта
app.MapPost("/recipes/create", ([FromForm] Recipe recipe) =>
{
    DataStore.Recipes.Add(recipe);
    
    string body = $@"
        <div class='alert alert-success'>
            <h4>Рецепт '{recipe.Title}' успешно сохранен!</h4>
            <p><strong>Описание:</strong> {recipe.Description}</p>
            <h5>Ингредиенты ({recipe.Ingredients?.Count ?? 0}):</h5>
            <ul>
                {string.Join("", recipe.Ingredients?.Select(i => $"<li>{i.Name} — {i.Amount} {i.Unit}</li>") ?? Array.Empty<string>())}
            </ul>
        </div>
        <a href='/recipes/create' class='btn btn-primary'>Добавить еще</a>";

    return Results.Content(GetHtmlWrapper("Рецепт сохранен", body), "text/html");
});
#endregion

#region 2. Обратная связь
app.MapGet("/feedback", () =>
{
    string body = @"
        <h2>Обратная связь</h2>
        <form method='post' action='/feedback'>
            <div class='mb-3'>
                <label class='form-label'>Ваш Email</label>
                <input type='email' name='UserEmail' class='form-control' required />
            </div>
            <div class='mb-3'>
                <label class='form-label'>Тема сообщения</label>
                <select name='Subject' class='form-select' required>
                    <option value=''>-- Выберите тему --</option>
                    <option value='Вопрос'>Вопрос по сайту</option>
                    <option value='Предложение'>Предложение по улучшению</option>
                    <option value='Жалоба'>Жалоба</option>
                </select>
            </div>
            <div class='mb-3'>
                <label class='form-label'>Сообщение</label>
                <textarea name='Message' class='form-control' rows='4' required></textarea>
            </div>
            <button type='submit' class='btn btn-primary'>Отправить сообщение</button>
        </form>";

    return Results.Content(GetHtmlWrapper("Обратная связь", body), "text/html");
});

app.MapPost("/feedback", ([FromForm] FeedbackModel feedback) =>
{
    string body = $@"
        <div class='alert alert-success'>
            <h4>Спасибо! Сообщение отправлено администратору.</h4>
            <p><strong>Email:</strong> {feedback.UserEmail}</p>
            <p><strong>Тема:</strong> {feedback.Subject}</p>
            <p><strong>Текст:</strong> {feedback.Message}</p>
        </div>
        <a href='/' class='btn btn-outline-primary'>На главную</a>";

    return Results.Content(GetHtmlWrapper("Сообщение отправлено", body), "text/html");
});
#endregion

#region 3. Обменник валют (AJAX)
app.MapGet("/exchange", () =>
{
    string body = @"
        <h2>Обменник валют</h2>
        <div class='card p-4'>
            <div class='row g-3'>
                <div class='col-md-3'>
                    <label class='form-label'>Продаю</label>
                    <select id='fromCurr' class='form-select'>
                        <option value='USD'>USD</option>
                        <option value='EUR'>EUR</option>
                        <option value='UAH'>UAH</option>
                    </select>
                </div>
                <div class='col-md-3'>
                    <label class='form-label'>Покупаю</label>
                    <select id='toCurr' class='form-select'>
                        <option value='UAH'>UAH</option>
                        <option value='USD'>USD</option>
                        <option value='EUR'>EUR</option>
                    </select>
                </div>
                <div class='col-md-3'>
                    <label class='form-label'>Сумма</label>
                    <input type='number' id='amount' class='form-control' value='100' />
                </div>
                <div class='col-md-3 d-flex align-items-end'>
                    <button id='calcBtn' class='btn btn-success w-100'>Рассчитать (AJAX)</button>
                </div>
            </div>
            <div class='mt-4 alert alert-info'>
                <h4>Результат: <span id='resultText'>0</span></h4>
            </div>
        </div>

        <script>
            document.getElementById('calcBtn').addEventListener('click', async function() {
                const data = {
                    from: document.getElementById('fromCurr').value,
                    to: document.getElementById('toCurr').value,
                    amount: parseFloat(document.getElementById('amount').value) || 0
                };

                const response = await fetch('/api/exchange', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(data)
                });
                
                const result = await response.json();
                document.getElementById('resultText').innerText = result.text;
            });
        </script>";

    return Results.Content(GetHtmlWrapper("Обмен валют", body), "text/html");
});

app.MapPost("/api/exchange", ([FromBody] ExchangeRequest req) =>
{
    decimal rate = 1.0m;
    if (req.From == "USD" && req.To == "UAH") rate = 41.5m;
    else if (req.From == "EUR" && req.To == "UAH") rate = 45.0m;
    else if (req.From == "UAH" && req.To == "USD") rate = 1 / 41.5m;
    else if (req.From == "UAH" && req.To == "EUR") rate = 1 / 45.0m;

    decimal total = Math.Round(req.Amount * rate, 2);
    return Results.Json(new { text = $"{total} {req.To}" });
});
#endregion

#region 4. Маленький интернет-магазин книг
app.MapGet("/books", () =>
{
    var booksCards = string.Join("", DataStore.Books.Select(b => $@"
        <div class='col-md-4 mb-3'>
            <div class='card h-100 shadow-sm'>
                <div class='card-body d-flex flex-column'>
                    <h5 class='card-title'>{b.Title}</h5>
                    <p class='card-text flex-grow-1'>{b.Description}</p>
                    <p class='fw-bold text-primary fs-5'>{b.Price} грн.</p>
                    <a href='/order?bookId={b.Id}' class='btn btn-primary w-100'>Купить</a>
                </div>
            </div>
        </div>"));

    string body = $@"
        <h2>Каталог книг</h2>
        <div class='row mt-3'>{booksCards}</div>";

    return Results.Content(GetHtmlWrapper("Книги", body), "text/html");
});

app.MapGet("/order", (int bookId) =>
{
    var book = DataStore.Books.FirstOrDefault(b => b.Id == bookId);
    if (book == null) return Results.NotFound("Книга не найдена");

    string body = $@"
        <h2>Оформление заказа</h2>
        <div class='card mb-3 bg-light p-3'>
            <h5>Товар: {book.Title}</h5>
            <p class='mb-0'>К оплате: <strong>{book.Price} грн.</strong></p>
        </div>
        <form method='post' action='/order'>
            <input type='hidden' name='BookId' value='{book.Id}' />
            <div class='mb-3'>
                <label class='form-label'>ФИО покупателя</label>
                <input type='text' name='FullName' class='form-control' required />
            </div>
            <div class='mb-3'>
                <label class='form-label'>Номер телефона</label>
                <input type='tel' name='Phone' class='form-control' placeholder='+380...' required />
            </div>
            <div class='mb-3'>
                <label class='form-label'>Город</label>
                <input type='text' name='City' class='form-control' required />
            </div>
            <div class='mb-3'>
                <label class='form-label'>Отделение Новой Почты</label>
                <input type='text' name='NovaPoshtaBranch' class='form-control' placeholder='№1' required />
            </div>
            <button type='submit' class='btn btn-success'>Подтвердить заказ</button>
        </form>";

    return Results.Content(GetHtmlWrapper("Оформление заказа", body), "text/html");
});

app.MapPost("/order", ([FromForm] OrderModel order) =>
{
    var book = DataStore.Books.FirstOrDefault(b => b.Id == order.BookId);

    string body = $@"
        <div class='alert alert-success text-center py-4'>
            <h3>Спасибо за заказ, {order.FullName}!</h3>
            <p class='fs-5 mt-3'>Вы заказали книгу: <strong>{book?.Title}</strong> ({book?.Price} грн.)</p>
            <hr />
            <p class='mb-1'>Детали доставки:</p>
            <p>г. {order.City}, Новая Почта {order.NovaPoshtaBranch}<br/>Телефон: {order.Phone}</p>
        </div>
        <div class='text-center'>
            <a href='/books' class='btn btn-primary'>Вернуться в магазин</a>
        </div>";

    return Results.Content(GetHtmlWrapper("Спасибо за заказ!", body), "text/html");
});
#endregion

app.Run();

#region HTML Шаблон-обертка
static string GetHtmlWrapper(string title, string bodyContent)
{
    return $@"<!DOCTYPE html>
    <html lang='ru'>
    <head>
        <meta charset='utf-8' />
        <meta name='viewport' content='width=device-width, initial-scale=1.0' />
        <title>{title}</title>
        <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css' rel='stylesheet' />
    </head>
    <body>
        <nav class='navbar navbar-expand-lg navbar-dark bg-dark mb-4'>
            <div class='container'>
                <a class='navbar-brand' href='/'>ASP.NET App</a>
                <div class='navbar-nav'>
                    <a class='nav-link' href='/recipes/create'>Рецепты</a>
                    <a class='nav-link' href='/feedback'>Обратная связь</a>
                    <a class='nav-link' href='/exchange'>Обменник</a>
                    <a class='nav-link' href='/books'>Книги</a>
                </div>
            </div>
        </nav>
        <div class='container'>
            {bodyContent}
        </div>
    </body>
    </html>";
}
#endregion

#region Модели данных и Хранилище
public class Ingredient
{
    public string Name { get; set; } = string.Empty;
    public double Amount { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public class Recipe
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<Ingredient> Ingredients { get; set; } = new();
}

public class FeedbackModel
{
    public string UserEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ExchangeRequest
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class OrderModel
{
    public int BookId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string NovaPoshtaBranch { get; set; } = string.Empty;
}

public static class DataStore
{
    public static List<Recipe> Recipes = new();
    
    public static List<Book> Books = new()
    {
        new Book { Id = 1, Title = "C# 10 и .NET 6", Price = 850, Description = "Подробный самоучитель по программированию." },
        new Book { Id = 2, Title = "ASP.NET Core MVC", Price = 920, Description = "Практическое руководство по созданию веб-сайтов." },
        new Book { Id = 3, Title = "Паттерны проектирования", Price = 650, Description = "Архитектура чистого кода и гибких систем." }
    };
}
#endregion