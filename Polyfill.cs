namespace System.Runtime.CompilerServices {
	internal sealed class IsExternalInit {
	}
}

#if !NET6_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis {
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
	internal sealed class RequiresUnreferencedCodeAttribute(string message) : Attribute {
		public string Message { get; } = message;
		public string? Url { get; set; }
	}

	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
	internal sealed class RequiresDynamicCodeAttribute(string message) : Attribute {
		public string Message { get; } = message;
		public string? Url { get; set; }
	}
}
#endif
