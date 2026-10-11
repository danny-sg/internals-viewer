using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using InternalsViewer.Query.Debugging.Exceptions;
using Microsoft.Win32;

namespace InternalsViewer.Query.Debugging.TimeTravel;

[SupportedOSPlatform("windows")]
internal static partial class TimeTravelLicence
{
    private const string Key = @"Software\Microsoft\TTD";

    private const string Value = "EULASigned";

    private const string Recorder = "TTD.exe";

    private const int TermsResource = 100;

    private const int RawDataResource = 10;

    private const uint LoadAsDataFile = 0x2;

    private const uint LoadAsImageResource = 0x20;

    private static readonly TimeSpan AcceptTimeout = TimeSpan.FromSeconds(30);

    public static bool IsAccepted
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(Key);

            return key?.GetValue(Value) is int and not 0;
        }
    }

    public static unsafe string? Read(string ttdDirectory)
    {
        var module = LoadLibraryEx(Path.Combine(ttdDirectory, Recorder), 0, LoadAsDataFile | LoadAsImageResource);

        if (module == 0)
        {
            return null;
        }

        try
        {
            var resource = FindResource(module, TermsResource, RawDataResource);

            var data = resource != 0 ? LockResource(LoadResource(module, resource)) : 0;

            if (data == 0)
            {
                return null;
            }

            using var stream = new UnmanagedMemoryStream((byte*)data, SizeofResource(module, resource));

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            return reader.ReadToEnd();
        }
        finally
        {
            FreeLibrary(module);
        }
    }

    public static async Task AcceptAsync(string ttdDirectory, CancellationToken cancellationToken)
    {
        using var process = Start(Path.Combine(ttdDirectory, Recorder));

        process.StandardInput.Close();

        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);

        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(AcceptTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill();

            throw new DebuggerException($"TTD did not accept the licence within {AcceptTimeout.TotalSeconds:0} seconds");
        }

        if (!IsAccepted)
        {
            throw new DebuggerException($"TTD did not record the licence as accepted. {(await output + await error).Trim()}");
        }
    }

    private static Process Start(string executable)
    {
        var info = new ProcessStartInfo(executable, "-acceptEula")
        {
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            return Process.Start(info) ?? throw new DebuggerException($"{executable} did not start");
        }
        catch (Win32Exception exception)
        {
            throw new DebuggerException($"{executable} could not be started to accept the licence: {exception.Message}");
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryExW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint LoadLibraryEx(string fileName, nint file, uint flags);

    [LibraryImport("kernel32.dll", EntryPoint = "FindResourceW")]
    private static partial nint FindResource(nint module, nint name, nint type);

    [LibraryImport("kernel32.dll")]
    private static partial nint LoadResource(nint module, nint resource);

    [LibraryImport("kernel32.dll")]
    private static partial nint LockResource(nint data);

    [LibraryImport("kernel32.dll")]
    private static partial uint SizeofResource(nint module, nint resource);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FreeLibrary(nint module);
}
