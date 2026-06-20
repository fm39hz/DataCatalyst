namespace DataCatalyst.Abstractions;

using System;

/// <summary>
/// Optional: marks a class as a plugin. Not required — SourceGen auto-discovers any class that implements IPlugin.
/// Use DependsOn for topological ordering.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DataPluginAttribute : Attribute {
	/// <summary>Plugin types this plugin depends on. Dependencies are resolved first.</summary>
	public Type[]? DependsOn { get; set; }
}
