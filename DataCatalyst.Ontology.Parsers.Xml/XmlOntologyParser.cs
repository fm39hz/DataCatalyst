using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using DataCatalyst.Ontology;

namespace DataCatalyst.Ontology.Parsers;

public sealed class XmlOntologyParser : IOntologyParser {
	public bool CanHandle(in OntologyFile file) {
		var ext = Path.GetExtension(file.FileName);
		if (!ext.Equals(".xml", StringComparison.OrdinalIgnoreCase)) return false;
		try {
			var doc = XDocument.Parse(file.Content);
			var root = doc.Root;
			if (root == null) return false;
			if (root.Name.LocalName.Equals("ontology", StringComparison.OrdinalIgnoreCase)) return true;
			return root.Element("concepts") != null || root.Element("aspects") != null;
		}
		catch { return false; }
	}

	public void Parse(in OntologyFile file, OntologyBuilder builder) {
		try {
			var doc = XDocument.Parse(file.Content);
			var root = doc.Root;
			if (root == null) return;

			var aspRoot = root.Element("aspects");
			if (aspRoot != null) {
				foreach (var aspEl in aspRoot.Elements()) {
					var aName = aspEl.Name.LocalName;
					if (builder.AspectFields.ContainsKey(aName)) continue;
					var fieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					foreach (var field in aspEl.Elements("field")) {
						var fName = field.Attribute("name")?.Value;
						var fType = field.Attribute("type")?.Value;
						if (fName != null && fType != null) fieldMap[fName] = fType;
					}
					if (fieldMap.Count > 0) builder.AddAspectFields(aName, fieldMap);
				}
			}

			var conceptsEl = root.Element("concepts");
			if (conceptsEl == null) return;

			foreach (var conceptEl in conceptsEl.Elements()) {
				var cName = conceptEl.Name.LocalName;
				if (builder.Requires.ContainsKey(cName) || builder.Suggests.ContainsKey(cName)) continue;
				var reqAttr = conceptEl.Attribute("requires")?.Value;
				if (!string.IsNullOrEmpty(reqAttr))
					builder.AddRequires(cName, [.. reqAttr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)]);
				var sugAttr = conceptEl.Attribute("suggests")?.Value;
				if (!string.IsNullOrEmpty(sugAttr))
					builder.AddSuggests(cName, [.. sugAttr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)]);
				if (!builder.Requires.ContainsKey(cName) && !builder.Suggests.ContainsKey(cName))
					builder.AddRequires(cName);
			}
		}
		catch { }
	}
}
