namespace ScopeParser.Backend;

using System.ComponentModel;
using System.Runtime.ConstrainedExecution;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using ScopeParser.Ast;
using ScopeParser.Lexing;
using ScopeParser.Parsing;

public class TypeScriptBackend(ISnippetProvider snippetProvider) : INodeVisitor<string>
{
    public string Visit(Node node) => node.Visit(this);
    private Dictionary<string, int> variableCount = new();
    private List<string> parameterInvokes = new();
    private List<string> parameterLoadValues = new();
    private List<string> parameterSignatures = new();
    private List<string> conditions = new();
    private List<string> recordMappers = new();
    private List<string> importSendAllRecords = new();
    private List<string> importSimpleParameters = new();
    private List<string> interfaceTypes = new();
    private List<string> exports = new();
    private List<string> exportEventHandlers = new();
    private List<string> exportSimpleResults = new();
    private int outputCount = 0;
    private int exportCount = 0;
    private int recordMapperCount = 0;
    private bool isUsingImport = false;

    public string VisitScript(Script node)
    {
        // We need to visit the script first to get all the variables
        var statementsStr = string.Join("\n", node.Statements.Select(Visit).ToList());

        // Then assemble the rest
        var conditionsStr = string.Join("\n", conditions);
        var exportsStr = string.Join(",\n    ", exports);
        var exportAddEventHandlersStr = string.Join("\n", exportEventHandlers);
        var exportSimpleResultsStr = string.Join("\n", exportSimpleResults);
        var importSendAllRecordsStr = string.Join("\n", importSendAllRecords);
        var importSimpleParametersStr = string.Join(", ", importSimpleParameters);
        var interfaceTypesStr = string.Join("\n", interfaceTypes);
        var parameterSignaturesStr = string.Join(",\n", parameterSignatures);
        var paramInvokesStr = string.Join(",\n", parameterInvokes);
        var recordMappersStr = string.Join("\n", recordMappers);
        var useAsExecutable = HandleUseAsExecutable();

        // The main script snippet contains all the boiler plate. Statements are just inserted in its midst
        return snippetProvider.GetSnippet("script",
            ("conditions", conditionsStr),
            ("exports", exportsStr),
            ("exportAddEventHandlers", exportAddEventHandlersStr),
            ("exportCount", exportSimpleResults.Count().ToString()),
            ("exportSimpleResults", exportSimpleResultsStr),
            ("importSendAllRecords", importSendAllRecordsStr),
            ("importSimpleParams", importSimpleParametersStr),
            ("interfaceTypes", interfaceTypesStr),
            ("paramInvokes", paramInvokesStr),
            ("paramSignatures", parameterSignaturesStr),
            ("recordMappers", recordMappersStr),
            ("statements", statementsStr),
            ("useAsExecutable", useAsExecutable)
        );
    }

    private string HandleUseAsExecutable()
    {
        if (!isUsingImport)
        {
            var loadParametersStr = string.Join(",\n", parameterLoadValues);
            var paramInvokesStr = string.Join(",\n", parameterInvokes);
            return snippetProvider.GetSnippet("useAsExecutable",
                ("paramInvokes", paramInvokesStr),
                ("loadParameters", loadParametersStr)
            );
        }
        return snippetProvider.GetSnippet("blockUseAsExecutable");
    }

    // We keep track of variable reuse. Variables are renamed variableName_0, variableName_1, etc.
    // This is to avoid name collisions in the generated code.
    // Technically we could just reuse the same variable, but this is easier to read.
    private void incVariableCount(string name)
    {
        if (!variableCount.ContainsKey(name))
        {
            variableCount[name] = 0;
        }
        else
        {
            variableCount[name]++;
        }
    }

    public string VisitAssignment(Assignment assignment)
    {
        var source = Visit(assignment.Source);

        incVariableCount(assignment.VariableName.Value);
        var variableName = Visit(assignment.VariableName);
        return snippetProvider.GetSnippet("assignment",
            ("variableName", variableName),
            ("source", source),
            ("name", assignment.VariableName.Value) // Passed to name the source
        );
    }

    public string VisitFieldList(FieldList node)
    {
        var fields = node.Fields.Select((field, index) => VisitFieldForFieldSpec(field, $"field_{index}"));
        var headers = string.Join("\n", node.Fields.Select(VisitFieldForHeader));

        // Fields are injected as a string list into the snippet.
        var mapRecord = string.Join(",\n", fields);
        var id = recordMapperCount++;
        var mapper = snippetProvider.GetSnippet("recordMapper",
            ("mapRecord", mapRecord),
            ("mapHeaders", headers),
            ("id", id.ToString())
        );
        recordMappers.Add(mapper);
        return snippetProvider.GetSnippet("fieldList",
            ("id", id.ToString())
        );
    }

    /// <summary>
    /// Visiting a field is slightly different because of aliases
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    private string VisitFieldForFieldSpec(Field field, string defaultName)
    {
        var ns = "undefined";
        var name = defaultName;

        if (field is AliasedField aliasedField)
        {
            name = aliasedField.Alias;
        }
        else if (field is InputField inputField)
        {
            name = inputField.Name;
            ns = inputField.Ns != null ? $"\"{inputField.Ns}\"" : "undefined";
        }

        var value = Visit(field);
        return snippetProvider.GetSnippet("field",
            ("name", name),
            ("namespace", ns),
            ("value", value)
        );
    }

    /// <summary>
    /// Visiting a field is slightly different because of aliases
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    private string VisitFieldForHeader(Field field, int count)
    {
        if (field is AliasedField aliasedField)
        {
            return snippetProvider.GetSnippet("alias",
                ("name", aliasedField.Alias)
            );
        }
        else if (field is InputField inputField)
        {
            if (inputField.Ns != null)
            {
                return snippetProvider.GetSnippet("header",
                    ("name", inputField.Name),
                    ("namespace", inputField.Ns),
                    ("count", count.ToString())
                );
            }
            else
            {
                return snippetProvider.GetSnippet("headerWithoutNamespace",
                    ("name", inputField.Name),
                    ("count", count.ToString())
                );
            }

        }
        return snippetProvider.GetSnippet("alias",
            ("name", $"header_{count}")
        );
    }

    public string VisitInputField(InputField field)
    {
        return snippetProvider.GetSnippet("inputField",
            ("name", field.Name),
            ("namespace", field.Ns != null ? $"\"{field.Ns}\"" : "undefined")
        );
    }

    public string VisitAliasedField(AliasedField field)
    {
        return Visit(field.Field);
    }

    public string VisitFileSource(FileSource node)
    {
        var fieldSpec = Visit(node.FieldSpec);
        var filename = Visit(node.FileName);
        return snippetProvider.GetSnippet("fileSource",
            ("fieldSpec", fieldSpec),
            ("filename", filename)
        );
    }

    public string VisitIdentifier(Identifier identifier)
    {
        if (!variableCount.ContainsKey(identifier.Value))
        {
            throw new ParseError($"Identifier {identifier.Value} is not defined", identifier.Token);
        }
        return "SOURCE__" + identifier.Value + "_" + variableCount[identifier.Value];
    }

    public string VisitOutput(Output node)
    {
        var variableName = getOutputName(outputCount++);
        var source = Visit(node.Source);
        var fileName = Visit(node.OutputFile);
        return snippetProvider.GetSnippet("output",
            ("variableName", variableName),
            ("source", source),
            ("fileName", fileName)
        );
    }

    public string VisitSelectQuery(SelectQuery node)
    {
        var fields = Visit(node.Fields);
        var source = Visit(node.Source);
        var where = node.Where != null ? Visit(node.Where) : "undefined";
        return snippetProvider.GetSnippet("selectQuery",
            ("fields", fields),
            ("source", source),
            ("where", where)
        );
    }

    public string VisitStar(Star node)
    {
        return snippetProvider.GetSnippet("star");
    }

    private string getOutputName(int count)
    {
        return "OUTPUT_FILE__" + count;
    }

    public string VisitWhereStatement(WhereStatement node)
    {
        var condition = Visit(node.Condition);
        return snippetProvider.GetSnippet("whereStatement",
            ("condition", condition)
        );
    }

    public string VisitJoinQuery(JoinQuery node)
    {
        var conditionName = getConditionName(conditions.Count);
        var tsExpression = Visit(node.Condition);
        var condition = snippetProvider.GetSnippet("joinCondition",
            ("name", conditionName),
            ("condition", tsExpression),
            ("token", node.Token.ToString())
        );
        conditions.Add(condition);
        return snippetProvider.GetSnippet("joinQuery",
            ("left", Visit(node.Left)),
            ("right", Visit(node.Right)),
            ("condition", conditionName),
            ("joinType", node.JoinType.ToString())
        );
    }

    private string getConditionName(int number)
    {
        return "condition_" + number;
    }

    public string VisitTsExpression(TsExpression node)
    {
        return node.Expression;
    }

    public string VisitAliasedSource(AliasedSource node)
    {
        var source = Visit(node.Source);
        var alias = node.Alias.Value;
        return snippetProvider.GetSnippet("aliasedSource",
            ("source", source),
            ("alias", alias)
        );
    }

    public string VisitStringLiteral(StringLiteral node)
    {
        return snippetProvider.GetSnippet("stringLiteral",
            ("value", node.Value)
        );
    }

    public string VisitBooleanLiteral(BooleanLiteral node)
    {
        return node.Value ? "true" : "false";
    }

    public string VisitNumberLiteral(NumberLiteral node)
    {
        return node.Value.ToString();
    }

    public string VisitVariableAssignment(VariableAssignment node)
    {
        return snippetProvider.GetSnippet("variableAssignment",
            ("variableName", node.VariableName),
            ("value", Visit(node.Value))
        );
    }

    public string VisitVariableDefinition(VariableDefinition node)
    {
        return snippetProvider.GetSnippet("variableDefinition",
            ("variableName", node.VariableName),
            ("variableType", node.Type),
            ("value", Visit(node.Value))
        );
    }

    public string VisitVariableIdentifier(VariableIdentifier node)
    {
        return node.VariableName;
    }

    public string VisitParam(Param node)
    {
        parameterSignatures.Add(snippetProvider.GetSnippet("paramSignature",
            ("paramName", node.Name)
        ));
        parameterLoadValues.Add(snippetProvider.GetSnippet("paramLoadValue",
            ("paramName", node.Name),
            ("default", node.DefaultValue != null ? Visit(node.DefaultValue) : "undefined")
        ));
        parameterInvokes.Add(node.Name);
        return "";
    }

    public string VisitExport(Export node)
    {
        var exportName = node.ExportName;
        if (exportName == null && node.Source is Identifier alias)
        {
            exportName = alias.Value;
        }
        else
        {
            exportName = $"export_{exportCount++}";
        }
        var name = $"EXPORT_{exportName}";
        var source = Visit(node.Source);
        var exportExport = snippetProvider.GetSnippet("exportExport",
            ("exportName", exportName),
            ("name", name)
        );
        exports.Add(exportExport);
        var exportAddEventHandlers = snippetProvider.GetSnippet("exportAddEventHandlers", ("name", exportName));
        exportEventHandlers.Add(exportAddEventHandlers);
        var exportSimpleResult = snippetProvider.GetSnippet("exportSimpleResult", ("name", exportName));
        exportSimpleResults.Add(exportSimpleResult);
        return snippetProvider.GetSnippet("export",
            ("name", name),
            ("source", source)
        );
    }

    public string VisitImport(Import node)
    {
        var type = "any";
        incVariableCount(node.Name.Value);
        var name = Visit(node.Name);
        if (node.Fields.Count > 0)
        {
            type = $"{node.Name.Value}Record";
            type = type[0].ToString().ToUpper() + type[1..];
            var fields = string.Join("\n  ", node.Fields.Select(Visit));
            interfaceTypes.Add(snippetProvider.GetSnippet("importInterface",
                ("name", type),
                ("fields", fields)
            ));
        }
        isUsingImport = true;
        var parameterName = node.Name.Value == "stream" ? "inputStream" : node.Name.Value; // Rename variable to avoid collision if necessary

        var importExport = snippetProvider.GetSnippet("importExport", ("exportName", node.Name.Value), ("name", name));
        exports.Add(importExport);
        var importSendAllRecordsStr = snippetProvider.GetSnippet("importSimpleSendAllRecords", ("name", parameterName));
        importSendAllRecords.Add(importSendAllRecordsStr);
        var importSimpleParametersStr = snippetProvider.GetSnippet("importSimpleParameters", ("name", parameterName), ("type", type));
        importSimpleParameters.Add(importSimpleParametersStr);
        return snippetProvider.GetSnippet("importDeclaration", ("name", name), ("type", type));
    }

    public string VisitTypedField(TypedField node)
    {
        return snippetProvider.GetSnippet("typedField",
            ("name", node.Name),
            ("type", node.Type)
        );
    }
}