namespace DataCatalyst.Abstractions;

using System;

/// <summary>
/// Declares an AOT context generation request.
/// Core's Source Generator will generate a partial class with the specified properties
/// for all discovered components.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class AotContextAttribute : Attribute {
	/// <summary>The name of the generated context class.</summary>
	public string ContextName { get; }

	/// <summary>The base class of the generated context.</summary>
	public string BaseType { get; }

	/// <summary>The attribute to apply to each serialized type.</summary>
	public string AttributeType { get; }

	/// <summary>The static method to call to register the default instance.</summary>
	public string RegisterMethod { get; }

	/// <summary>Additional attributes to put on the generated class.</summary>
	public string[]? ExtraClassAttributes { get; set; }

	public AotContextAttribute(string contextName, string baseType, string attributeType, string registerMethod) {
		ContextName = contextName;
		BaseType = baseType;
		AttributeType = attributeType;
		RegisterMethod = registerMethod;
	}
}
