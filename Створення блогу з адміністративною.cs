using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

#region 1. Главная страница блога (Публикации + Пагинация)
app.MapGet("/", (int page = 1) =>
{
    int pageSize = 3;
    var posts = BlogStore.Posts;
    int totalItems = posts.Count;
    int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

    var pagedPosts = posts.Skip((page - 1) * pageSize).Take(pageSize).ToList();

    string postCardsHtml = string.Join("", pagedPosts.Select(p => $@"
        <div class='card mb-4 shadow-sm'>
            <div class='card-body'>
                <h3 class='card-title'>{p.Title}</h3>
                <p class='text-muted'><small>Опубліковано: {p.CreatedAt:dd.MM.yyyy}</small></p>
                <p class='card-text'>{p.Content}</p>
                <a href='/post/{p.Id}' class='btn btn-outline-primary'>Читати далі та коментарі ({p.Comments.Count})</a>
            </div>
        </div>"));

    string paginationHtml = string.Join(" ", Enumerable.Range(1, totalPages).Select(i => $@"
        <a href='/?page={i}' class='btn btn-sm {(i == page ? "btn-primary" : "btn-outline-primary")}'>{i}</a>"));

    string body = $@"
        <h1 class='mb-4'>Публікації блогу</h1>
        {postCardsHtml}
        <div class='d-flex gap-1 justify-content-center mt-4'>
            {paginationHtml}
        </div>";

    return Results.Content(GetHtmlWrapper("Головна", body), "text/html");
});
#endregion

#region 2. Детальная страница публикации + Компонент «Комментарии» (с Капчей)
app.MapGet("/post/{id:int}", (int id) =>
{
    var post = BlogStore.Posts.FirstOrDefault(p => p.Id == id);
    if (post == null) return Results.NotFound("Публікацію не знайдено");

    string commentsHtml = post.Comments.Any() 
        ? string.Join("", post.Comments.Select(c => $@"
            <div class='border-bottom py-2'>
                <strong>{c.AuthorName}</strong> <small class='text-muted'>({c.CreatedAt:dd.MM.yyyy HH:mm})</small>
                <p class='mb-1'>{c.Message}</p>
            </div>"))
        : "<p class='text-muted'>Будьте першим, хто залишить коментар!</p>";

    int captchaNum = Random.Shared.Next(1000, 9999);

    string body = $@"
        <a href='/' class='btn btn-secondary mb-3'>← На головну</a>
        <h2>{post.Title}</h2>
        <p class='text-muted'>Опубліковано: {post.CreatedAt:dd.MM.yyyy}</p>
        <div class='fs-5 mb-5'>{post.Content}</div>

        <hr/>
        <h3>Коментарі</h3>
        <div class='mb-4'>{commentsHtml}</div>

        <div class='card p-3 bg-light'>
            <h5>Залишити коментар</h5>
            <form method='post' action='/post/{post.Id}/comment'>
                <div class='mb-2'>
                    <label class='form-label'>Ваше ім'я</label>
                    <input name='name' class='form-control' required />
                </div>
                <div class='mb-2'>
                    <label class='form-label'>Повідомлення</label>
                    <textarea name='message' class='form-control' rows='3' required></textarea>
                </div>
                <div class='row mb-3'>
                    <div class='col-md-4'>
                        <label class='form-label'>Код капчі: <span class='badge bg-warning text-dark fs-6'>{captchaNum}</span></label>
                        <input type='hidden' name='captchaExpected' value='{captchaNum}' />
                        <input name='captchaInput' class='form-control' placeholder='Введіть код' required />
                    </div>
                </div>
                <button type='submit' class='btn btn-success'>Надіслати коментар</button>
            </form>
        </div>";

    return Results.Content(GetHtmlWrapper(post.Title, body), "text/html");
});

app.MapPost("/post/{id:int}/comment", ([FromRoute] int id, [FromForm] string name, [FromForm] string message, [FromForm] string captchaExpected, [FromForm] string captchaInput) =>
{
    var post = BlogStore.Posts.FirstOrDefault(p => p.Id == id);
    if (post == null) return Results.NotFound();

    if (captchaInput != captchaExpected)
    {
        return Results.Content(GetHtmlWrapper("Помилка капчі", $@"
            <div class='alert alert-danger'>Невірно введено код капчі!</div>
            <a href='/post/{id}' class='btn btn-secondary'>Повернутися</a>"), "text/html");
    }

    post.Comments.Add(new Comment
    {
        AuthorName = name,
        Message = message,
        CreatedAt = DateTime.Now
    });

    return Results.Redirect($"/post/{id}");
});
#endregion

#region 3. Список пользователей (Поиск + Пагинация)
app.MapGet("/users", (string? search, int page = 1) =>
{
    int pageSize = 3;
    var query = BlogStore.Users.AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(u => u.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || u.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    int totalItems = query.Count();
    int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
    var pagedUsers = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

    string rowsHtml = string.Join("", pagedUsers.Select(u => $@"
        <tr>
            <td>{u.Id}</td>
            <td>{u.Name}</td>
            <td>{u.Email}</td>
            <td><span class='badge bg-info text-dark'>{u.Role}</span></td>
        </tr>"));

    string paginationHtml = string.Join(" ", Enumerable.Range(1, totalPages).Select(i => $@"
        <a href='/users?search={search}&page={i}' class='btn btn-sm {(i == page ? "btn-primary" : "btn-outline-primary")}'>{i}</a>"));

    string body = $@"
        <h2>Список користувачів</h2>
        
        <form method='get' action='/users' class='row g-2 mb-3'>
            <div class='col-md-8'>
                <input name='search' value='{search}' class='form-control' placeholder='Пошук за ім'ям або email...' />
            </div>
            <div class='col-md-4'>
                <button type='submit' class='btn btn-primary w-100'>Шукати</button>
            </div>
        </form>

        <table class='table table-bordered table-striped'>
            <thead>
                <tr><th>ID</th><th>Ім'я</th><th>Email</th><th>Роль</th></tr>
            </thead>
            <tbody>
                {(pagedUsers.Any() ? rowsHtml : "<tr><td colspan='4' class='text-center'>Користувачів не знайдено</td></tr>")}
            </tbody>
        </table>

        <div class='d-flex gap-1 justify-content-center mt-3'>
            {paginationHtml}
        </div>";

    return Results.Content(GetHtmlWrapper("Користувачі", body), "text/html");
});
#endregion

#region 4. Керування підписками + HTML Редактор (Froala)
app.MapGet("/subscribers", () =>
{
    var subs = BlogStore.Subscribers;

    string listHtml = string.Join("", subs.Select(s => $@"
        <div class='d-flex justify-content-between align-items-center border-bottom py-2'>
            <div>
                <input type='checkbox' name='selectedEmails' value='{s.Email}' class='form-check-input me-2 sub-checkbox' checked />
                <span>{s.Email}</span>
            </div>
            <form method='post' action='/subscribers/delete' style='margin:0;'>
                <input type='hidden' name='email' value='{s.Email}' />
                <button type='submit' class='btn btn-sm btn-outline-danger'>Видалити</button>
            </form>
        </div>"));

    string body = $@"
        <h2>Керування підписниками та розсилка</h2>

        <div class='row mt-4'>
            <div class='col-md-5'>
                <div class='card p-3 shadow-sm mb-4'>
                    <div class='d-flex justify-content-between align-items-center mb-2'>
                        <h5>Передплатники</h5>
                        <button type='button' class='btn btn-sm btn-outline-secondary' onclick='toggleAll(this)'>Зняти всі</button>
                    </div>
                    {(subs.Any() ? listHtml : "<p class='text-muted'>Список підписників порожній</p>")}
                </div>
            </div>

            <div class='col-md-7'>
                <div class='card p-3 shadow-sm'>
                    <h5>Надіслати лист (Froala HTML Editor)</h5>
                    <form method='post' action='/subscribers/send'>
                        <div id='selectedEmailsContainer'></div>
                        <div class='mb-3'>
                            <label class='form-label'>Тема листа</label>
                            <input name='subject' class='form-control' required />
                        </div>
                        <div class='mb-3'>
                            <label class='form-label'>Тіло листа (HTML)</label>
                            <textarea id='froala-editor' name='htmlBody'></textarea>
                        </div>
                        <button type='submit' class='btn btn-success w-100'>Надіслати вибраним</button>
                    </form>
                </div>
            </div>
        </div>

        <script>
            function toggleAll(btn) {{
                const checkboxes = document.querySelectorAll('.sub-checkbox');
                const isChecked = checkboxes[0]?.checked;
                checkboxes.forEach(c => c.checked = !isChecked);
                btn.innerText = isChecked ? 'Обрати всі' : 'Зняти всі';
            }}
        </script>";

    return Results.Content(GetHtmlWrapper("Підписники", body, includeFroala: true), "text/html");
});

app.MapPost("/subscribers/delete", ([FromForm] string email) =>
{
    var sub = BlogStore.Subscribers.FirstOrDefault(s => s.Email == email);
    if (sub != null) BlogStore.Subscribers.Remove(sub);

    return Results.Redirect("/subscribers");
});

app.MapPost("/subscribers/send", ([FromForm] string[] selectedEmails, [FromForm] string subject, [FromForm] string htmlBody) =>
{
    string body = $@"
        <div class='alert alert-success'>
            <h4>Лист успішно надіслано!</h4>
            <p><strong>Тема:</strong> {subject}</p>
            <p><strong>Отримувачі:</strong> {string.Join(", ", selectedEmails)}</p>
            <hr/>
            <h6>Вміст листа:</h6>
            <div class='border p-3 bg-white rounded'>{htmlBody}</div>
        </div>
        <a href='/subscribers' class='btn btn-primary'>Назад</a>";

    return Results.Content(GetHtmlWrapper("Розсилка", body), "text/html");
});
#endregion

app.Run();

#region HTML Шаблон-обертка
static string GetHtmlWrapper(string title, string bodyContent, bool includeFroala = false)
{
    string froalaAssets = includeFroala ? @"
        <link href='https://cdn.jsdelivr.net/npm/froala-editor@latest/css/froala_editor.pkgd.min.css' rel='stylesheet' type='text/css' />
        <script type='text/javascript' src='https://cdn.jsdelivr.net/npm/froala-editor@latest/js/froala_editor.pkgd.min.js'></script>
    " : "";

    string froalaInit = includeFroala ? @"
        <script>
            new FroalaEditor('#froala-editor');
        </script>
    " : "";

    return $@"<!DOCTYPE html>
    <html lang='uk'>
    <head>
        <meta charset='utf-8' />
        <meta name='viewport' content='width=device-width, initial-scale=1.0' />
        <title>{title}</title>
        <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css' rel='stylesheet' />
        {froalaAssets}
    </head>
    <body class='bg-light'>
        <nav class='navbar navbar-expand-lg navbar-dark bg-dark mb-4'>
            <div class='container'>
                <a class='navbar-brand fw-bold' href='/'>Блог Додаток</a>
                <div class='navbar-nav'>
                    <a class='nav-link' href='/'>Публікації</a>
                    <a class='nav-link' href='/users'>Користувачі</a>
                    <a class='nav-link' href='/subscribers'>Розсилка</a>
                </div>
            </div>
        </nav>
        <div class='container pb-5'>
            {bodyContent}
        </div>
        {froalaInit}
    </body>
    </html>";
}
#endregion

#region Хранилище данных и Модели
public class Post
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<Comment> Comments { get; set; } = new();
}

public class Comment
{
    public string AuthorName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class Subscriber
{
    public string Email { get; set; } = string.Empty;
}

public static class BlogStore
{
    public static List<Post> Posts = new()
    {
        new Post { Id = 1, Title = "Перша стаття про ASP.NET Core", Content = "Ласкаво просимо до нашого новітнього блогу! Тут ми ділимося знаннями про C# та ASP.NET.", CreatedAt = DateTime.Now.AddDays(-5) },
        new Post { Id = 2, Title = "Робота з Froala Editor", Content = "Froala — це чудовий WYSIWYG редактор, який легко інтегрується у веб-додатки.", CreatedAt = DateTime.Now.AddDays(-3) },
        new Post { Id = 3, Title = "Пагінація та Пошук", Content = "Правильна пагінація покращує продуктивність сайту та зручність користувачів.", CreatedAt = DateTime.Now.AddDays(-2) },
        new Post { Id = 4, Title = "Безпека та Капча", Content = "Використання капчі допомагає захистити вашу форму коментарів від спам-ботів.", CreatedAt = DateTime.Now.AddDays(-1) }
    };

    public static List<User> Users = new()
    {
        new User { Id = 1, Name = "Олександр", Email = "alex@test.com", Role = "Admin" },
        new User { Id = 2, Name = "Марія", Email = "maria@test.com", Role = "User" },
        new User { Id = 3, Name = "Іван", Email = "ivan@test.com", Role = "User" },
        new User { Id = 4, Name = "Олена", Email = "elena@test.com", Role = "Editor" }
    };

    public static List<Subscriber> Subscribers = new()
    {
        new Subscriber { Email = "user1@gmail.com" },
        new Subscriber { Email = "user2@yahoo.com" },
        new Subscriber { Email = "dev@outlook.com" }
    };
}
#endregion