namespace FM39hz.DataCatalyst.Abstractions;

using System;

/// <summary>
///     Declarative tag used by built-in DataCatalyst plugins to make their contract explicit.
///     The attribute is purely documentation; discovery still happens through <c>[ModuleInitializer]</c>
///     calling <c>DcPluginRegistry.Register(...)</c>. Tooling and tests may inspect it, but the
///     pipeline driver does not.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DcPluginAttribute(Type contract) : Attribute {
	public Type Contract { get; } = contract;
}
