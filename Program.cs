var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/guess", (HttpRequest request) =>
{
    string secretKey = "letmein";
    int secretNumber = 7;

    string key = request.Headers["X-Secret-Key"];
    if (key != secretKey)
    {
        return Results.StatusCode(401);
    }

    string guessText = request.Query["guess"];
    int guess = int.Parse(guessText);

    if (guess == secretNumber)
    {
        return Results.StatusCode(200);
    }
    else
    {
        return Results.StatusCode(400);
    }
});

app.Run();
