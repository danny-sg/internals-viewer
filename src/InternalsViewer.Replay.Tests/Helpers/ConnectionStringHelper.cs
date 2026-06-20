using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace InternalsViewer.Replay.Tests.Helpers;

internal class ConnectionStringHelper
{
    public static string GetConnectionString(string name)
    {
        var builder = new ConfigurationBuilder().AddUserSecrets<ConnectionStringHelper>();

        var configuration = builder.Build();

        return configuration.GetConnectionString(name) ?? string.Empty;
    }
}
