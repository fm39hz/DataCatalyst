using System.Runtime.CompilerServices;
using DataCatalyst.Ontology;

namespace DataCatalyst.Ontology.Parsers;

internal static class XmlParserRegistration {
	[ModuleInitializer]
	internal static void Register() {
		ParserRegistry.Register(new XmlOntologyParser());
	}
}
