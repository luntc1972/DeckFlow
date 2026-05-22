using System;
using System.Collections.Generic;

namespace DeckFlow.Web.Tests.Infrastructure;

/// <summary>
/// Disposable helper that captures and restores process-wide environment variables.
/// Subsumes the two prior inline EnvScope helpers in BasicAuthMiddlewareTests and
/// AdminBruteForceTrackerStoreTests. Combine with <c>[Collection("AdminEnvSerial")]</c>
/// on env-mutating test classes to serialize their parallel execution and eliminate
/// the cross-class race on FEEDBACK_ADMIN_USER / FEEDBACK_ADMIN_PASSWORD.
/// </summary>
internal sealed class EnvScope : IDisposable
{
    private readonly Dictionary<string, string?> _previous = new();

    private EnvScope(params string[] keys)
    {
        foreach (var key in keys)
        {
            _previous[key] = Environment.GetEnvironmentVariable(key);
        }
    }

    public static EnvScope Clear(params string[] keys)
    {
        var scope = new EnvScope(keys);
        foreach (var key in keys) Environment.SetEnvironmentVariable(key, null);
        return scope;
    }

    public static EnvScope Set(string name, string value)
    {
        var scope = new EnvScope(name);
        Environment.SetEnvironmentVariable(name, value);
        return scope;
    }

    public static EnvScope Set(string k1, string v1, string k2, string v2)
    {
        var scope = new EnvScope(k1, k2);
        Environment.SetEnvironmentVariable(k1, v1);
        Environment.SetEnvironmentVariable(k2, v2);
        return scope;
    }

    public void Dispose()
    {
        foreach (var (key, value) in _previous)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
