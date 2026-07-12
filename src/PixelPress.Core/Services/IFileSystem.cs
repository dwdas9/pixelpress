namespace PixelPress.Core.Services;

/// <summary>
/// Minimal file system surface the planner needs. Exists for one reason:
/// planner logic (recursion, classification, conflict resolution) must be
/// testable in memory without touching a real disk. The executor (M3)
/// extends this with read/write operations.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    /// <summary>All files under a directory, recursively, in a stable order.</summary>
    IEnumerable<string> EnumerateFilesRecursively(string directory);

    /// <summary>File size in bytes.</summary>
    long GetFileLength(string path);

    /// <summary>Creates a directory, including any missing parents, if it
    /// doesn't already exist.</summary>
    void CreateDirectory(string path);

    /// <summary>Returns a temp file path in the same directory as
    /// <paramref name="destinationPath"/>, so the later move in
    /// <see cref="MoveFileAtomic"/> is guaranteed to be on the same
    /// volume and therefore atomic.</summary>
    string GetTempFilePath(string destinationPath);

    /// <summary>Atomically replaces the destination's contents with the
    /// temp file's. The destination either has its old contents or its
    /// new contents at every point — never a partial write, even if the
    /// process is killed mid-move.</summary>
    void MoveFileAtomic(string tempPath, string destinationPath, bool overwrite);

    /// <summary>Copies a file, used when <see cref="Processing.InflationGuard"/>
    /// abandons an encode that came out bigger than the source: the original
    /// still has to reach the output folder, or the batch would be missing a
    /// file.</summary>
    void CopyFile(string sourcePath, string destinationPath, bool overwrite);

    /// <summary>Best-effort delete used to clean up an abandoned temp
    /// file after a failed encode. Never throws — a cleanup failure must
    /// not mask the original error being reported to the user.</summary>
    void DeleteFile(string path);
}

/// <summary>Production implementation backed by System.IO.</summary>
public sealed class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<string> EnumerateFilesRecursively(string directory) =>
        Directory.EnumerateFiles(directory, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true, // Unreadable subfolders are skipped, not fatal.
        }).Order(StringComparer.Ordinal);

    public long GetFileLength(string path) => new FileInfo(path).Length;

    public void CreateDirectory(string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public string GetTempFilePath(string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        directory = string.IsNullOrEmpty(directory) ? "." : directory;
        var fileName = Path.GetFileName(destinationPath);
        return Path.Combine(directory, $".{fileName}.tmp-{Guid.NewGuid():N}");
    }

    public void MoveFileAtomic(string tempPath, string destinationPath, bool overwrite) =>
        File.Move(tempPath, destinationPath, overwrite);

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite) =>
        File.Copy(sourcePath, destinationPath, overwrite);

    public void DeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort: the temp file is orphaned but harmless, and the
            // caller's real error (why encoding failed) must not be masked.
        }
    }
}
