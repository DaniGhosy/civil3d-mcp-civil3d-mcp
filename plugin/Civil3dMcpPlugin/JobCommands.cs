using System.Collections;
using System.Text.Json.Nodes;

namespace Civil3DMcpPlugin;

/// <summary>
/// Async job queue backing civil3d_job: start a long-running operation as a background task,
/// poll its status, or request cooperative cancellation.
///
/// Adapted from source: source validates the requested operation against a hardcoded 3-entry
/// allowlist (publish_sheet_pdf, surface_dem_import, bulk_qc_report) mapped to plugin method
/// names. publish_sheet_pdf is confirmed impossible in this plugin (PublishSheetPdfAsync always
/// throws — see PlanProductionCommands equivalent notes), so that specific allowlist doesn't
/// transfer. Instead of hand-picking a different fixed set, this version accepts ANY method name
/// already handled by CommandDispatcher.DispatchAsync directly as the job's operation — simpler,
/// immediately useful for every long-running command already ported (e.g. qcReportGenerate for a
/// bulk QC report across a large drawing), and consistent with this plugin's own "primitives, not
/// business commands" convention (see CLAUDE.md). An unrecognized method name surfaces
/// CommandDispatcher's own existing "not implemented" error through the job's failed state,
/// rather than duplicating a second list of valid names to keep in sync.
///
/// Also skips source's AsyncLocal-based RunWithRequestContextAsync/PluginLog — this plugin has no
/// existing per-request context-tracking or logging infrastructure to hook into, and the job
/// itself already carries everything needed (jobId, operation, drawing identity) without it.
/// </summary>
public static class JobCommands
{
  public static Task<object?> StartJobAsync(JsonObject? parameters)
  {
    var operation = PluginRuntime.GetRequiredString(parameters, "operation");
    var operationParameters = parameters?["parameters"] as JsonObject;
    operationParameters = operationParameters?.DeepClone() as JsonObject ?? new JsonObject();

    var job = JobRegistry.Create(
      $"Queued {operation}",
      operation,
      requestId: null,
      PluginRuntime.GetActiveDrawingIdentity());
    job.CancellationSource = new CancellationTokenSource();
    var cancellationToken = job.CancellationSource.Token;

    _ = Task.Run(async () =>
    {
      try
      {
        // Keep the queued state observable long enough for a caller to issue an
        // immediate cooperative cancellation after StartJob.
        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        JobRegistry.Progress(job.JobId, 10, $"Starting {operation}", null);
        var result = await CommandDispatcher.DispatchAsync(operation, operationParameters, cancellationToken);
        var warnings = ExtractWarnings(result).ToList();
        if (cancellationToken.IsCancellationRequested)
        {
          warnings.Add("Cancellation was requested after non-interruptible Civil 3D host work began; the committed result is reported as completed.");
        }
        JobRegistry.Complete(job.JobId, result, warnings);
      }
      catch (OperationCanceledException)
      {
        JobRegistry.AcknowledgeCancellation(job.JobId);
      }
      catch (JsonRpcDispatchException ex)
      {
        JobRegistry.Fail(job.JobId, ex.Message, ex.Code);
      }
      catch (Exception ex)
      {
        JobRegistry.Fail(job.JobId, ex.Message, ex.GetType().Name);
      }
      finally
      {
        try
        {
          job.CancellationSource?.Dispose();
        }
        catch (ObjectDisposedException)
        {
          // Worker and cancellation raced; the source is already released.
        }
        job.CancellationSource = null;
      }
    });

    return Task.FromResult<object?>(ToResponse(job));
  }

  public static Task<object?> GetJobStatusAsync(JsonObject? parameters)
  {
    var jobId = PluginRuntime.GetRequiredString(parameters, "jobId");
    return Task.FromResult<object?>(ToResponse(JobRegistry.Get(jobId)));
  }

  public static Task<object?> CancelJobAsync(JsonObject? parameters)
  {
    var jobId = PluginRuntime.GetRequiredString(parameters, "jobId");
    return Task.FromResult<object?>(ToResponse(JobRegistry.Cancel(jobId)));
  }

  private static IEnumerable<string> ExtractWarnings(object? result)
  {
    if (result is not IDictionary<string, object?> dictionary
      || !dictionary.TryGetValue("warnings", out var warningValue)
      || warningValue is not IEnumerable warnings
      || warningValue is string)
    {
      return [];
    }

    return warnings.Cast<object?>()
      .Select(value => value?.ToString())
      .Where(value => !string.IsNullOrWhiteSpace(value))
      .Cast<string>()
      .ToArray();
  }

  private static Dictionary<string, object?> ToResponse(JobRecord job)
  {
    var stats = JobRegistry.GetStats();
    return new Dictionary<string, object?>
    {
      ["jobId"] = job.JobId,
      ["state"] = job.State,
      ["operation"] = job.Operation,
      ["progressPercent"] = job.ProgressPercent,
      ["currentPhase"] = job.CurrentPhase,
      ["estimatedRemainingSeconds"] = job.EstimatedRemainingSeconds,
      ["result"] = job.Result,
      ["createdAt"] = job.CreatedAt,
      ["completedAt"] = job.CompletedAt,
      ["durationMs"] = job.DurationMs,
      ["requestId"] = job.RequestId,
      ["drawingIdentity"] = job.DrawingIdentity,
      ["failureCategory"] = job.FailureCategory,
      ["cancellationRequested"] = job.CancellationRequested,
      ["warnings"] = job.Warnings.ToArray(),
      ["registry"] = new Dictionary<string, object?>
      {
        ["total"] = stats.Total,
        ["running"] = stats.Running,
        ["capacity"] = stats.Capacity,
        ["terminalRetentionMinutes"] = stats.TerminalRetentionMinutes,
      },
    };
  }
}
