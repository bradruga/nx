using NUnit.Framework;

// Fixtures run in parallel; tests within a fixture stay serial. Each test gets its own browser context and page
// from PageTest, so viewport changes never leak. Override the worker count with -- NUnit.NumberOfTestWorkers=N.
[assembly: Parallelizable(ParallelScope.Fixtures)]
[assembly: LevelOfParallelism(6)]
