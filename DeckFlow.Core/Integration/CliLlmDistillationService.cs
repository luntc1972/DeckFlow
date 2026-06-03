using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Integration;

/// <summary>
/// CLI-backed distillation service for Claude.
/// </summary>
public sealed class CliLlmDistillationService : ILlmDistillationService
{
    internal const string CliCommandEnvironmentKey = "DECKFLOW_LLM_CLI_COMMAND";
    internal const string CliTimeoutEnvironmentKey = "DECKFLOW_LLM_CLI_TIMEOUT_SECONDS";
    internal const string InstructionPlaceholder = "{instruction}";

    private const string ClaudeProvider = "claude";
    private const int MaxRetries = 3;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _provider;
    private readonly Func<CliCommandSpec, string, CancellationToken, Task<string>> _runProcess;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a CLI-backed LLM distillation service for the supplied provider.
    /// </summary>
    /// <param name="provider">Provider name. This phase supports claude.</param>
    public CliLlmDistillationService(string provider)
        : this(provider, processRunnerOverride: null)
    {
    }

    internal CliLlmDistillationService(
        string provider,
        Func<CliCommandSpec, string, CancellationToken, Task<string>>? processRunnerOverride,
        TimeSpan? timeoutOverride = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        _provider = provider.Trim();
        _runProcess = processRunnerOverride ?? RunProcessAsync;
        _timeout = timeoutOverride ?? ReadTimeout();
    }

    /// <inheritdoc />
    public async Task<SummaryResult> SummarizeAsync(
        string transcript,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);

        var payload = await ExtractWithRetryAsync<SummaryPayload>(
            BuildInstruction(DistillationSchemas.SummarySystemPrompt, DistillationSchemas.SummarySchema),
            transcript,
            payload => DistillationValidation.ValidateSummary(payload.Summary),
            cancellationToken).ConfigureAwait(false);

        return new SummaryResult(payload.Summary, new TokenUsage(0, 0));
    }

    /// <inheritdoc />
    public async Task<ClipsResult> ExtractClipsAsync(
        string transcript,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);

        var payload = await ExtractWithRetryAsync<ClipsPayload>(
            BuildInstruction(DistillationSchemas.ClipsSystemPrompt, DistillationSchemas.ClipsSchema),
            transcript,
            payload => DistillationValidation.ValidateClips(payload.Clips),
            cancellationToken).ConfigureAwait(false);

        return new ClipsResult(payload.Clips, new TokenUsage(0, 0));
    }

    /// <inheritdoc />
    public async Task<TagsResult> InferTagsAsync(
        string transcript,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);

        var payload = await ExtractWithRetryAsync<TagsPayload>(
            BuildInstruction(DistillationSchemas.TagsSystemPrompt, DistillationSchemas.TagsSchema),
            transcript,
            DistillationValidation.ValidateTags,
            cancellationToken).ConfigureAwait(false);

        return new TagsResult(
            payload.Archetype,
            payload.Bracket,
            payload.CardCategory,
            new TokenUsage(0, 0));
    }

    internal CliCommandSpec BuildCommandSpec(string instruction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);
        if (!string.Equals(_provider, ClaudeProvider, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Unsupported CLI distillation provider '{_provider}'.");
        }

        var overrideValue = Environment.GetEnvironmentVariable(CliCommandEnvironmentKey);
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            return BuildOverrideCommandSpec(overrideValue, instruction);
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return new CliCommandSpec(
                "claude",
                ["-p", instruction, "--output-format", "json", "--allowedTools", string.Empty],
                CliEnvelopeKind.ClaudeJson);
        }

        throw new InvalidOperationException(
            $"{CliCommandEnvironmentKey} must be set as a JSON array with one {InstructionPlaceholder} placeholder, "
            + "for example [\"wsl.exe\",\"claude\",\"-p\",\"{instruction}\",\"--output-format\",\"json\",\"--allowedTools\",\"\"] "
            + "or [\"cmd.exe\",\"/c\",\"claude.cmd\",\"-p\",\"{instruction}\",\"--output-format\",\"json\",\"--allowedTools\",\"\"]");
    }

    private static CliCommandSpec BuildOverrideCommandSpec(string overrideValue, string instruction)
    {
        string[] parts;
        try
        {
            parts = JsonSerializer.Deserialize<string[]>(overrideValue)
                ?? throw new JsonException("Override deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"{CliCommandEnvironmentKey} must be a JSON array of string arguments with exactly one {InstructionPlaceholder} placeholder.",
                ex);
        }

        if (parts.Length == 0)
        {
            throw new InvalidOperationException($"{CliCommandEnvironmentKey} must contain at least one element for the executable name.");
        }

        if (parts.Any(part => part is null))
        {
            throw new InvalidOperationException($"{CliCommandEnvironmentKey} must be a JSON array of strings; null elements are not supported.");
        }

        if (string.IsNullOrWhiteSpace(parts[0]))
        {
            throw new InvalidOperationException($"{CliCommandEnvironmentKey} element 0 must be the executable name.");
        }

        var placeholderIndexes = parts
            .Select((part, index) => (part, index))
            .Where(item => string.Equals(item.part, InstructionPlaceholder, StringComparison.Ordinal))
            .Select(item => item.index)
            .ToArray();
        if (placeholderIndexes.Length != 1 || placeholderIndexes[0] == 0)
        {
            throw new InvalidOperationException(
                $"{CliCommandEnvironmentKey} must contain exactly one {InstructionPlaceholder} placeholder in the argument list.");
        }

        var arguments = parts[1..].ToArray();
        arguments[placeholderIndexes[0] - 1] = instruction;
        return new CliCommandSpec(parts[0], arguments, CliEnvelopeKind.ClaudeJson);
    }

    private async Task<T> ExtractWithRetryAsync<T>(
        string instruction,
        string transcript,
        Action<T> validate,
        CancellationToken cancellationToken)
    {
        var spec = BuildCommandSpec(instruction);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(_timeout);

        Exception? last = null;
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var stdout = await _runProcess(spec, transcript, linkedCts.Token).ConfigureAwait(false);
                var modelText = ExtractModelText(spec.EnvelopeKind, stdout);
                var json = ExtractBalancedJsonObject(FenceStrip(modelText));
                var payload = JsonSerializer.Deserialize<T>(json, JsonOpts)
                    ?? throw new InvalidOperationException("CLI JSON deserialized to null.");
                validate(payload);
                return payload;
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new InvalidOperationException($"CLI extraction failed after {MaxRetries} attempts.", last);
    }

    private static async Task<string> RunProcessAsync(
        CliCommandSpec spec,
        string stdinBody,
        CancellationToken linkedToken)
    {
        var startInfo = new ProcessStartInfo(spec.FileName)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in spec.ArgumentList)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"'{spec.FileName}' process failed to start.");
        using var killReg = linkedToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        });
        var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedToken);
        var stderrTask = process.StandardError.ReadToEndAsync(linkedToken);

        try
        {
            await process.StandardInput.BaseStream.WriteAsync(Encoding.UTF8.GetBytes(stdinBody).AsMemory(), linkedToken).ConfigureAwait(false);
            await process.StandardInput.BaseStream.FlushAsync(linkedToken).ConfigureAwait(false);
            process.StandardInput.Close();
            await process.WaitForExitAsync(linkedToken).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"'{spec.FileName}' exited with code {process.ExitCode}: {ProcessOutput.Tail(stderr)}");
            }

            return stdout;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    private static string ExtractModelText(CliEnvelopeKind kind, string stdout)
    {
        if (kind == CliEnvelopeKind.Raw)
        {
            return stdout.Trim();
        }

        using var envelope = JsonDocument.Parse(stdout);
        var root = envelope.RootElement;
        if (root.TryGetProperty("is_error", out var isError)
            && isError.ValueKind == JsonValueKind.True)
        {
            throw new InvalidOperationException("claude returned is_error=true.");
        }

        if (!root.TryGetProperty("result", out var result)
            || result.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("claude result field is null or missing.");
        }

        var text = result.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("claude result field is empty.");
        }

        return text;
    }

    private static string FenceStrip(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstLineEnd = trimmed.IndexOf('\n', StringComparison.Ordinal);
        var closingFenceStart = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (firstLineEnd < 0 || closingFenceStart <= firstLineEnd)
        {
            return trimmed;
        }

        return trimmed[(firstLineEnd + 1)..closingFenceStart].Trim();
    }

    private static string ExtractBalancedJsonObject(string text)
    {
        var start = -1;
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (current == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (current == '"')
            {
                inString = true;
                continue;
            }

            if (current == '{')
            {
                if (depth == 0)
                {
                    start = index;
                }

                depth++;
                continue;
            }

            if (current == '}' && depth > 0)
            {
                depth--;
                if (depth == 0 && start >= 0)
                {
                    return text[start..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException("No balanced JSON object found.");
    }

    private static string BuildInstruction(string systemPrompt, string schema)
        => systemPrompt
            + Environment.NewLine
            + Environment.NewLine
            + "Output ONLY valid JSON matching this exact schema. Do not include markdown fences or explanations:"
            + Environment.NewLine
            + schema;

    private static TimeSpan ReadTimeout()
    {
        var timeoutValue = Environment.GetEnvironmentVariable(CliTimeoutEnvironmentKey);
        if (string.IsNullOrWhiteSpace(timeoutValue))
        {
            return DefaultTimeout;
        }

        if (!double.TryParse(timeoutValue, out var seconds) || seconds <= 0)
        {
            throw new InvalidOperationException($"{CliTimeoutEnvironmentKey} must be a positive number of seconds.");
        }

        return TimeSpan.FromSeconds(seconds);
    }

}
