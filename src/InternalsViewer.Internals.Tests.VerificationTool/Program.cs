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

        var logFilename = $"C:\\Temp\\VerificationTool_{DateTime.Now:yyyyMMddHHmm}.log";

        tableService.LogFilename = logFilename;
        indexService.LogFilename = logFilename;

        Console.WriteLine("Database?");

        var databaseName = Console.ReadLine() ?? string.Empty;

        Console.WriteLine("Verify Type: Tables (T) or Indexes (I)?");

        var verifyType = Console.ReadLine()?.ToLower();

        if (verifyType == "t" || verifyType == "table")
        {
            Console.WriteLine("Object Id or * for all?");

            var objectId = Console.ReadLine();

            if (objectId == "*")
            {
                try
                {
                    await tableService.VerifyTables(databaseName);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
            else if (int.TryParse(objectId, out var id))
            {
                try
                {
                    await tableService.VerifyTable(databaseName, id);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
        else
        {
            Console.WriteLine("Object Id or * for all?");

            var objectId = Console.ReadLine();

            if (objectId == "*")
            {
                try
                {
                    await indexService.VerifyIndexes(databaseName);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            else
            {
                Console.WriteLine("Index Id?");

                var indexId = Console.ReadLine();

                if (int.TryParse(objectId, out var id) && int.TryParse(indexId, out var index))
                {
                    try
                    {
                        await indexService.VerifyIndex(databaseName, id, index);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }
        }

        Console.WriteLine("Press any key to exit");
        Console.ReadKey();
    }
}
