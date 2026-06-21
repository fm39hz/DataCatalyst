namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;

/// <summary>Delegate for field-level merge of a component struct.</summary>
public delegate object MergeComponentDelegate(object parent, object child);

/// <summary>Registry of field-level merge functions for [DataComponent] structs.
/// Populated by SourceGen via [ModuleInitializer].</summary>
public static class ComponentMerger {
	private static readonly Dictionary<Type, MergeComponentDelegate> _mergers = [];

	public static void Register<T>(MergeComponentDelegate merger) where T : struct {
		_mergers[typeof(T)] = merger;
	}

	public static bool TryMerge(Type type, object parent, object child, out object result) {
		if (_mergers.TryGetValue(type, out var merger)) {
			result = merger(parent, child);
			return true;
		}
		result = parent;
		return false;
	}
}
