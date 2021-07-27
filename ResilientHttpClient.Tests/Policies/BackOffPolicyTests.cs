using System;
using NUnit.Framework;

namespace ResilientHttp.Policies
{
  public class BackOffPolicyTests
  {
    [Test]
    public void immediate_policy_should_work()
    {
      var policy = BackOffPolicies.Immediate();

      Assert.AreEqual(TimeSpan.Zero, policy(1));
      Assert.AreEqual(TimeSpan.Zero, policy(2));
      Assert.AreEqual(TimeSpan.Zero, policy(3));
      Assert.AreEqual(TimeSpan.Zero, policy(4));
      Assert.AreEqual(TimeSpan.Zero, policy(5));
    }

    [Test]
    public void constant_policy_should_work()
    {
      var duration = TimeSpan.FromMinutes(5);
      var policy   = BackOffPolicies.Constant(duration);

      Assert.AreEqual(duration, policy(1));
      Assert.AreEqual(duration, policy(2));
      Assert.AreEqual(duration, policy(3));
      Assert.AreEqual(duration, policy(4));
      Assert.AreEqual(duration, policy(5));
    }

    [Test]
    public void linear_policy_should_work()
    {
      var duration    = TimeSpan.FromMinutes(1);
      var maxDuration = TimeSpan.FromMinutes(3);
      var policy      = BackOffPolicies.Linear(duration, maxDuration);

      Assert.AreEqual(duration * 1, policy(1));
      Assert.AreEqual(duration * 2, policy(2));
      Assert.AreEqual(duration * 3, policy(3));
      Assert.AreEqual(duration * 3, policy(4));
      Assert.AreEqual(duration * 3, policy(5));
    }

    [Test]
    public void linear_policy_with_jitter_should_work()
    {
      var duration    = TimeSpan.FromMinutes(1);
      var maxDuration = TimeSpan.FromMinutes(3);
      var maxJitter   = TimeSpan.FromSeconds(30);
      var policy      = BackOffPolicies.LinearWithJitter(duration, maxDuration, maxJitter);

      AreRoughlyEqual(duration * 1, policy(1), maxJitter);
      AreRoughlyEqual(duration * 2, policy(2), maxJitter);
      AreRoughlyEqual(duration * 3, policy(3), maxJitter);
      AreRoughlyEqual(duration * 3, policy(4), maxJitter);
      AreRoughlyEqual(duration * 3, policy(5), maxJitter);
    }

    [Test]
    public void exponential_policy_should_work()
    {
      var duration    = TimeSpan.FromMinutes(1);
      var maxDuration = TimeSpan.FromMinutes(3);
      var maxDelay    = TimeSpan.FromMinutes(30);
      var policy      = BackOffPolicies.Exponential(duration, maxDuration, maxDelay);

      Assert.AreEqual(duration * 1, policy(1));
    }

    private static void AreRoughlyEqual(TimeSpan duration, TimeSpan expected, TimeSpan jitter)
    {
      Assert.IsTrue(expected - duration < jitter);
    }
  }
}