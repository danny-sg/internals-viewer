namespace InternalsViewer.Internals.Exceptions;

public sealed class DatabaseLoadException(string message, Exception exception) 
    : Exception(message, exception);