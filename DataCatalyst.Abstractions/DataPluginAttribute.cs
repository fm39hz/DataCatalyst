namespace DataCatalyst.Abstractions;

using System;

/// <summary>
/// Optional: marks a class as a plugin. Not required — SourceGen auto-discovers any class that implements IPlugin.
/// Use this to specify ordering via Order or for documentation purposes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DataPluginAttribute : Attribute {
	/// <summary>Plugin execution order. Lower values execute first.</summary>
	public int Order { get; set; }
}
