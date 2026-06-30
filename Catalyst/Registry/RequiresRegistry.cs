namespace Catalyst.Registry;

using System;
using System.Collections.Generic;

public sealed class RequiresRegistry : IRequiresRegistry {
	private readonly Dictionary<string, string[]> _requires = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string[]> _suggests = new(StringComparer.OrdinalIgnoreCase);

	public bool Frozen { get; private set; }

	public void Register(string concept, string[] requires, string[] suggests) {
		if (Frozen) {
			throw new InvalidOperationException("Registry frozen");
		}

		_requires[concept] = requires;
		if (suggests.Length > 0) {
			_suggests[concept] = suggests;
		}
	}

	public string[] GetRequired(string concept)
		=> _requires.TryGetValue(concept, out var a) ? a : [];

	public string[] GetSuggested(string concept)
		=> _suggests.TryGetValue(concept, out var a) ? a : [];

	public bool HasConcept(string concept) => _requires.ContainsKey(concept) || _suggests.ContainsKey(concept);

	public IReadOnlyCollection<string> AllConcepts => _requires.Keys;

	public void Freeze() => Frozen = true;
}
