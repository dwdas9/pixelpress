using System.Collections.Concurrent;
using PixelPress.Core.Services;

namespace PixelPress.Core.Tests.Planning;

/// <summary>
/// In-memory IFileSystem for planner and executor tests. Paths are stored
/// with '/' separators, and every incoming path is normalized to '/', so
/// the fake behaves identically whether the production code built a path
/// with Windows or Unix separators (Path.Combine differs by platform).
///
/// Backed by ConcurrentDictionary rather than Dictionary: JobExecutor
/// genuinely processes files in parallel across multiple threads, so any
/// fake standing in for the file system during its tests must tolerate
/// real concurrent access. A plain Dictionary silently corrupts under
/// concurrent writes - no exception, just dropped entries - which is
/// exactly what surfaced as intermittent, hardware-dependent test
/// failures (fine on lightly-loaded machines, failing on machines with
/// more CPU cores giving the race more chances to trigger).
/// </summary>
public sealed class FakeFileSystem : IFileSystem
{
    private readonly ConcurrentDictionary<string, long> _files =
        new(StringComparer.OrdinalIgnoreCase);

    private static string Normalize(string path) => path.Replace('\\', '/');

    public FakeFileSystem AddFile(string path, long bytes = 1_000)
    {
        _files[Normalize(path)] = bytes;
        return this;
    }

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public bool DirectoryExists(string path)
    {
        var prefix = Normalize(path).TrimEnd('/') + "/";
        return _files.Keys.Any(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<string> EnumerateFilesRecursively(string directory)
    {
        var prefix = Normalize(directory).TrimEnd('/') + "/";
        return _files.Keys
            .Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    public long GetFileLength(string path) => _files[Normalize(path)];

    // --- Execution-phase members --------------------------------------

    public void CreateDirectory(string path)
    {
        // No-op: the fake has no real directory tree; DirectoryExists is
        // derived from file paths already present.
    }

    public string GetTempFilePath(string destinationPath) =>
        $"{Normalize(destinationPath)}.tmp-{Guid.NewGuid():N}";

    public void MoveFileAtomic(string tempPath, string destinationPath, bool overwrite)
    {
        var source = Normalize(tempPath);
        var dest = Normalize(destinationPath);

        if (!_files.TryRemove(source, out var bytes))
        {
            throw new IOException($"Temp file not found: {tempPath}");
        }

        _files[dest] = bytes;
    }

    public void DeleteFile(string path) => _files.TryRemove(Normalize(path), out _);

    /// <summary>Test-only introspection: every path currently "on disk".
    /// Used to assert temp files never linger after a run.</summary>
    public IReadOnlyCollection<string> AllPaths => _files.Keys.ToList();
}
