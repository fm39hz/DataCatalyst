namespace UniversalDataDriven.Abstractions;

using System;

/// <summary>
///     Declarative tag used by built-in UDDSG plugins to make their contract explicit.
///     The attribute is purely documentation; discovery still happens through <c>[ModuleInitializer]</c>
///     calling <c>UddsgPluginRegistry.Register(...)</c>. Tooling and tests may inspect it, but the
///     pipeline driver does not.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class UddsgPluginAttribute : Attribute {
	public Type Contract { get; }

	public UddsgPluginAttribute(Type contract) {
		Contract = contract;
	}
}
