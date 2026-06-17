namespace DataCatalyst.Abstractions;

using System;

/// <summary>Marks a class as a data plugin with optional dependencies.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DataPluginAttribute : Attribute {
	/// <summary>Plugin types this plugin depends on.</summary>
	public Type[]? DependsOn { get; set; }
}
