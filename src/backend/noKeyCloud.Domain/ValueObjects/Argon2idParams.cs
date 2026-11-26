namespace noKeyCloud.Domain.ValueObjects;

public sealed record Argon2idParams
{
    private const int MinMemorySizeKiB = 19_456;
    private const int MinIterations = 2;
    private const int MinParallelism = 1;

    public int MemorySizeKiB { get; private set; }
    public int Iterations { get; private set; }
    public int Parallelism { get; private set; }

    public Argon2idParams(int memorySizeKiB, int iterations, int parallelism)
    {
        if (memorySizeKiB < MinMemorySizeKiB)
            throw new ArgumentOutOfRangeException(nameof(memorySizeKiB), $"Memory must be at least {MinMemorySizeKiB} KiB.");
        if (iterations < MinIterations)
            throw new ArgumentOutOfRangeException(nameof(iterations), $"Iterations must be at least {MinIterations}.");
        if (parallelism < MinParallelism)
            throw new ArgumentOutOfRangeException(nameof(parallelism), $"Parallelism must be at least {MinParallelism}.");

        MemorySizeKiB = memorySizeKiB;
        Iterations = iterations;
        Parallelism = parallelism;
    }
    
    public static Argon2idParams Default => new(memorySizeKiB: 65_536, iterations: 3, parallelism: 1);

    private Argon2idParams() { }
}