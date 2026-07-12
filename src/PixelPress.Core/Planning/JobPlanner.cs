using PixelPress.Core.Formats;
using PixelPress.Core.Jobs;
using PixelPress.Core.Services;

namespace PixelPress.Core.Planning;

/// <summary>
/// Phase one of the two-phase pipeline. Turns a <see cref="JobRequest"/>
/// into a complete <see cref="JobPlan"/> without touching any pixels:
/// expands directories, classifies files, decides every output path,
/// resolves every naming conflict, and estimates savings. All decisions
/// a run will make are visible in the plan before the run starts.
/// </summary>
public sealed class JobPlanner(IFileSystem fileSystem)
{
    /// <summary>Fallback target when a source format is input-only and the
    /// user asked to keep formats. JPEG opens everywhere, which is the
    /// right default for everyday users.</summary>
    private const ImageFormatId InputOnlyFallback = ImageFormatId.Jpeg;

    public JobPlan CreatePlan(JobRequest request)
    {
        Validate(request);

        var skipped = new List<SkippedFile>();
        var sources = CollectSourceFiles(request, skipped);
        var items = BuildItems(request, sources, skipped);

        return new JobPlan { Items = items, Skipped = skipped };
    }

    /// <summary>
    /// Re-estimates an existing plan at a new quality, without re-planning it.
    ///
    /// Quality is the one setting that cannot change the *shape* of a plan: it
    /// does not affect which files are in the batch, what format they become,
    /// where they are written, or which names collide. It changes exactly one
    /// number per item. So dragging the quality slider has no business
    /// re-walking the file system, re-stat-ing every file and re-resolving
    /// every naming conflict — which is what routing it through
    /// <see cref="CreatePlan"/> did, and why the batch estimate felt too
    /// expensive to keep live.
    ///
    /// Pure arithmetic over the existing items, no I/O. Every other setting
    /// (target format, resize, metadata, output policy) genuinely can change
    /// the plan's shape and still needs a full <see cref="CreatePlan"/>.
    /// </summary>
    public static JobPlan ReEstimate(JobPlan plan, int quality)
    {
        var items = plan.Items
            .Select(item => item with
            {
                EstimatedOutputBytes = SizeEstimator.Estimate(
                    item.SourceBytes,
                    FormatRegistry.Get(item.SourceFormat),
                    FormatRegistry.Get(item.OutputFormat),
                    quality),
            })
            .ToList();

        return plan with { Items = items };
    }

    private static void Validate(JobRequest request)
    {
        if (request.InputPaths.Count == 0)
        {
            throw new ArgumentException("At least one input path is required.", nameof(request));
        }

        if (request.OutputPolicy == OutputPolicy.SeparateFolder &&
            string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            throw new ArgumentException(
                "OutputDirectory is required when writing to a separate folder.",
                nameof(request));
        }

        if (request.TargetFormat is { } target && !FormatRegistry.Get(target).CanEncode)
        {
            throw new ArgumentException(
                $"{target} cannot be used as an output format.", nameof(request));
        }

        if (request.ResizeEnabled && request.ResizeMaxDimensionPixels <= 0)
        {
            throw new ArgumentException(
                "ResizeMaxDimensionPixels must be greater than zero when resize is enabled.",
                nameof(request));
        }

        if (request.Quality is < 1 or > 100)
        {
            throw new ArgumentException(
                "Quality must be between 1 and 100.", nameof(request));
        }
    }

    /// <summary>
    /// Expands the user's dropped paths into concrete source files.
    /// Directories are scanned recursively; each file remembers the root
    /// it came from so folder structure can be mirrored. Loose files have
    /// no root and land in the output directory itself.
    /// </summary>
    private List<SourceFile> CollectSourceFiles(JobRequest request, List<SkippedFile> skipped)
    {
        var seen = new HashSet<string>(PathComparer);
        var sources = new List<SourceFile>();

        void Add(string path, string? root)
        {
            if (!seen.Add(path))
            {
                skipped.Add(new SkippedFile(path, SkipReason.Duplicate));
            }
            else if (FormatRegistry.FromPath(path) is null)
            {
                skipped.Add(new SkippedFile(path, SkipReason.NotAnImage));
            }
            else
            {
                sources.Add(new SourceFile(path, root));
            }
        }

        foreach (var input in request.InputPaths)
        {
            if (fileSystem.DirectoryExists(input))
            {
                foreach (var file in fileSystem.EnumerateFilesRecursively(input))
                {
                    Add(file, input);
                }
            }
            else if (fileSystem.FileExists(input))
            {
                Add(input, root: null);
            }
            else
            {
                skipped.Add(new SkippedFile(input, SkipReason.Missing));
            }
        }

        return sources;
    }

    private List<PlannedItem> BuildItems(
        JobRequest request, List<SourceFile> sources, List<SkippedFile> skipped)
    {
        _ = skipped; // Reserved for future per-item exclusions during build.
        var items = new List<PlannedItem>(sources.Count);
        var claimedOutputs = new HashSet<string>(PathComparer);

        foreach (var source in sources)
        {
            var sourceFormat = FormatRegistry.FromPath(source.Path)!;
            var (outputFormat, fallback) = ResolveOutputFormat(request, sourceFormat);

            var idealPath = ComputeIdealOutputPath(request, source, outputFormat);
            var (finalPath, renamed) = ResolveConflict(idealPath, claimedOutputs, request, source);
            claimedOutputs.Add(finalPath);

            var sourceBytes = fileSystem.GetFileLength(source.Path);

            items.Add(new PlannedItem
            {
                SourcePath = source.Path,
                OutputPath = finalPath,
                SourceFormat = sourceFormat.Id,
                OutputFormat = outputFormat.Id,
                SourceBytes = sourceBytes,
                EstimatedOutputBytes = SizeEstimator.Estimate(
                    sourceBytes, sourceFormat, outputFormat, request.Quality),
                FormatFallbackApplied = fallback,
                RenamedToAvoidConflict = renamed,
            });
        }

        return items;
    }

    private static (ImageFormat Format, bool Fallback) ResolveOutputFormat(
        JobRequest request, ImageFormat sourceFormat)
    {
        if (request.TargetFormat is { } target)
        {
            return (FormatRegistry.Get(target), Fallback: false);
        }

        // "Keep format" job: input-only sources (HEIC, RAW, ICO) cannot be
        // re-encoded as themselves, so they fall back — visibly flagged.
        return sourceFormat.CanEncode
            ? (sourceFormat, Fallback: false)
            : (FormatRegistry.Get(InputOnlyFallback), Fallback: true);
    }

    private static string ComputeIdealOutputPath(
        JobRequest request, SourceFile source, ImageFormat outputFormat)
    {
        var newFileName = Path.GetFileNameWithoutExtension(source.Path)
                          + "." + outputFormat.CanonicalExtension;

        if (request.OutputPolicy == OutputPolicy.OverwriteOriginals)
        {
            // Same directory as the source; extension may still change
            // (format conversion in place produces a sibling file).
            return Path.Combine(Path.GetDirectoryName(source.Path) ?? "", newFileName);
        }

        var outputRoot = request.OutputDirectory!;

        if (request.PreserveFolderStructure && source.Root is { } root)
        {
            // Mirror the dropped folder (by name) and the file's relative
            // location inside it: dropping /photos/2024/a.jpg via /photos
            // yields <out>/photos/2024/a.<ext>.
            var rootName = Path.GetFileName(Path.TrimEndingDirectorySeparator(root));
            var relativeDir = Path.GetDirectoryName(Path.GetRelativePath(root, source.Path)) ?? "";
            return Path.Combine(outputRoot, rootName, relativeDir, newFileName);
        }

        return Path.Combine(outputRoot, newFileName);
    }

    /// <summary>
    /// Guarantees a unique output path. Collisions come from two places:
    /// another planned output (photo.heic + photo.jpg both → photo.jpg)
    /// or a file already on disk. Resolution is deterministic: append
    /// " (2)", " (3)"… in scan order. In overwrite mode, replacing the
    /// source itself is the whole point, so the source's own path never
    /// counts as a disk collision.
    /// </summary>
    private (string Path, bool Renamed) ResolveConflict(
        string idealPath,
        HashSet<string> claimedOutputs,
        JobRequest request,
        SourceFile source)
    {
        bool CollidesOnDisk(string candidate) =>
            fileSystem.FileExists(candidate) &&
            !(request.OutputPolicy == OutputPolicy.OverwriteOriginals &&
              PathsReferToSameFile(candidate, source.Path));

        if (!claimedOutputs.Contains(idealPath) && !CollidesOnDisk(idealPath))
        {
            return (idealPath, Renamed: false);
        }

        var directory = Path.GetDirectoryName(idealPath) ?? "";
        var stem = Path.GetFileNameWithoutExtension(idealPath);
        var extension = Path.GetExtension(idealPath);

        for (var n = 2; ; n++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({n}){extension}");
            if (!claimedOutputs.Contains(candidate) && !CollidesOnDisk(candidate))
            {
                return (candidate, Renamed: true);
            }
        }
    }

    /// <summary>Case-insensitive path comparison: Windows and default
    /// macOS volumes are both case-insensitive, and treating paths
    /// case-sensitively there would plan two outputs into one file.</summary>
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>Compares two paths for equality regardless of which
    /// separator style each was built with — Path.Combine emits '\' on
    /// Windows even when a caller-supplied path used '/'.</summary>
    private static bool PathsReferToSameFile(string a, string b) =>
        string.Equals(
            a.Replace('\\', '/'),
            b.Replace('\\', '/'),
            StringComparison.OrdinalIgnoreCase);

    private sealed record SourceFile(string Path, string? Root);
}
