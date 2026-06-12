using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)] // Why: Core.Tests uses per-test SQLite files, but xUnit collection parallelism still causes sporadic SQLite pool/file contention under WSL.
