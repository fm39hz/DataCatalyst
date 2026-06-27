using System.Runtime.CompilerServices;
using DataCatalyst.Ontology;

namespace DataCatalyst.Ontology.Parsers;

internal static class JsonParserRegistration {
	[ModuleInitializer]
	internal static void Register() {
		ParserRegistry.Register(new JsonOntologyParser());
	}
}
