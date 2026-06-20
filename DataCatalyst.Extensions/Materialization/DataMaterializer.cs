namespace DataCatalyst.Extensions.Materialization;

using System;
using System.Collections.Generic;
using DataCatalyst.Core;

/// <summary>Interface for AOT-safe component materialization from DataEntry to a generic target.</summary>
public interface IComponentMaterializer<TTarget> {
	/// <summary>Materializes component if it exists in the data entry.</summary>
	public void Materialize(DataEntry entry, TTarget target);
}

/// <summary>Generic materializer for a specific component type and target.</summary>
public sealed class ComponentMaterializer<TComponent, TTarget>(Action<TTarget, TComponent> apply) : IComponentMaterializer<TTarget>
	where TComponent : struct {

	private readonly Action<TTarget, TComponent> _apply = apply ?? throw new ArgumentNullException(nameof(apply));

	/// <inheritdoc />
	public void Materialize(DataEntry entry, TTarget target) {
		if (entry.TryGet<TComponent>(out var comp)) {
			_apply(target, comp);
		}
	}
}

/// <summary>Registry and dispatcher for generic data materialization.</summary>
public sealed class DataMaterializer<TTarget> {
	private readonly List<IComponentMaterializer<TTarget>> _materializers = [];

	/// <summary>Registers a component type and its application action.</summary>
	public void Register<TComponent>(Action<TTarget, TComponent> apply) where TComponent : struct => _materializers.Add(new ComponentMaterializer<TComponent, TTarget>(apply));

	/// <summary>Materializes all registered components from the DataEntry to the target.</summary>
	public void Materialize(DataEntry entry, TTarget target) {
#if NET8_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(entry);
#else
		if (entry == null) {
			throw new ArgumentNullException(nameof(entry));
		}
#endif

		for (var i = 0; i < _materializers.Count; i++) {
			_materializers[i].Materialize(entry, target);
		}
	}

	/// <summary>Clears all registered materializers.</summary>
	public void Clear() => _materializers.Clear();
}
