namespace Catalyst.Registry;

using System;
using System.Collections.Generic;
using Catalyst.Storage;

public interface IBeingRegistry {
	public readonly record struct Record(Type BeingType, Type[] Concepts);

	public void Register<TBeing>(params ReadOnlySpan<Type> concepts) where TBeing : struct, IBeing;
	public void RegisterPool(Type conceptType, Func<ITypedStoragePool> factory);
	public ITypedStoragePool? CreatePool(Type conceptType);
	public IReadOnlyList<Record> All { get; }
	public bool Frozen { get; }
	public void Freeze();
}
