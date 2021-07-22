using System;
using System.Collections.Generic;

namespace ResilientHttp.Utilities
{
  /// <summary>Indicates, in a type safe way, a value which may or may not exist (including value and reference types).</summary>
  public readonly struct Optional<T> : IEquatable<Optional<T>>
  {
    public static Optional<T> None => default;

    private readonly T    value;
    private readonly bool hasValue;

    private Optional(T value)
    {
      this.value = value;
      hasValue   = true;
    }

    public bool IsSome => hasValue;
    public bool IsNone => !hasValue;

    public bool HasValue => hasValue;

    public T GetOrDefault(T defaultValue)
    {
      if (hasValue)
      {
        return value;
      }

      return defaultValue;
    }

    public T GetOrThrow()
    {
      if (hasValue)
      {
        return value;
      }

      throw new NullReferenceException($"The optional for {typeof(T)} lacks a value");
    }

    public bool TryGet(out T result)
    {
      if (hasValue)
      {
        result = value;
        return true;
      }

      result = default!;
      return false;
    }

    public Optional<TOther> Select<TOther>(Func<T, TOther> mapper)
    {
      if (hasValue)
      {
        return mapper(value);
      }

      return default;
    }

    public override string ToString()
    {
      if (hasValue)
      {
        if (value != null)
        {
          return value.ToString();
        }

        return "null";
      }

      return "None";
    }

    public bool Equals(Optional<T> other) => EqualityComparer<T>.Default.Equals(value, other.value) && hasValue == other.hasValue;
    public override bool Equals(object? obj) => obj is Optional<T> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(value, hasValue);

    public static bool operator ==(Optional<T> left, Optional<T> right) => left.Equals(right);
    public static bool operator !=(Optional<T> left, Optional<T> right) => !left.Equals(right);

    public static implicit operator Optional<T>(T value)
    {
      if (value != null)
      {
        return new Optional<T>(value);
      }

      return default;
    }
  }
}