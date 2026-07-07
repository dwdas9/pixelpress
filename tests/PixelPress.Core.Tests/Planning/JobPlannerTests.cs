using PixelPress.Core.Formats;
using PixelPress.Core.Jobs;
using PixelPress.Core.Planning;
using Xunit;

namespace PixelPress.Core.Tests.Planning;

public sealed class JobPlannerTests
{
    private static JobRequest Request(params string[] inputs) => new()
    {
        InputPaths = inputs,
        OutputDirectory = "/out",
    };

    [Fact]
    public void Plans_a_single_file_into_the_output_folder()
    {
        var fs = new FakeFileSystem().AddFile("/pics/cat.jpg", 2_000);
        var plan = new JobPlanner(fs).CreatePlan(Request("/pics/cat.jpg"));

        var item = Assert.Single(plan.Items);
        Assert.Equal("/out/cat.jpg", item.OutputPath.Replace('\\', '/'));
        Assert.Equal(ImageFormatId.Jpeg, item.SourceFormat);
        Assert.Equal(ImageFormatId.Jpeg, item.OutputFormat);
        Assert.Equal(2_000, item.SourceBytes);
        Assert.True(item.EstimatedOutputBytes is > 0 and < 2_000);
    }

    [Fact]
    public void Scans_folders_recursively_and_mirrors_structure()
    {
        var fs = new FakeFileSystem()
            .AddFile("/photos/a.jpg")
            .AddFile("/photos/2024/b.png")
            .AddFile("/photos/2024/summer/c.webp");

        var plan = new JobPlanner(fs).CreatePlan(Request("/photos"));

        var outputs = plan.Items.Select(i => i.OutputPath.Replace('\\', '/')).ToList();
        Assert.Equal(3, plan.Items.Count);
        Assert.Contains("/out/photos/a.jpg", outputs);
        Assert.Contains("/out/photos/2024/b.png", outputs);
        Assert.Contains("/out/photos/2024/summer/c.webp", outputs);
    }

    [Fact]
    public void Non_image_files_are_skipped_not_fatal()
    {
        var fs = new FakeFileSystem()
            .AddFile("/mixed/photo.jpg")
            .AddFile("/mixed/notes.txt")
            .AddFile("/mixed/report.pdf");

        var plan = new JobPlanner(fs).CreatePlan(Request("/mixed"));

        Assert.Single(plan.Items);
        Assert.Equal(2, plan.Skipped.Count(s => s.Reason == SkipReason.NotAnImage));
    }

    [Fact]
    public void Missing_paths_are_reported_not_thrown()
    {
        var fs = new FakeFileSystem().AddFile("/pics/real.jpg");
        var plan = new JobPlanner(fs).CreatePlan(Request("/pics/real.jpg", "/pics/gone.jpg"));

        Assert.Single(plan.Items);
        var skip = Assert.Single(plan.Skipped);
        Assert.Equal(SkipReason.Missing, skip.Reason);
    }

    [Fact]
    public void Duplicate_inputs_are_processed_once()
    {
        var fs = new FakeFileSystem().AddFile("/pics/cat.jpg");
        var plan = new JobPlanner(fs).CreatePlan(Request("/pics/cat.jpg", "/pics/CAT.JPG"));

        Assert.Single(plan.Items);
        Assert.Single(plan.Skipped, s => s.Reason == SkipReason.Duplicate);
    }

    [Fact]
    public void Heic_falls_back_to_jpeg_when_keeping_format_and_is_flagged()
    {
        var fs = new FakeFileSystem().AddFile("/pics/IMG_0001.heic");
        var plan = new JobPlanner(fs).CreatePlan(Request("/pics/IMG_0001.heic"));

        var item = Assert.Single(plan.Items);
        Assert.Equal(ImageFormatId.Heic, item.SourceFormat);
        Assert.Equal(ImageFormatId.Jpeg, item.OutputFormat);
        Assert.True(item.FormatFallbackApplied);
        Assert.Equal(1, plan.FormatFallbackCount);
        Assert.EndsWith(".jpg", item.OutputPath);
    }

    [Fact]
    public void Explicit_conversion_is_not_flagged_as_fallback()
    {
        var fs = new FakeFileSystem().AddFile("/pics/IMG_0001.heic");
        var request = Request("/pics/IMG_0001.heic") with
        {
            TargetFormat = ImageFormatId.WebP,
        };

        var item = Assert.Single(new JobPlanner(fs).CreatePlan(request).Items);
        Assert.Equal(ImageFormatId.WebP, item.OutputFormat);
        Assert.False(item.FormatFallbackApplied);
    }

    [Fact]
    public void Colliding_output_names_are_auto_suffixed_deterministically()
    {
        // photo.heic and photo.png both convert to photo.jpg.
        var fs = new FakeFileSystem()
            .AddFile("/pics/photo.heic")
            .AddFile("/pics/photo.png");
        var request = Request("/pics") with { TargetFormat = ImageFormatId.Jpeg };

        var plan = new JobPlanner(fs).CreatePlan(request);

        var outputs = plan.Items.Select(i => i.OutputPath.Replace('\\', '/')).ToList();
        Assert.Equal(2, outputs.Count);
        Assert.Contains("/out/pics/photo.jpg", outputs);
        Assert.Contains("/out/pics/photo (2).jpg", outputs);
        Assert.Equal(1, plan.RenamedCount);
    }

    [Fact]
    public void Existing_files_on_disk_also_cause_renames()
    {
        var fs = new FakeFileSystem()
            .AddFile("/pics/cat.jpg")
            .AddFile("/out/cat.jpg"); // Already there from a previous run.

        var plan = new JobPlanner(fs).CreatePlan(Request("/pics/cat.jpg"));

        var item = Assert.Single(plan.Items);
        Assert.Equal("/out/cat (2).jpg", item.OutputPath.Replace('\\', '/'));
        Assert.True(item.RenamedToAvoidConflict);
    }

    [Fact]
    public void Overwrite_mode_targets_the_source_path_without_self_collision()
    {
        var fs = new FakeFileSystem().AddFile("/pics/cat.jpg");
        var request = new JobRequest
        {
            InputPaths = ["/pics/cat.jpg"],
            OutputPolicy = OutputPolicy.OverwriteOriginals,
        };

        var item = Assert.Single(new JobPlanner(fs).CreatePlan(request).Items);
        Assert.Equal("/pics/cat.jpg", item.OutputPath.Replace('\\', '/'));
        Assert.False(item.RenamedToAvoidConflict);
    }

    [Fact]
    public void Overwrite_mode_with_format_change_writes_a_sibling_file()
    {
        var fs = new FakeFileSystem().AddFile("/pics/IMG_0001.heic");
        var request = new JobRequest
        {
            InputPaths = ["/pics/IMG_0001.heic"],
            OutputPolicy = OutputPolicy.OverwriteOriginals,
        };

        var item = Assert.Single(new JobPlanner(fs).CreatePlan(request).Items);
        Assert.Equal("/pics/IMG_0001.jpg", item.OutputPath.Replace('\\', '/'));
    }

    [Fact]
    public void Empty_input_list_is_rejected()
    {
        var planner = new JobPlanner(new FakeFileSystem());
        Assert.Throws<ArgumentException>(
            () => planner.CreatePlan(new JobRequest
            {
                InputPaths = [],
                OutputDirectory = "/out",
            }));
    }

    [Fact]
    public void Separate_folder_policy_requires_an_output_directory()
    {
        var planner = new JobPlanner(new FakeFileSystem().AddFile("/a.jpg"));
        Assert.Throws<ArgumentException>(
            () => planner.CreatePlan(new JobRequest { InputPaths = ["/a.jpg"] }));
    }

    [Fact]
    public void Input_only_formats_are_rejected_as_conversion_targets()
    {
        var planner = new JobPlanner(new FakeFileSystem().AddFile("/a.jpg"));
        var request = Request("/a.jpg") with { TargetFormat = ImageFormatId.Heic };

        Assert.Throws<ArgumentException>(() => planner.CreatePlan(request));
    }

    [Fact]
    public void Plan_totals_aggregate_correctly()
    {
        var fs = new FakeFileSystem()
            .AddFile("/pics/a.jpg", 1_000)
            .AddFile("/pics/b.jpg", 3_000);

        var plan = new JobPlanner(fs).CreatePlan(Request("/pics"));

        Assert.Equal(4_000, plan.TotalSourceBytes);
        Assert.True(plan.TotalEstimatedOutputBytes > 0);
        Assert.True(plan.TotalEstimatedOutputBytes < plan.TotalSourceBytes);
        Assert.False(plan.IsEmpty);
    }
}
