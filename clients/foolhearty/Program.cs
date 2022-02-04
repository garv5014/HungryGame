using foolhearty;
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
        services.AddHostedService<ClientLogic>();
        services.AddTransient<Foolhearty>();
    })
    .RunConsoleAsync();