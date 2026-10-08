using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient(); 
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<QuestGameService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers(); 

app.Run();


public class HomeController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _openWeatherApiKey = "YOUR_API_KEY_HERE"; 

    public HomeController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new WeatherViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Index(string city)
    {
        var model = new WeatherViewModel { City = city };

        if (string.IsNullOrWhiteSpace(city))
        {
            model.ErrorMessage = "Пожалуйста, введите название города.";
            return View(model);
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(city)}&appid={_openWeatherApiKey}&units=metric&lang=ru";
            
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var weatherData = await response.Content.ReadFromJsonAsync<OpenWeatherResponse>();
                if (weatherData != null)
                {
                    model.Temperature = weatherData.Main.Temp;
                    model.Description = weatherData.Weather.FirstOrDefault()?.Description ?? "Нет описания";
                    model.Humidity = weatherData.Main.Humidity;
                    model.WindSpeed = weatherData.Wind.Speed;
                    model.IsSuccess = true;
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                model.ErrorMessage = $"Город '{city}' не найден. Проверьте правильность написания.";
            }
            else
            {
                model.ErrorMessage = "Ошибка при получении данных от погодного сервиса.";
            }
        }
        catch (Exception ex)
        {
            model.ErrorMessage = $"Произошла непредвиденная ошибка: {ex.Message}";
        }

        return View(model);
    }
}

public class WeatherViewModel
{
    public string City { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Humidity { get; set; }
    public double WindSpeed { get; set; }
    public bool IsSuccess { get; set; } = false;
    public string? ErrorMessage { get; set; }
}

public class OpenWeatherResponse
{
    [JsonPropertyName("main")] public MainInfo Main { get; set; } = new();
    [JsonPropertyName("weather")] public List<WeatherDescription> Weather { get; set; } = new();
    [JsonPropertyName("wind")] public WindInfo Wind { get; set; } = new();
}

public class MainInfo
{
    [JsonPropertyName("temp")] public double Temp { get; set; }
    [JsonPropertyName("humidity")] public int Humidity { get; set; }
}

public class WeatherDescription
{
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
}

public class WindInfo
{
    [JsonPropertyName("speed")] public double Speed { get; set; }
}


[ApiController]
[Route("api")]
public class QuestRoomController : ControllerBase
{
    private readonly QuestGameService _gameService;

    public QuestRoomController(QuestGameService gameService)
    {
        _gameService = gameService;
    }

    [HttpGet("cases")]
    public IActionResult GetCases() => Ok(_gameService.GetCases());

    [HttpGet("cases/{id}/locations")]
    public IActionResult GetLocations(int id)
    {
        var locations = _gameService.GetLocations(id);
        return locations != null ? Ok(locations) : NotFound("Дело не найдено.");
    }

    [HttpGet("locations/{locationId}/clues")]
    public IActionResult GetClues(int locationId)
    {
        var clues = _gameService.GetClues(locationId);
        return clues != null ? Ok(clues) : NotFound("Локация не найдена.");
    }

    [HttpPost("cases/{id}/inspect")]
    public IActionResult InspectClue(int id, [FromBody] InspectRequest request)
    {
        var result = _gameService.InspectClue(id, request.ClueId);
        return result != null ? Ok(result) : BadRequest("Ошибка осмотра улики.");
    }

    [HttpPost("cases/{id}/combine-clues")]
    public IActionResult CombineClues(int id, [FromBody] CombineCluesRequest request)
    {
        var result = _gameService.CombineClues(id, request.ClueId1, request.ClueId2);
        return result != null ? Ok(result) : BadRequest("Эти улики нельзя объединить.");
    }

    [HttpPost("cases/{id}/solve")]
    public IActionResult SolveCase(int id, [FromBody] SolveCaseRequest request)
    {
        var result = _gameService.SolveCase(id, request.SuspectName, request.Motive);
        return Ok(result);
    }

    [HttpGet("cases/{id}/notebook")]
    public IActionResult GetNotebook(int id)
    {
        var notebook = _gameService.GetNotebook(id);
        return notebook != null ? Ok(notebook) : NotFound("Записи не найдены.");
    }
}

public record InspectRequest(int ClueId);
public record CombineCluesRequest(int ClueId1, int ClueId2);
public record SolveCaseRequest(string SuspectName, string Motive);

public class DetectiveCase
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Средняя";
    public string Status { get; set; } = "Не раскрыто"; 
}

public class Location
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class Clue
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public bool IsKeyEvidence { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class Notebook
{
    public List<string> FoundClues { get; set; } = new();
    public List<string> UnlockedSuspects { get; set; } = new();
}

public class QuestGameService
{
    private readonly List<DetectiveCase> _cases = new()
    {
        new DetectiveCase { Id = 1, Title = "Тайна заброшенного особняка", Difficulty = "Высокая", Status = "В процессе" }
    };

    private readonly List<Location> _locations = new()
    {
        new Location { Id = 101, CaseId = 1, Name = "Кабинет директора" },
        new Location { Id = 102, CaseId = 1, Name = "Старый гараж" }
    };

    private readonly List<Clue> _clues = new()
    {
        new Clue { Id = 1, LocationId = 101, Name = "Ключ от сейфа", IsLocked = false, IsKeyEvidence = true, Description = "Маленький ржавый ключ" },
        new Clue { Id = 2, LocationId = 101, Name = "Старый сейф", IsLocked = true, IsKeyEvidence = false, Description = "Тяжелый запертый сейф" }
    };

    private readonly Notebook _notebook = new()
    {
        FoundClues = new List<string> { "Записка с угрозой" },
        UnlockedSuspects = new List<string> { "Дворецкий Джон", "Бизнес-партнер Марк" }
    };

    public List<DetectiveCase> GetCases() => _cases;
    public List<Location>? GetLocations(int caseId) => _locations.Where(l => l.CaseId == caseId).ToList();
    public List<Clue>? GetClues(int locationId) => _clues.Where(c => c.LocationId == locationId).ToList();

    public object? InspectClue(int caseId, int clueId)
    {
        var clue = _clues.FirstOrDefault(c => c.Id == clueId);
        if (clue == null) return null;
        return new { clue.Name, clue.Description, clue.IsKeyEvidence };
    }

    public object? CombineClues(int caseId, int clue1Id, int clue2Id)
    {
        if ((clue1Id == 1 && clue2Id == 2) || (clue1Id == 2 && clue2Id == 1))
        {
            _notebook.FoundClues.Add("Секретные документы из сейфа");
            return new { Result = "Сейф открыт! Получены новые секретные документы.", NewClue = "Секретные документы" };
        }
        return null;
    }

    public object SolveCase(int caseId, string suspect, string motive)
    {
        if (suspect.Equals("Марк", StringComparison.OrdinalIgnoreCase))
        {
            return new { Status = "Победа", Message = "Вы верно определили преступника и мотив! Дело закрыто." };
        }
        return new { Status = "Поражение", Message = "Преступник ушел от ответственности. Неверные выводы." };
    }

    public Notebook GetNotebook(int caseId) => _notebook;
}