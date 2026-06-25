namespace DataCatalyst.Registry;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using DataCatalyst.Storage;

/// <summary>Global registry of being types and their pool factories.
/// Populated by SourceGen-generated ModuleInitializer code at assembly load.
/// Frozen after pipeline build to prevent mutation.</summary>
public static class BeingRegistry {
	public readonly record struct Record(Type BeingType, Type[] Concepts);

	private static readonly List<Record> _beings = [];
	private static readonly Dictionary<Type, Func<IStoragePool>> _poolFactories = [];
	private static FrozenDictionary<Type, Func<IStoragePool>>? _frozenPoolFactories;
	private static bool _frozen;

	public static void Register<TBeing>(params ReadOnlySpan<Type> concepts)
		where TBeing : struct, IBeing {
		if (_frozen) {
			throw new InvalidOperationException("Registry frozen after pipeline build");
		}

		_beings.Add(new(typeof(TBeing), concepts.ToArray()));
	}

	public static void RegisterPool(Type conceptType, Func<IStoragePool> factory) {
		if (_frozen) {
			throw new InvalidOperationException("Registry frozen after pipeline build");
		}

		_poolFactories[conceptType] = factory;
	}

	public static IStoragePool? CreatePool(Type conceptType) {
		if (_frozenPoolFactories != null) {
			return _frozenPoolFactories.TryGetValue(conceptType, out var f) ? f() : null;
		}

		return _poolFactories.TryGetValue(conceptType, out var factory) ? factory() : null;
	}

	public static IReadOnlyList<Record> All => _beings;

	internal static void Freeze() {
		_frozen = true;
		_frozenPoolFactories = _poolFactories.ToFrozenDictionary();
	}
}
