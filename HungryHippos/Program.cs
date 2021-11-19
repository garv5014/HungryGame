using HungryHippos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<GameLogic>();
builder.Services.AddSingleton<IRandomService, SystemRandomService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = builder.Environment.ApplicationName, Version = "v1" });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{builder.Environment.ApplicationName} v1"));
}
app.MapFallbackToPage("/_Host");

//API endpoints
app.MapGet("/join", (string userName, GameLogic gameInfo) => gameInfo.JoinPlayer(userName));
app.MapGet("/move/left", (string token, GameLogic gameInfo) => gameInfo.Move(token, Direction.Left));
app.MapGet("/move/right", (string token, GameLogic gameInfo) => gameInfo.Move(token, Direction.Right));
app.MapGet("/move/up", (string token, GameLogic gameInfo) => gameInfo.Move(token, Direction.Up));
app.MapGet("/move/down", (string token, GameLogic gameInfo) => gameInfo.Move(token, Direction.Down));
app.MapGet("/users", ([FromServices] GameLogic gameInfo) => gameInfo.GetPlayersByScoreDescending().Select(p => new {p.Name, p.Id, p.Score}));

app.Run();
