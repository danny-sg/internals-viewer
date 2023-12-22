﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InternalsViewer.Tests.Internals.UnitTests.TestHelpers;

public class TestLogHelper
{
    public static ILogger<T> GetLogger<T>()
    {
        return new NullLogger<T>();
    }
}