// fake attribute for backward compatibility with .NET 4.5

#if !NET5_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis
{
    [Flags]
    internal enum DynamicallyAccessedMemberTypes
    {
        All = -1,
        None = 0,
        PublicParameterlessConstructor = 1,
        PublicConstructors = 3,
        NonPublicConstructors = 4,
        PublicMethods = 8,
        NonPublicMethods = 16,
        PublicFields = 32,
        NonPublicFields = 64,
        PublicNestedTypes = 128,
        NonPublicNestedTypes = 256,
        PublicProperties = 512,
        NonPublicProperties = 1024,
        PublicEvents = 2048,
        NonPublicEvents = 4096,
        Interfaces = 8192
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Interface | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter, Inherited = false)]
    internal sealed class DynamicallyAccessedMembersAttribute : Attribute
    {
        private DynamicallyAccessedMemberTypes _memberTypes;

        public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes)
        {
            _memberTypes = memberTypes;
        }

        public DynamicallyAccessedMemberTypes MemberTypes { get { return _memberTypes; } }
    }

    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Class, Inherited = false)]
    internal sealed class RequiresUnreferencedCodeAttribute : Attribute
    {
        public RequiresUnreferencedCodeAttribute(string message)
        {
            Message = message;
        }

        public string Message { get; }
        public string Url { get; set; }
    }

    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Class, Inherited = false)]
    internal sealed class RequiresDynamicCodeAttribute : Attribute
    {
        public RequiresDynamicCodeAttribute(string message)
        {
            Message = message;
        }

        public string Message { get; }
        public string Url { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    internal sealed class UnconditionalSuppressMessageAttribute : Attribute
    {
        public UnconditionalSuppressMessageAttribute(string category, string checkId)
        {
            Category = category;
            CheckId = checkId;
        }

        public string Category { get; }
        public string CheckId { get; }
        public string Justification { get; set; }
        public string MessageId { get; set; }
        public string Scope { get; set; }
        public string Target { get; set; }
    }
}
#endif
