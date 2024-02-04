using InternalsViewer.Internals.Tests.VerificationTool.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Internals.Tests.VerificationTool;

internal static class Program
{
    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);


        builder.Services.AddTransient<ObjectService>();
        builder.Services.AddTransient<ObjectPageListService>();
        builder.Services.AddTransient<IndexVerificationService>();
        builder.Services.AddTransient<TableVerificationService>();

        builder.Services.RegisterServices();

        builder.Logging.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });

        using var host = builder.Build();

        using var scope = host.Services.CreateScope();

        var services = scope.ServiceProvider;

        var tableService = services.GetRequiredService<TableVerificationService>();
        var indexService = services.GetRequiredService<IndexVerificationService>();

        Console.WriteLine("Verify Type: Tables (T) or Indexes (I)?");

        var verifyType = Console.ReadLine()?.ToLower();

        if (verifyType == "t" || verifyType == "table")
        {
            Console.WriteLine("Object Id or * for all?");

            var objectId = Console.ReadLine();

            if (objectId == "*")
            {
                await tableService.VerifyTables();
            }
            else if (int.TryParse(objectId, out var id))
            {
                await tableService.VerifyTable(id);
            }
        }
        else
        {
            Console.WriteLine("Object Id or * for all?");

            var objectId = Console.ReadLine();

            if (objectId == "*")
            {
                await indexService.VerifyIndexes();
            }
            else
            {
                Console.WriteLine("Index Id?");

                var indexId = Console.ReadLine();

                if(int.TryParse(objectId, out var id) && int.TryParse(indexId, out var index))
                {
                    await indexService.VerifyIndex(id, index);
                }
            }
        }

        Console.WriteLine("Press any key to exit");
        Console.ReadKey();
    }
}
