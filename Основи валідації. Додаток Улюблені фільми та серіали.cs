using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

#region 1. Главная страница и Навигация
app.MapGet("/", () =>
{
    string body = @"
        <div class='text-center mt-4'>
            <h2>Проектна робота: Валідація та TvShows</h2>
            <p class='lead'>Оберіть розділ для перегляду:</p>
            <div class='d-flex justify-content-center gap-3 flex-wrap mt-3'>
                <a href='/Account/Register' class='btn btn-primary btn-lg'>1. Форма реєстрації (Атрибути валідації)</a>
                <a href='/TvShows' class='btn btn-outline-primary btn-lg'>2. TV Shows (Пошук, Сортування, Пагінація)</a>
            </div>
        </div>";

    return Results.Content(GetHtmlWrapper("Головна", body), "text/html");
});
#endregion

#region 2. Контроллер Account & Remote Validation

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

#endregion

app.Run();

#region 3. Кастомный атрибут валидации кредитной карты
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class CustomCreditCardAttribute : ValidationAttribute, IClientModelValidator
{
    private const string CardRegex = @"^(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|6(?:011|5[0-9][0-9])[0-9]{12}|3[47][0-9]{13})$";

    public CustomCreditCardAttribute() 
        : base("Недійсний номер кредитної картки (підтримуються Visa, Mastercard, Discover, Amex).")
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success; // Совместимость с [Required]
        }

        string rawCardNumber = value.ToString()!.Replace(" ", "").Replace("-", "");

        if (Regex.IsMatch(rawCardNumber, CardRegex))
        {
            return ValidationResult.Success;
        }

        return new ValidationResult(ErrorMessage);
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-customcreditcard", ErrorMessage ?? "Недійсний номер картки");
    }

    private static bool MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (attributes.ContainsKey(key)) return false;
        attributes.Add(key, value);
        return true;
    }
}
#endregion

#region 4. Модель RegisterViewModel
public class RegisterViewModel
{
    [Required(ErrorMessage = "Ім'я є обов'язковим")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Прізвище є обов'язковим")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Username є обов'язковим")]
    [StringLength(20, ErrorMessage = "Username не повинен перевищувати 20 символів")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username може містити лише літери, цифри та підкреслення")]
    [Remote(action: "CheckUsername", controller: "Account", ErrorMessage = "Це ім'я користувача вже зайняте")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email є обов'язковим")]
    [EmailAddress(ErrorMessage = "Некоректний формат Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Пароль є обов'язковим")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Довжина пароля повинна бути від 6 до 100 символів")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Підтвердження пароля є обов'язковим")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Паролі не співпадають")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Range(18, 100, ErrorMessage = "Вік повинен бути від 18 до 100 років")]
    public int Age { get; set; }

    [Phone(ErrorMessage = "Некоректний номер телефону")]
    public string? PhoneNumber { get; set; }

    [Url(ErrorMessage = "Некоректний формат URL-адреси")]
    public string? Website { get; set; }

    [CreditCard(ErrorMessage = "Некоректний номер кредитної картки")]
    [CustomCreditCard]
    public string? CreditCardNumber { get; set; }

    [ValidateNever]
    public bool TermsOfService { get; set; }
}
#endregion

#region 5. Контроллеры AccountController и TvShowsController

public class AccountController : Controller
{
    private static readonly List<string> ExistingUsers = new() { "admin", "john_doe", "user123" };

    [HttpGet]
    public IActionResult Register()
    {
        string html = @"
        <h2>Реєстрація користувача</h2>
        <form method='post' action='/Account/Register' class='mt-3'>
            <div class='row g-3'>
                <div class='col-md-6'><label>Ім'я (*)</label><input name='FirstName' class='form-control' required /></div>
                <div class='col-md-6'><label>Прізвище (*)</label><input name='LastName' class='form-control' required /></div>
                <div class='col-md-6'><label>Username (*, max 20, [a-zA-Z0-9_])</label><input name='Username' class='form-control' required /></div>
                <div class='col-md-6'><label>Email (*)</label><input type='email' name='Email' class='form-control' required /></div>
                <div class='col-md-6'><label>Пароль (*)</label><input type='password' name='Password' class='form-control' required /></div>
                <div class='col-md-6'><label>Підтвердження пароля (*)</label><input type='password' name='ConfirmPassword' class='form-control' required /></div>
                <div class='col-md-4'><label>Вік (18-100)</label><input type='number' name='Age' value='18' class='form-control' /></div>
                <div class='col-md-4'><label>Телефон</label><input name='PhoneNumber' class='form-control' /></div>
                <div class='col-md-4'><label>Веб-сайт</label><input name='Website' class='form-control' /></div>
                <div class='col-md-12'><label>Кредитна картка (Custom CreditCard Validation)</label><input name='CreditCardNumber' class='form-control' placeholder='4111-1111-1111-1111' /></div>
                <div class='col-md-12 form-check ms-3'>
                    <input type='checkbox' name='TermsOfService' value='true' class='form-check-input' id='terms' />
                    <label class='form-check-label' for='terms'>Погоджуюся з умовами (ValidateNever)</label>
                </div>
            </div>
            <button type='submit' class='btn btn-success mt-4'>Зареєструватися</button>
        </form>";

        return Content(GetHtmlWrapper("Реєстрація", html), "text/html");
    }

    [HttpPost]
    public IActionResult Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join("<br/>", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));

            string errorBody = $@"
                <div class='alert alert-danger'>
                    <h4>Помилка валідації:</h4>
                    <p>{errors}</p>
                </div>
                <a href='/Account/Register' class='btn btn-secondary'>Назад</a>";

            return Content(GetHtmlWrapper("Помилка реєстрації", errorBody), "text/html");
        }

        // Имитация сохранения в базу данных
        ExistingUsers.Add(model.Username);

        string successBody = $@"
            <div class='alert alert-success'>
                <h4>Користувача {model.Username} успішно збережено в базі даних!</h4>
                <p>Email: {model.Email}</p>
            </div>
            <a href='/' class='btn btn-primary'>На головну</a>";

        return Content(GetHtmlWrapper("Успіх", successBody), "text/html");
    }

    // Remote validation endpoint
    [AcceptVerbs("GET", "POST")]
    public IActionResult CheckUsername(string username)
    {
        bool exists = ExistingUsers.Contains(username, StringComparer.OrdinalIgnoreCase);
        return Json(!exists);
    }
}

public class TvShowsController : Controller
{
    public IActionResult Index(string? search, string? genre, string? sortBy, int page = 1)
    {
        int pageSize = 3;
        var shows = TvStore.Shows.AsQueryable();

        // Поиск
        if (!string.IsNullOrWhiteSpace(search))
        {
            shows = shows.Where(s => s.Title.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        // Фильтр по жанру
        if (!string.IsNullOrWhiteSpace(genre))
        {
            shows = shows.Where(s => s.Genre.Equals(genre, StringComparison.OrdinalIgnoreCase));
        }

        // Сортировка
        shows = sortBy switch
        {
            "rating_desc" => shows.OrderByDescending(s => s.Rating),
            "title_asc" => shows.OrderBy(s => s.Title),
            _ => shows.OrderBy(s => s.Id)
        };

        int totalItems = shows.Count();
        var pagedShows = shows.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

        // Генерация компонента статистики
        var genresStats = TvStore.Shows
            .GroupBy(s => s.Genre)
            .Select(g => $"<a href='/TvShows?genre={g.Key}' class='badge bg-info text-dark me-2 text-decoration-none'>{g.Key} ({g.Count()})</a>");

        string statsComponentHtml = $@"
            <div class='card p-3 mb-4 bg-light shadow-sm'>
                <h5>StatisticsViewComponent: Жанри фільмів</h5>
                <div>{string.Join(" ", genresStats)}</div>
            </div>";

        string rowsHtml = string.Join("", pagedShows.Select(s => $@"
            <tr>
                <td>{s.Title}</td>
                <td><a href='/TvShows?genre={s.Genre}'>{s.Genre}</a></td>
                <td>⭐ {s.Rating}</td>
            </tr>"));

        string paginationHtml = string.Join(" ", Enumerable.Range(1, totalPages).Select(p => $@"
            <a href='/TvShows?search={search}&genre={genre}&sortBy={sortBy}&page={p}' class='btn btn-sm {(p == page ? "btn-primary" : "btn-outline-primary")}'>{p}</a>"));

        string body = $@"
            <h2>Серіали та Фільми (TvShows)</h2>
            {statsComponentHtml}

            <form method='get' action='/TvShows' class='row g-2 mb-3'>
                <div class='col-md-4'>
                    <input name='search' value='{search}' class='form-control' placeholder='Пошук за назвою...' />
                </div>
                <div class='col-md-3'>
                    <select name='sortBy' class='form-select'>
                        <option value=''>Сортування за замовчуванням</option>
                        <option value='rating_desc' {(sortBy == "rating_desc" ? "selected" : "")}>За рейтингом (високий -> низький)</option>
                        <option value='title_asc' {(sortBy == "title_asc" ? "selected" : "")}>За назвою (А-Я)</option>
                    </select>
                </div>
                <div class='col-md-2'>
                    <button type='submit' class='btn btn-primary w-100'>Застосувати</button>
                </div>
            </form>

            <table class='table table-bordered table-striped mt-3'>
                <thead>
                    <tr>
                        <th>Назва</th>
                        <th>Жанр</th>
                        <th>Рейтинг</th>
                    </tr>
                </thead>
                <tbody>
                    {(pagedShows.Any() ? rowsHtml : "<tr><td colspan='3' class='text-center'>Нічого не знайдено</td></tr>")}
                </tbody>
            </table>

            <div class='d-flex gap-1 mt-3'>
                <span>Сторінки: </span> {paginationHtml}
            </div>";

        return Content(GetHtmlWrapper("TvShows", body), "text/html");
    }
}
#endregion

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
    </head>
    <body class='bg-light'>
        <nav class='navbar navbar-expand-lg navbar-dark bg-dark mb-4'>
            <div class='container'>
                <a class='navbar-brand fw-bold' href='/'>ASP.NET WebApp</a>
                <div class='navbar-nav'>
                    <a class='nav-link' href='/Account/Register'>Реєстрація</a>
                    <a class='nav-link' href='/TvShows'>TvShows</a>
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

#region Модели данных и Хранилище TvShows
public class TvShow
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public double Rating { get; set; }
}

public static class TvStore
{
    public static List<TvShow> Shows = new()
    {
        new TvShow { Id = 1, Title = "Breaking Bad", Genre = "Drama", Rating = 9.5 },
        new TvShow { Id = 2, Title = "Stranger Things", Genre = "Sci-Fi", Rating = 8.7 },
        new TvShow { Id = 3, Title = "Dark", Genre = "Sci-Fi", Rating = 8.8 },
        new TvShow { Id = 4, Title = "The Office", Genre = "Comedy", Rating = 9.0 },
        new TvShow { Id = 5, Title = "Friends", Genre = "Comedy", Rating = 8.9 },
        new TvShow { Id = 6, Title = "Game of Thrones", Genre = "Fantasy", Rating = 9.2 },
        new TvShow { Id = 7, Title = "The Witcher", Genre = "Fantasy", Rating = 8.2 }
    };
}
#endregion