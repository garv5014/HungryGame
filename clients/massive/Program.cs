using massive;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;


await Host.CreateDefaultBuilder(args)
    .UseContentRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
    .ConfigureLogging(logging =>
    {

    })
    .ConfigureServices(services =>
    {
        services.AddHostedService<SimpleHostedService>();
        services.AddSingleton<MassiveClient>();
    })
    .RunConsoleAsync();
