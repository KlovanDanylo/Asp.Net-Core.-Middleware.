using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

var postgresConnection = builder.Configuration.GetConnectionString("Postgres") 
    ?? "Host=db;Database=appdb;Username=postgres;Password=postgres";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(postgresConnection));

var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "redis:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisConnection));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (!db.Movies.Any())
    {
        db.Movies.AddRange(
            new Movie { Title = "Inception", Votes = 10 },
            new Movie { Title = "Interstellar", Votes = 15 },
            new Movie { Title = "The Matrix", Votes = 8 }
        );
    }

    if (!db.Boards.Any())
    {
        var board = new Board { Title = "Главная Kanban Доска", Description = "Проектный трекер" };
        var list = new ListEntity { Title = "В работе", Position = 1, Board = board };
        list.Cards.Add(new Card { Title = "Создать Dockerfile", Description = "Запаковать API в Docker", Status = "In Progress", Position = 1 });
        db.Boards.Add(board);
        db.Lists.Add(list);
    }

    db.SaveChanges();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();


public class Movie
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Votes { get; set; } = 0;
}

public class Board
{
    public int Id { get; set; }
    [Required] public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ListEntity> Lists { get; set; } = new();
}

public class ListEntity
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public Board? Board { get; set; }
    [Required] public string Title { get; set; } = string.Empty;
    public int Position { get; set; }
    public List<Card> Cards { get; set; } = new();
}

public class Card
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public ListEntity? List { get; set; }
    [Required] public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Todo";
    public int Position { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<ListEntity> Lists => Set<ListEntity>();
    public DbSet<Card> Cards => Set<Card>();
}

[ApiController]
[Route("weather")]
public class WeatherController : ControllerBase
{
    private static readonly string[] Summaries = new[] { "Сонячно", "Хмарно", "Дощ", "Сніг", "Вітряно", "Ясно" };

    [HttpGet("today")]
    public IActionResult GetTodayWeather()
    {
        var rng = new Random();
        return Ok(new
        {
            Date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            TemperatureC = rng.Next(-10, 35),
            Summary = Summaries[rng.Next(Summaries.Length)]
        });
    }

    [HttpGet("{city}")]
    public IActionResult GetCityWeather(string city)
    {
        var rng = new Random();
        return Ok(new
        {
            City = city,
            TemperatureC = rng.Next(-10, 35),
            Summary = Summaries[rng.Next(Summaries.Length)],
            Humidity = rng.Next(30, 95)
        });
    }
}


[ApiController]
[Route("movies")]
public class MoviesController : ControllerBase
{
    private readonly AppDbContext _db;
    public MoviesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetMovies() => Ok(await _db.Movies.ToListAsync());

    [HttpPost("/vote/{movieId}")]
    public async Task<IActionResult> Vote(int movieId)
    {
        var movie = await _db.Movies.FindAsync(movieId);
        if (movie == null) return NotFound("Фільм не знайдено.");

        movie.Votes += 1;
        await _db.SaveChangesAsync();
        return Ok(movie);
    }
}

[ApiController]
[Route("boards")]
public class KanbanController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IDatabase _redis;
    private static int _cacheHits = 0;
    private static int _cacheMisses = 0;

    public KanbanController(AppDbContext db, IConnectionMultiplexer redis)
    {
        _db = db;
        _redis = redis.GetDatabase();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBoard(int id)
    {
        string cacheKey = $"board:{id}";
        var cachedData = await _redis.StringGetAsync(cacheKey);

        if (!cachedData.IsNullOrEmpty)
        {
            Interlocked.Increment(ref _cacheHits);
            var board = JsonSerializer.Deserialize<Board>(cachedData!);
            return Ok(new { Data = board, Source = "Redis Cache" });
        }

        Interlocked.Increment(ref _cacheMisses);
        var boardFromDb = await _db.Boards
            .Include(b => b.Lists)
            .ThenInclude(l => l.Cards)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (boardFromDb == null) return NotFound("Дошку не знайдено.");

        var serialized = JsonSerializer.Serialize(boardFromDb);
        await _redis.StringSetAsync(cacheKey, serialized, TimeSpan.FromSeconds(60));

        return Ok(new { Data = boardFromDb, Source = "PostgreSQL Database" });
    }

    [HttpPost]
    public async Task<IActionResult> CreateBoard([FromBody] Board board)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        _db.Boards.Add(board);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetBoard), new { id = board.Id }, board);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBoard(int id, [FromBody] Board board)
    {
        var existing = await _db.Boards.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Title = board.Title;
        existing.Description = board.Description;
        await _db.SaveChangesAsync();

        await _redis.KeyDeleteAsync($"board:{id}");
        return Ok(existing);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBoard(int id)
    {
        var existing = await _db.Boards.FindAsync(id);
        if (existing == null) return NotFound();

        _db.Boards.Remove(existing);
        await _db.SaveChangesAsync();

        await _redis.KeyDeleteAsync($"board:{id}");
        return NoContent();
    }

    [HttpGet("cache-metrics")]
    public IActionResult GetCacheMetrics()
    {
        return Ok(new
        {
            Hits = _cacheHits,
            Misses = _cacheMisses,
            TotalRequests = _cacheHits + _cacheMisses
        });
    }
}
