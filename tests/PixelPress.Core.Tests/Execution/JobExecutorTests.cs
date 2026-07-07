using PixelPress.Core.Execution;
using PixelPress.Core.Jobs;
using PixelPress.Core.Planning;
using PixelPress.Core.Presets;
using PixelPress.Core.Processing;
using PixelPress.Core.Tests.Planning;
using PixelPress.Core.Tests.Processing;
using Xunit;

namespace PixelPress.Core.Tests.Execution;

public sealed class JobExecutorTests
{
    private static JobRequest Request(params string[] inputs) => new()
    {
        InputPaths = inputs,
        OutputDirectory = "/out",
    };

    private static (JobPlan Plan, FakeFileSystem Fs) PlanFrom(FakeFileSystem fs, JobRequest request) =>
        (new JobPlanner(fs).CreatePlan(request), fs);

    [Fact]
    public async Task All_items_succeed_and_land_at_their_final_output_path()
    {
        var fs = new FakeFileSystem()
            .AddFile("/pics/a.jpg", 1_000)
            .AddFile("/pics/b.jpg", 2_000);
        var (plan, _) = PlanFrom(fs, Request("/pics"));
        var codec = new FakeImageCodec(fs);
        var executor = new JobExecutor(fs, codec);

        var summary = await executor.ExecuteAsync(plan, Request("/pics"));

        Assert.Equal(2, summary.SucceededCount);
        Assert.Equal(0, summary.FailedCount);
        Assert.False(summary.WasCancelled);
        Assert.All(summary.Results, r => Assert.True(fs.FileExists(r.OutputPath)));
    }

    [Fact]
    public async Task Temp_files_never_linger_after_a_successful_run()
    {
        var fs = new FakeFileSystem().AddFile("/pics/a.jpg", 1_000);
        var (plan, _) = PlanFrom(fs, Request("/pics"));
        var codec = new FakeImageCodec(fs);
        var executor = new JobExecutor(fs, codec);

        await executor.ExecuteAsync(plan, Request("/pics"));

        Assert.DoesNotContain(fs.AllPaths, p => p.Contains(".tmp-"));
    }

    [Fact]
    public async Task One_failure_does_not_stop_the_batch()
    {
        var fs = new FakeFileSystem()
            .AddFile("/pics/good1.jpg", 1_000)
            .AddFile("/pics/bad.jpg", 1_000)
            .AddFile("/pics/good2.jpg", 1_000);
        var (plan, _) = PlanFrom(fs, Request("/pics"));

        var codec = new FakeImageCodec(fs)
        {
            Behavior = req => req.SourcePath.Contains("bad")
                ? new CodecResult { Success = false, ErrorMessage = "Corrupt file." }
                : new CodecResult { Success = true, OutputBytes = 500 },
        };
        var executor = new JobExecutor(fs, codec);

        var summary = await executor.ExecuteAsync(plan, Request("/pics"));

        Assert.Equal(3, summary.TotalCount);
        Assert.Equal(2, summary.SucceededCount);
        Assert.Equal(1, summary.FailedCount);
        var failure = Assert.Single(summary.Failures);
        Assert.Contains("bad", failure.SourcePath);
        Assert.Equal("Corrupt file.", failure.ErrorMessage);
    }

    [Fact]
    public async Task Failed_items_leave_no_temp_file_behind_either()
    {
        var fs = new FakeFileSystem().AddFile("/pics/bad.jpg", 1_000);
        var (plan, _) = PlanFrom(fs, Request("/pics"));
        var codec = new FakeImageCodec(fs)
        {
            Behavior = _ => new CodecResult { Success = false, ErrorMessage = "Nope." },
        };
        var executor = new JobExecutor(fs, codec);

        await executor.ExecuteAsync(plan, Request("/pics"));

        Assert.DoesNotContain(fs.AllPaths, p => p.Contains(".tmp-"));
        Assert.DoesNotContain(fs.AllPaths, p => p.Contains("/out/"));
    }

    [Fact]
    public async Task Failed_items_do_not_report_output_bytes()
    {
        var fs = new FakeFileSystem().AddFile("/pics/bad.jpg", 1_000);
        var (plan, _) = PlanFrom(fs, Request("/pics"));
        var codec = new FakeImageCodec(fs)
        {
            Behavior = _ => new CodecResult { Success = false, ErrorMessage = "Nope." },
        };
        var executor = new JobExecutor(fs, codec);

        var summary = await executor.ExecuteAsync(plan, Request("/pics"));

        var result = Assert.Single(summary.Results);
        Assert.Null(result.OutputBytes);
        Assert.Equal(1_000, result.SourceBytes);
    }

    [Fact]
    public async Task Progress_is_reported_once_per_item_reaching_the_total()
    {
        var fs = new FakeFileSystem();
        for (var i = 0; i < 20; i++)
        {
            fs.AddFile($"/pics/img{i}.jpg", 1_000);
        }
        var (plan, _) = PlanFrom(fs, Request("/pics"));
        var codec = new FakeImageCodec(fs);
        var executor = new JobExecutor(fs, codec);

        var reports = new List<ExecutionProgress>();
        var progress = new SynchronousProgress<ExecutionProgress>(reports.Add);

        var summary = await executor.ExecuteAsync(plan, Request("/pics"), progress);

        Assert.Equal(20, reports.Count);
        Assert.All(reports, r => Assert.Equal(20, r.Total));
        Assert.Equal(Enumerable.Range(1, 20), reports.Select(r => r.Completed).OrderBy(x => x));
        Assert.Equal(20, summary.SucceededCount);
    }

    [Fact]
    public async Task Pre_cancelled_token_stops_the_run_and_flags_cancellation()
    {
        var fs = new FakeFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fs.AddFile($"/pics/img{i}.jpg", 1_000);
        }
        var (plan, _) = PlanFrom(fs, Request("/pics"));
        var codec = new FakeImageCodec(fs);
        var executor = new JobExecutor(fs, codec);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var summary = await executor.ExecuteAsync(plan, Request("/pics"), cancellationToken: cts.Token);

        Assert.True(summary.WasCancelled);
        Assert.Empty(summary.Results);
    }

    [Fact]
    public async Task Codec_receives_the_preset_and_metadata_policy_from_the_request()
    {
        var fs = new FakeFileSystem().AddFile("/pics/a.jpg", 1_000);
        var (plan, _) = PlanFrom(fs, Request("/pics"));
        var codec = new FakeImageCodec(fs);
        var executor = new JobExecutor(fs, codec);

        var request = Request("/pics") with
        {
            Preset = PresetId.SmallestSize,
            MetadataPolicy = MetadataPolicy.Strip,
        };

        await executor.ExecuteAsync(plan, request);

        var received = Assert.Single(codec.Requests);
        Assert.Equal(Presets.Presets.SmallestSize, received.Preset);
        Assert.Equal(MetadataPolicy.Strip, received.MetadataPolicy);
    }

    [Fact]
    public async Task Results_are_ordered_deterministically_by_source_path()
    {
        var fs = new FakeFileSystem();
        for (var i = 9; i >= 0; i--)
        {
            fs.AddFile($"/pics/img{i}.jpg", 1_000);
        }
        var (plan, _) = PlanFrom(fs, Request("/pics"));
        var codec = new FakeImageCodec(fs);
        var executor = new JobExecutor(fs, codec);

        var summary = await executor.ExecuteAsync(plan, Request("/pics"));

        var sourcePaths = summary.Results.Select(r => r.SourcePath).ToList();
        var sorted = sourcePaths.OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, sourcePaths);
    }

    /// <summary>IProgress implementation that invokes its callback
    /// synchronously on the reporting thread, so test assertions can rely
    /// on every report having landed by the time ExecuteAsync completes.
    /// The default IProgress&lt;T&gt; captured via SynchronizationContext
    /// would otherwise marshal reports asynchronously, making this test
    /// flaky under xunit's threading model.</summary>
    private sealed class SynchronousProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value)
        {
            lock (callback)
            {
                callback(value);
            }
        }
    }
}
