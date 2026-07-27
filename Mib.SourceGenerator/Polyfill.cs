#if !NET5_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter)]
    internal sealed class MaybeNullWhenAttribute(bool returnValue) : Attribute
    {
        public bool ReturnValue { get; } = returnValue;
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    internal sealed class MemberNotNullWhenAttribute : Attribute
    {
        public MemberNotNullWhenAttribute(bool returnValue, string member)
        {
            ReturnValue = returnValue;
            Members = new[] { member };
        }
        public MemberNotNullWhenAttribute(bool returnValue, params string[] members)
        {
            ReturnValue = returnValue;
            Members = members;
        }
        public bool ReturnValue { get; }
        public string[] Members { get; }
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