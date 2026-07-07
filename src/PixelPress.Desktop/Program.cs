using System.Runtime.InteropServices;
using Avalonia;
using PixelPress.Core.Processing;

namespace PixelPress.Desktop;

internal static class Program
{
    // Windows only: a WinExe (GUI-subsystem) process isn't attached to
    // the console it was launched from, so plain Console.WriteLine
    // writes nowhere when run interactively - exactly what produced no
    // visible output for --verify-codecs. Attaching to the parent
    // console before writing fixes this without giving the normal GUI
    // launch a console window, since this path only runs for the CLI flag.
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    private const int AttachParentProcess = -1;

    // Avalonia configuration and startup. Application-level composition
    // (DI, main window) lives in App.axaml.cs, keeping this file inert.
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--verify-codecs"))
        {
            return RunCodecVerification();
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// Round-trips a synthetic image through every encodable format and
    /// prints the result. Run with `dotnet run -- --verify-codecs` (or
    /// the published exe with `--verify-codecs`). Exists because codec
    /// behaviour is the one part of this app that can only be confirmed
    /// on the actual platforms it ships for — this is the one-command
    /// way to check the format matrix on real Windows and real macOS.
    /// </summary>
    private static int RunCodecVerification()
    {
        if (OperatingSystem.IsWindows() && !Console.IsOutputRedirected)
        {
            AttachConsole(AttachParentProcess);
            // Re-bind Console.Out to the now-attached console. Without
            // this, .NET may already hold a handle opened before the
            // console was attached and output still goes nowhere. Only
            // done when output isn't already redirected (e.g. `> file`)
            // — attaching unconditionally would override a real
            // redirection target with the console instead.
            var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            Console.SetOut(stdout);
        }

        Console.WriteLine("Verifying image codecs...\n");

        var report = CodecVerifier.Run();

        foreach (var result in report.Results)
        {
            var status = result.Success ? "OK  " : "FAIL";
            var detail = result.ErrorMessage is null ? "" : $" — {result.ErrorMessage}";
            Console.WriteLine($"  [{status}] {result.FormatName}{detail}");
        }

        Console.WriteLine(report.AllPassed
            ? "\nAll codecs verified."
            : "\nSome codecs failed. See above.");

        return report.AllPassed ? 0 : 1;
    }
}
