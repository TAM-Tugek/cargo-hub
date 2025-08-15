var builder = WebApplication.CreateBuilder(args);

// Swagger UI için servisler
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var configuration = builder.Configuration;

// ENV veya *_FILE okuyan yardımcı fonksiyon
string FromEnvOrFile(string key)
{
    var val = Environment.GetEnvironmentVariable(key);
    var file = Environment.GetEnvironmentVariable($"{key}_FILE");
    if (!string.IsNullOrEmpty(file) && File.Exists(file))
        return File.ReadAllText(file).Trim();
    return val ?? string.Empty;
}

// ENV değerlerini oku
var dbHost = FromEnvOrFile("DB_HOST");
var dbName = FromEnvOrFile("DB_NAME");
var dbUser = FromEnvOrFile("DB_USER");
var dbPass = FromEnvOrFile("DB_PASSWORD");
var rabbitUrl = FromEnvOrFile("RABBITMQ_URL");

var app = builder.Build();

// >>> Health endpoint
app.MapGet("/healthz", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        service = "CargoHub.Api",
        timeUtc = DateTime.UtcNow
    });
});
// <<< Health endpoint

// --- Health aliases & root ---
app.MapGet("/", () => Results.Ok(new { hello = "cargo-hub", env = app.Environment.EnvironmentName, timeUtc = DateTime.UtcNow }));
app.MapGet("/health", () => Results.Redirect("/healthz"));
app.MapGet("/api/health", () => Results.Redirect("/healthz"));
app.MapGet("/api/healthz", () => Results.Redirect("/healthz"));
// --- End aliases ---

// >>> Config-check endpoint
app.MapGet("/config-check", () =>
{
    return Results.Ok(new
    {
        Db = new
        {
            Host = dbHost,
            Name = dbName,
            User = dbUser,
            HasPassword = !string.IsNullOrEmpty(dbPass)
        },
        RabbitMQ = string.IsNullOrEmpty(rabbitUrl) ? "NOT_SET" : "SET"
    });
});
// <<< Config-check endpoint

// Swagger + UI: Development ve Staging'de açık
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTP->HTTPS yönlendirmeyi staging/proxy arkasında sorun çıkarmasın diye pas geçiyoruz
// app.UseHttpsRedirection();

// Demo endpoint
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}