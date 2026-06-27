using System.Collections.Generic;

namespace DataCatalyst.Ontology;

public static class ParserRegistry {
	private static readonly List<IOntologyParser> _parsers = [];
	private static readonly object _lock = new();

	public static void Register(IOntologyParser parser) {
		lock (_lock) {
			_parsers.Add(parser);
		}
	}

	public static IReadOnlyList<IOntologyParser> GetParsers() {
		lock (_lock) {
			return [.. _parsers];
		}
	}

	public static void Clear() {
		lock (_lock) {
			_parsers.Clear();
		}
	}
}
