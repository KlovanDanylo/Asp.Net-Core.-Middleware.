using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=app.db"));

builder.Services.AddSingleton<CarService>();

builder.Services.AddMemoryCache();
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(p => p.VaryByHeader("User-Agent"));
});
builder.Services.AddScoped<ArticleService>();
builder.Services.AddScoped<UserRatingService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    if (!db.Articles.Any())
    {
        db.Articles.AddRange(
            new Article { Title = "Введение в Razor Pages", Views = 1500 },
            new Article { Title = "Оптимизация и Кеширование в .NET", Views = 3200 }
        );
        db.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseOutputCache();

app.MapRazorPages();

app.Run();

public class TodoItem
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Введите название задачи")]
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Subject
{
    public string Name { get; set; } = string.Empty;
    public int Hours { get; set; }
}

public class Car
{
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
}

public class Article
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Views { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
    public DbSet<Article> Articles => Set<Article>();
}


public class CarService
{
    private readonly List<Car> _cars = new();
    public IReadOnlyList<Car> GetCars() => _cars.AsReadOnly();
    public void AddCars(IEnumerable<Car> cars) => _cars.AddRange(cars);
}

public class ArticleService
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "PopularArticles";

    public ArticleService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<List<Article>> GetPopularArticlesAsync()
    {
        if (!_cache.TryGetValue(CacheKey, out List<Article>? articles))
        {
            articles = await _db.Articles.OrderByDescending(a => a.Views).Take(5).ToListAsync();
            var options = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(2));
            _cache.Set(CacheKey, articles, options);
        }
        return articles!;
    }

    public async Task AddArticleAsync(Article article)
    {
        _db.Articles.Add(article);
        await _db.SaveChangesAsync();
        _cache.Remove(CacheKey); 
    }
}

public class UserRatingService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<UserRatingService> _logger;

    public UserRatingService(IMemoryCache cache, ILogger<UserRatingService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public double GetUserRating(string userId)
    {
        string cacheKey = $"UserRating_{userId}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5); // Жизнь кеша - 5 мин[cite: 7]
            _logger.LogInformation("Перераховано"); // Логирование перерасчета[cite: 7]
            return CalculateComplexRating(userId);
        });
    }

    private double CalculateComplexRating(string userId)
    {
        _logger.LogInformation("взято з кеша");
        return new Random().NextDouble() * 100;
    }
}


public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<TodoItem> TodoList { get; set; } = new();

    [BindProperty]
    public TodoItem NewTodo { get; set; } = new();

    public async Task OnGetAsync()
    {
        TodoList = await _db.TodoItems.OrderByDescending(t => t.CreatedAt).ToListAsync();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        if (!ModelState.IsValid)
        {
            TodoList = await _db.TodoItems.ToListAsync();
            return Page();
        }

        _db.TodoItems.Add(NewTodo);
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var item = await _db.TodoItems.FindAsync(id);
        if (item != null)
        {
            item.IsCompleted = !item.IsCompleted;
            await _db.SaveChangesAsync();
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var item = await _db.TodoItems.FindAsync(id);
        if (item != null)
        {
            _db.TodoItems.Remove(item);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage();
    }
}

public class CalculatorModel : PageModel
{
    [BindProperty] public double Num1 { get; set; }
    [BindProperty] public double Num2 { get; set; }
    [BindProperty] public string Operation { get; set; } = "+";
    public double? Result { get; set; }

    public void OnPost()
    {
        Result = Operation switch
        {
            "+" => Num1 + Num2,
            "-" => Num1 - Num2,
            "*" => Num1 * Num2,
            "/" => Num2 != 0 ? Num1 / Num2 : double.NaN,
            _ => 0
        };
    }
}

public class SubjectsModel : PageModel
{
    [BindProperty]
    public List<Subject> Subjects { get; set; } = new();

    public void OnPost() { }
}

public class CarsModel : PageModel
{
    private readonly CarService _carService;
    public CarsModel(CarService carService) => _carService = carService;

    public IReadOnlyList<Car> Cars => _carService.GetCars();

    public void OnGet([FromQuery] List<Car> cars)
    {
        if (cars.Any()) _carService.AddCars(cars);
    }

    public IActionResult OnPost([FromForm] List<Car> cars)
    {
        if (cars.Any()) _carService.AddCars(cars);
        return RedirectToPage();
    }
}