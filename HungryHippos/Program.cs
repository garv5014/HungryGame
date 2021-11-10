using HungryHippos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
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

//API endpoints
app.MapGet("/join", (string userName, GameInfo gameInfo) => gameInfo.JoinPlayer(userName));
app.MapGet("/move/left", (string token, GameInfo gameInfo) => gameInfo.Move(token, Direction.Left));
app.MapGet("/move/right", (string token, GameInfo gameInfo) => gameInfo.Move(token, Direction.Right));
app.MapGet("/move/up", (string token, GameInfo gameInfo) => gameInfo.Move(token, Direction.Up));
app.MapGet("/move/down", (string token, GameInfo gameInfo) => gameInfo.Move(token, Direction.Down));
app.MapGet("/users", ([FromServices] GameInfo gameInfo) => gameInfo.GetPlayers().Select(p => new {p.Name, p.Id, p.Score}));

app.Run();
