namespace Catalyst.Registry;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Catalyst.Storage;

public sealed class BeingRegistry : IBeingRegistry {
	private readonly List<IBeingRegistry.Record> _beings = [];
	private readonly Dictionary<Type, Func<ITypedStoragePool>> _poolFactories = [];
	private FrozenDictionary<Type, Func<ITypedStoragePool>>? _frozenPoolFactories;

	public bool Frozen { get; private set; }

	public void Register<TBeing>(params ReadOnlySpan<Type> concepts)
		where TBeing : struct, IBeing {
		if (Frozen) {
			throw new InvalidOperationException("Registry frozen after pipeline build");
		}

		_beings.Add(new IBeingRegistry.Record(typeof(TBeing), concepts.ToArray()));
	}

	public void RegisterPool(Type conceptType, Func<ITypedStoragePool> factory) {
		if (Frozen) {
			throw new InvalidOperationException("Registry frozen after pipeline build");
		}

		_poolFactories[conceptType] = factory;
	}

	public ITypedStoragePool? CreatePool(Type conceptType) {
		if (_frozenPoolFactories != null) {
			return _frozenPoolFactories.TryGetValue(conceptType, out var f) ? f() : null;
		}

		return _poolFactories.TryGetValue(conceptType, out var factory) ? factory() : null;
	}

	public IReadOnlyList<IBeingRegistry.Record> All => _beings;

	public void Freeze() {
		Frozen = true;
		_frozenPoolFactories = _poolFactories.ToFrozenDictionary();
	}
}
