﻿using Microsoft.Extensions.Configuration;

namespace InternalsViewer.Tests.Internals.UnitTests.TestHelpers;
internal class ConnectionStringHelper
{
    public static string GetConnectionString(string name)
    {
        var builder = new ConfigurationBuilder().AddUserSecrets<ConnectionStringHelper>();

        var configuration = builder.Build();

        return configuration.GetConnectionString(name);
    }
}
