namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

/// <summary>Merges <paramref name="inherited"/> fields onto <paramref name="current"/> and returns the result.</summary>
/// <param name="current">The current/child component value to merge onto.</param>
/// <param name="inherited">The parent/base component whose non-default fields are applied.</param>
public delegate object MergeComponent(object current, object inherited);

/// <summary>Registry of field-level merge functions for [DataComponent] structs.
/// Populated by SourceGen via [ModuleInitializer].</summary>
public static class ComponentMerger {
	private static readonly Dictionary<Type, MergeComponent> _mergers = [];

	/// <summary>Registers a field-level merge function for a component type.</summary>
	/// <typeparam name="T">Component struct type.</typeparam>
	/// <param name="merger">Delegate that applies non-default inherited fields onto current.</param>
	public static void Register<T>(MergeComponent merger) where T : struct {
		_mergers[typeof(T)] = merger;
	}

	/// <summary>Performs field-level merge of <paramref name="inherited"/> onto <paramref name="current"/>.</summary>
	/// <returns>The merged component. If no merger is registered, returns <paramref name="current"/> unchanged.</returns>
	public static object Merge(Type type, object current, object inherited) {
		if (_mergers.TryGetValue(type, out var merger)) {
			return merger(current, inherited);
		}
		return current;
	}
}
