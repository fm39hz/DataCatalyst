namespace DataCatalyst.Plugins.StateEngine;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public sealed class StateMachineGenerator : IIncrementalGenerator {
	public void Initialize(IncrementalGeneratorInitializationContext context) { }
}
