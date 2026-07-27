#if !NET5_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter)]
    internal sealed class MaybeNullWhenAttribute(bool returnValue) : Attribute
    {
        public bool ReturnValue { get; } = returnValue;
    }
}
#endif

#if !NET6_0_OR_GREATER
namespace System
{
    internal static class HashCode
    {
        public static int Combine<T1, T2>(T1 v1, T2 v2)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (v1?.GetHashCode() ?? 0);
                hash = hash * 31 + (v2?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public static int Combine<T1, T2, T3>(T1 v1, T2 v2, T3 v3)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (v1?.GetHashCode() ?? 0);
                hash = hash * 31 + (v2?.GetHashCode() ?? 0);
                hash = hash * 31 + (v3?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public static int Combine<T1, T2, T3, T4>(T1 v1, T2 v2, T3 v3, T4 v4)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (v1?.GetHashCode() ?? 0);
                hash = hash * 31 + (v2?.GetHashCode() ?? 0);
                hash = hash * 31 + (v3?.GetHashCode() ?? 0);
                hash = hash * 31 + (v4?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
#endif