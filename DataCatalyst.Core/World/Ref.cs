namespace DataCatalyst;

using System;

public readonly struct Ref<TConcept>(Type beingType) where TConcept : struct, IConcept {
	public Type BeingType { get; } = beingType ?? throw new ArgumentNullException(nameof(beingType));

	public bool IsValid => BeingType != null;

	public override string ToString() => BeingType?.Name ?? "None";
}
