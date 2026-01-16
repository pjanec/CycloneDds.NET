#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;
    
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class IsExternalInit { }
    
    [EditorBrowsable(EditorBrowsableState.Never)]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute { }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName)
        {
            FeatureName = featureName;
        }

        public string FeatureName { get; }
        public bool IsOptional { get; init; }

        public const string RefStructs = "RefStructs";
        public const string RequiredMembers = "RequiredMembers";
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute { }
}
#endif
