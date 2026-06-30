namespace Catalyst.SourceGen.Parsing;

using Catalyst.SourceGen.Models;

public interface IOntologyParser {
	public bool CanHandle(in OntologyFile file);
	public void Parse(in OntologyFile file, OntologyBuilder builder);
}
