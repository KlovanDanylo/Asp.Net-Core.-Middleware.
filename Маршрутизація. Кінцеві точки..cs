using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<IUserReader>(sp => sp.GetRequiredService<UserService>());
builder.Services.AddSingleton<IUserWriter>(sp => sp.GetRequiredService<UserService>());
builder.Services.AddSingleton<IUserNotifier>(sp => sp.GetRequiredService<UserService>());

var app = builder.Build();

string apiKey = builder.Configuration["TmdbApiKey"] ?? "YOUR_TMDB_API_KEY";

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/test-418")
    {
        context.Response.StatusCode = 418;
    }
    else if (context.Request.Path == "/test-429")
    {
        context.Response.StatusCode = 429;
    }
    else
    {
        await next();
    }

    if (!context.Response.HasStarted)
    {
        if (context.Response.StatusCode == 404)
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync("<h1>Помилка 404: Сторінку не знайдено</h1>");
        }
        else if (context.Response.StatusCode == 418)
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync("<h1>Помилка 418: Я - чайник! 🫖</h1>");
        }
        else if (context.Response.StatusCode == 429)
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync("<h1>Помилка 429: Забагато запитів. Зачекайте хвилинку!</h1>");
        }
    }
});


app.MapGet("/users/{id:int}", (int id, IUserReader reader) =>
{
    string userName = reader.GetUserName(id);
    return Results.Ok(new { id = id, name = userName });
});

app.MapPut("/users/{id:int}", (int id, string name, IUserWriter writer) =>
{
    writer.UpdateUserName(id, name);
    return Results.Ok(new { message = $"Ім'я користувача {id} оновлено на {name}" });
});

app.MapPost("/users/{id:int}/notify", (int id, string msg, IUserNotifier notifier) =>
{
    notifier.Notify(id, msg);
    return Results.Ok(new { message = $"Повідомлення надіслано користувачу {id}" });
});


app.MapGet("/", async (HttpContext context, IHttpClientFactory clientFactory) =>
{
    string page = context.Request.Query["page"].ToString();
    if (string.IsNullOrEmpty(page)) page = "1";

    string search = context.Request.Query["search"].ToString();
    
    string url = string.IsNullOrEmpty(search)
        ? $"https://api.themoviedb.org/3/movie/popular?api_key={apiKey}&language=uk-UA&page={page}"
        : $"https://api.themoviedb.org/3/search/movie?api_key={apiKey}&language=uk-UA&query={search}&page={page}";

    var client = clientFactory.CreateClient();
    TmdbResponse? tmdbData = null;
    string errorMsg = "";

    try
    {
        var response = await client.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var json = await response.ContentReadAsStringAsync();
            tmdbData = JsonSerializer.Deserialize<TmdbResponse>(json);
        }
        else
        {
            errorMsg = "Не вдалося отримати дані з TMDB API. Перевірте свій API-ключ.";
        }
    }
    catch
    {
        errorMsg = "Помилка з'єднання з мережею!";
    }

    string html = "<html><head><meta charset='utf-8'><title>Фільмотека</title></head><body style='font-family:sans-serif; margin:20px;'>" +
                  "<h1>🎬 Фільмотека</h1>" +
                  "<form action='/' method='get'>" +
                  "<input type='text' name='search' placeholder='Пошук фільму...' value='" + search + "' /> " +
                  "<button type='submit'>Шукати</button>" +
                  "</form><hr/>";

    if (!string.IsNullOrEmpty(errorMsg))
    {
        html += $"<p style='color:red;'><b>{errorMsg}</b></p>";
    }
    else if (tmdbData?.Results != null)
    {
        html += "<div style='display:flex; flex-wrap:wrap; gap:15px;'>";
        foreach (var movie in tmdbData.Results)
        {
            string poster = string.IsNullOrEmpty(movie.PosterPath)
                ? "https://via.placeholder.com/150x225?text=No+Poster"
                : $"https://image.tmdb.org/t/p/w200{movie.PosterPath}";

            html += $"<div style='border:1px solid #ccc; padding:10px; width:150px; text-align:center;'>" +
                    $"<img src='{poster}' style='width:100%; border-radius:4px;' />" +
                    $"<h4><a href='/movie/{movie.Id}'>{movie.Title}</a></h4>" +
                    $"<p>⭐ {movie.VoteAverage}</p></div>";
        }
        html += "</div><br/>";

        int currPage = int.Parse(page);
        html += "<div><b>Сторінки: </b>";
        if (currPage > 1) html += $"<a href='/?page={currPage - 1}&search={search}'>[Назад]</a> ";
        html += $"<span> {currPage} </span>";
        html += $"<a href='/?page={currPage + 1}&search={search}'>[Вперед]</a></div>";
    }

    html += "</body></html>";
    return Results.Content(html, "text/html");
});

app.MapGet("/movie/{id:int}", async (int id, IHttpClientFactory clientFactory) =>
{
    var client = clientFactory.CreateClient();
    string movieUrl = $"https://api.themoviedb.org/3/movie/{id}?api_key={apiKey}&language=uk-UA";
    string similarUrl = $"https://api.themoviedb.org/3/movie/{id}/similar?api_key={apiKey}&language=uk-UA";

    try
    {
        var movieResp = await client.GetAsync(movieUrl);
        if (!movieResp.IsSuccessStatusCode)
        {
            return Results.Content("<h2>Фільм не знайдено!</h2>", "text/html");
        }

        var movieJson = await movieResp.ContentReadAsStringAsync();
        var movie = JsonSerializer.Deserialize<MovieDetail>(movieJson);

        // Отримання схожих фільмів
        var simResp = await client.GetAsync(similarUrl);
        var simJson = await simResp.ContentReadAsStringAsync();
        var simData = JsonSerializer.Deserialize<TmdbResponse>(simJson);
        var simList = simData?.Results?.Take(5).ToList() ?? new List<MovieItem>();

        string poster = string.IsNullOrEmpty(movie?.PosterPath)
            ? "https://via.placeholder.com/200x300?text=No+Poster"
            : $"https://image.tmdb.org/t/p/w300{movie.PosterPath}";

        string html = "<html><head><meta charset='utf-8'></head><body style='font-family:sans-serif; margin:20px;'>" +
                      $"<a href='/'>← На головну</a><br/><br/>" +
                      $"<div style='display:flex; gap:20px;'>" +
                      $"<img src='{poster}' style='border-radius:8px;' />" +
                      $"<div><h1>{movie?.Title}</h1>" +
                      $"<p>{movie?.Overview}</p>" +
                      $"<p><b>Дата релізу:</b> {movie?.ReleaseDate}</p>" +
                      $"<p><b>Рейтинг:</b> ⭐ {movie?.VoteAverage}</p></div></div>" +
                      $"<hr/><h3>5 схожих фільмів:</h3><div style='display:flex; gap:15px;'>";

        foreach (var sim in simList)
        {
            string simPoster = string.IsNullOrEmpty(sim.PosterPath)
                ? "https://via.placeholder.com/100x150"
                : $"https://image.tmdb.org/t/p/w200{sim.PosterPath}";

            html += $"<div style='border:1px solid #ddd; padding:5px; width:100px; text-align:center;'>" +
                    $"<img src='{simPoster}' style='width:100%;' />" +
                    $"<p><a href='/movie/{sim.Id}'>{sim.Title}</a></p></div>";
        }

        html += "</div></body></html>";
        return Results.Content(html, "text/html");
    }
    catch
    {
        return Results.Content("<h2>Помилка під час завантаження сторінки фільму</h2>", "text/html");
    }
});

app.Run();

public interface IUserReader
{
    string GetUserName(int id);
}

public interface IUserWriter
{
    void UpdateUserName(int id, string newName);
}

public interface IUserNotifier
{
    void Notify(int id, string message);
}

public class UserService : IUserReader, IUserWriter, IUserNotifier
{
    private readonly Dictionary<int, string> _users = new()
    {
        { 1, "Андрій" },
        { 2, "Олена" }
    };

    public string GetUserName(int id)
    {
        return _users.TryGetValue(id, out string? name) ? name : "Користувача не знайдено";
    }

    public void UpdateUserName(int id, string newName)
    {
        _users[id] = newName;
    }

    public void Notify(int id, string message)
    {
        Console.WriteLine($"[ПОВІДОМЛЕННЯ для User #{id}]: {message}");
    }
}

public class TmdbResponse
{
    [JsonPropertyName("results")]
    public List<MovieItem>? Results { get; set; }
}

public class MovieItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    [JsonPropertyName("vote_average")]
    public double VoteAverage { get; set; }
}

public class MovieDetail
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("overview")]
    public string Overview { get; set; } = "";

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    [JsonPropertyName("release_date")]
    public string ReleaseDate { get; set; } = "";

    [JsonPropertyName("vote_average")]
    public double VoteAverage { get; set; }
}