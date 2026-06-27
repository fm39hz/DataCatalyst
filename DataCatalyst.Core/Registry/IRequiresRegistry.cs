namespace DataCatalyst.Registry;

using System.Collections.Generic;

public interface IRequiresRegistry {
	public void Register(string concept, string[] requires, string[] suggests);
	public string[] GetRequired(string concept);
	public string[] GetSuggested(string concept);
	public bool HasConcept(string concept);
	public IReadOnlyCollection<string> AllConcepts { get; }
	public bool Frozen { get; }
	public void Freeze();
}
