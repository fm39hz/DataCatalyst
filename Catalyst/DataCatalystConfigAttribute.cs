namespace Catalyst.Abstractions;

using System;

/// <summary>
/// Per-source config for Catalyst SourceGen.
/// Apply multiple for different data directories.
/// Type-safe — attributes passed as System.Type, not strings.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class CatalystConfigAttribute(string sourcePath = "") : Attribute {
	/// <summary>Path prefix this config applies to. Empty = all sources.</summary>
	public string SourcePath { get; } = sourcePath;

	/// <summary>Namespace for generated components.</summary>
	public string Namespace { get; set; } = "Catalyst.Generated";

	/// <summary>Additional attributes to inject on generated components for this source.</summary>
	public Type[]? Attributes { get; set; }
}
