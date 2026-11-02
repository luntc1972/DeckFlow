using System;
using System.Runtime.CompilerServices;

namespace DeckFlow.Web.Tests;

internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Why: inherited developer data paths must not select real data; tests can still set their own paths.
        Environment.SetEnvironmentVariable("MTG_DATA_DIR", null);
        Environment.SetEnvironmentVariable("DECKFLOW_KB_ROOT", null);
    }
}
