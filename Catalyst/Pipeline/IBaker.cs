namespace Catalyst.Pipeline;

using System;
using Catalyst.Knowledge;

/// <summary>
/// A non-generic base interface for a database baker.
/// </summary>
public interface IBaker {
	/// <summary>
	/// The source aspect type that this baker compiles (e.g. StateGroup).
	/// </summary>
	public Type SourceAspectType { get; }

	/// <summary>
	/// The final compiled type produced by this baker (e.g. BakedStateGroup).
	/// </summary>
	public Type BakedType { get; }

	/// <summary>
	/// Bakes/compiles a single aspect instance into its optimized runtime representation.
	/// </summary>
	public object Bake(string beingKey, object sourceAspect, Knowledge knowledge, DiagnosticBag diagnostics);
}

/// <summary>
/// A generic interface representing a pipeline baker.
/// </summary>
/// <typeparam name="TSource">The raw aspect type to compile.</typeparam>
/// <typeparam name="TBaked">The final compiled runtime type.</typeparam>
public interface IBaker<TSource, TBaked> : IBaker where TSource : struct {
	Type IBaker.SourceAspectType => typeof(TSource);
	Type IBaker.BakedType => typeof(TBaked);

	object IBaker.Bake(string beingKey, object sourceAspect, Knowledge knowledge, DiagnosticBag diagnostics)
		=> Bake(beingKey, (TSource)sourceAspect, knowledge, diagnostics)!;

	/// <summary>
	/// Bakes/compiles a single aspect instance into its optimized runtime representation.
	/// </summary>
	public TBaked Bake(string beingKey, TSource source, Knowledge knowledge, DiagnosticBag diagnostics);
}
