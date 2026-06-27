namespace DataCatalyst;

using System;

/// <summary>
/// A type-safe reference to a Being implementing a specific Concept.
/// </summary>
/// <typeparam name="TConcept">The Concept type required by this reference.</typeparam>
public readonly struct Ref<TConcept>(Type beingType) where TConcept : struct, IConcept {
	public Type BeingType { get; } = beingType;

	public static implicit operator Ref<TConcept>(Type beingType) => new(beingType);
	public static implicit operator Type(Ref<TConcept> r) => r.BeingType;

	public override string ToString() => BeingType?.Name ?? "None";
}
