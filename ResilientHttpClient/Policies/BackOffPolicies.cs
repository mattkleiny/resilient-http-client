using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ResilientHttp.Policies
{
  /// <summary>A policy that dictates how long to wait between each successive retry of a <see cref="ConnectionPolicy"/>.</summary>
  public delegate TimeSpan BackOffPolicy(int attempt);

  public static class BackOffPolicies
  {
    private static readonly TimeSpan            MaxJitter = TimeSpan.FromSeconds(2);
    private static readonly ThreadLocal<Random> Random    = new(() => new Random(Environment.TickCount));

    public static BackOffPolicy Immediate()
    {
      return _ => TimeSpan.Zero;
    }

    public static BackOffPolicy Constant(TimeSpan duration)
    {
      return _ => duration;
    }

    public static BackOffPolicy Linear(TimeSpan duration, TimeSpan maxDelay)
    {
      return attempt => Min(TimeSpan.FromMilliseconds(duration.TotalMilliseconds * attempt), maxDelay);
    }

    public static BackOffPolicy LinearWithJitter(TimeSpan duration, TimeSpan maxDelay)
    {
      return attempt => Min(TimeSpan.FromMilliseconds(duration.TotalMilliseconds * attempt) + Jitter(), maxDelay);
    }

    public static BackOffPolicy Exponential(TimeSpan duration, TimeSpan baseDelay, TimeSpan maxDelay)
    {
      return attempt => Min(baseDelay + TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * duration.TotalMilliseconds), maxDelay);
    }

    public static BackOffPolicy ExponentialWithJitter(TimeSpan duration, TimeSpan baseDelay, TimeSpan maxDelay)
    {
      return attempt => Min(baseDelay + TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * duration.TotalMilliseconds) + Jitter(), maxDelay);
    }

    private static TimeSpan Jitter()
    {
      return TimeSpan.FromMilliseconds(Random.Value.Next(0, (int) MaxJitter.TotalMilliseconds));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
  }
}