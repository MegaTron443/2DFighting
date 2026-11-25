public struct DeterministicRng
{
    public const uint DefaultSeed = 0x6D2B79F5u;

    public uint State { get; private set; }

    public DeterministicRng(uint seed)
    {
        State = seed == 0u ? DefaultSeed : seed;
    }

    public void Seed(uint seed)
    {
        State = seed == 0u ? DefaultSeed : seed;
    }

    public void RestoreState(uint state)
    {
        State = state == 0u ? DefaultSeed : state;
    }

    public uint NextUInt()
    {
        uint x = State;
        if (x == 0u) x = DefaultSeed;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        State = x;
        return x;
    }

    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 1) return 0;
        return (int)(NextUInt() % (uint)maxExclusive);
    }

    public int NextRange(int minInclusive, int maxInclusive)
    {
        if (maxInclusive <= minInclusive) return minInclusive;
        uint span = (uint)((long)maxInclusive - minInclusive + 1L);
        return (int)(minInclusive + (NextUInt() % span));
    }

    public double NextDouble()
    {
        return (NextUInt() >> 8) * (1.0 / 16777216.0);
    }
}
