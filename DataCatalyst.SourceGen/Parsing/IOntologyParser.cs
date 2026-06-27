namespace DataCatalyst.SourceGen.Parsing;

using DataCatalyst.SourceGen.Models;

public interface IOntologyParser {
	public bool CanHandle(in OntologyFile file);
	public void Parse(in OntologyFile file, OntologyBuilder builder);
}
