namespace DataCatalyst.Registry;

using System;
using System.Collections.Generic;

public static class RequiresRegistry {
	private static readonly Dictionary<string, string[]> _requires = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<string, string[]> _suggests = new(StringComparer.OrdinalIgnoreCase);
	private static bool _frozen;

	public static void Register(string concept, string[] requires, string[] suggests) {
		if (_frozen) throw new InvalidOperationException("Registry frozen");
		_requires[concept] = requires;
		if (suggests.Length > 0) _suggests[concept] = suggests;
	}

	public static string[] GetRequired(string concept)
		=> _requires.TryGetValue(concept, out var a) ? a : [];

	public static string[] GetSuggested(string concept)
		=> _suggests.TryGetValue(concept, out var a) ? a : [];

	public static bool HasConcept(string concept) => _requires.ContainsKey(concept);

	public static IReadOnlyCollection<string> AllConcepts => _requires.Keys;

	internal static void Freeze() => _frozen = true;
}
