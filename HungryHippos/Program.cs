using HungryHippos;
using HungryHippos.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddSingleton<GameInfo>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapPost("/join", (string userName, ILogger<Program> logger, GameInfo gameInfo) =>
{
    logger.LogInformation(userName + " wants to sign up.");
    return Results.Ok(gameInfo.JoinPlayer(userName));
});
app.MapPost("/move", (MoveRequest moveRequest, ILogger<Program> logger) =>
{
    logger.LogInformation($"{moveRequest.UserToken} wants to move {moveRequest.Direction}");
    return Results.Ok();
});
app.MapGet("/users", ([FromServices] GameInfo gameInfo) => gameInfo.GetPlayers());

app.Run();
