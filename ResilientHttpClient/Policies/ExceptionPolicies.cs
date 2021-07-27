using System;

namespace ResilientHttp.Policies
{
  /// <summary>A policy for determining if an exception is safe to retry.</summary>
  public delegate bool ExceptionPolicy(Exception exception);

  public static class ExceptionPolicies
  {
    public static ExceptionPolicy All  { get; } = _ => true;
    public static ExceptionPolicy None { get; } = _ => false;

    public static ExceptionPolicy OfType<T>()
      where T : Exception => exception => exception is T;
  }
}