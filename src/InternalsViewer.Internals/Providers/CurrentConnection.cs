﻿namespace InternalsViewer.Internals.Providers;

public class CurrentConnection
{
    public string ConnectionString { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = string.Empty;
}
