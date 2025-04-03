namespace ScopeParser.Backend;

using System.Text.RegularExpressions;
using ScopeParser.Ast;
using ScopeParser.Lexing;

public class TypeScriptBackend(ISnippetProvider snippetProvider) : INodeVisitor<string>
{
    public string Visit(Node node) => node.Visit(this);
    private Dictionary<string, int> variableCount = new();
    private List<string> conditions = new();
    private List<string> recordMappers = new();
    private int outputCount = 0;
    private int recordMapperCount = 0;

    public string VisitScript(Script node)
    {
        var statements = node.Statements.Select(Visit).ToList();
        var conditionsStr = string.Join("\n", conditions);
        var recordMappersStr = string.Join("\n", recordMappers);
        // The main script snippet contains all the boiler plate. Statements are just inserted in its midst
        return snippetProvider.GetSnippet("script",
            ("statements", string.Join("\n", statements)),
            ("conditions", conditionsStr),
            ("recordMappers", recordMappersStr)
        );
    }

    public string VisitAssignment(Assignment assignment)
    {
        var source = Visit(assignment.Source);
        // We keep track of variable reuse. Variables are renamed variableName_0, variableName_1, etc.
        // This is to avoid name collisions in the generated code.
        // Technically we could just reuse the same variable, but this is easier to read.
        if (!variableCount.ContainsKey(assignment.VariableName.Value))
        {
            variableCount[assignment.VariableName.Value] = 0;
        }
        else
        {
            variableCount[assignment.VariableName.Value]++;
        }
        var variableName = Visit(assignment.VariableName);
        return snippetProvider.GetSnippet("assignment",
            ("variableName", variableName),
            ("source", source),
            ("name", assignment.VariableName.Value) // Passed to name the source
        );
    }

    public string VisitInputField(InputField field)
    {
        return snippetProvider.GetSnippet("inputField",
            ("name", field.Name),
            ("namespace", field.Ns != null ? $"\"{field.Ns}\"" : "undefined")
        );
    }

    public string VisitFieldList(FieldList node)
    {
        var fields = node.Fields.Select(Visit).ToList();
        // Fields are injected as a string list into the snippet.
        var mapRecord = string.Join(",\n", fields);
        var id = recordMapperCount++;
        var mapper = snippetProvider.GetSnippet("recordMapper",
            ("mapRecord", mapRecord),
            ("id", id.ToString())
        );
        recordMappers.Add(mapper);
        return snippetProvider.GetSnippet("fieldList",
            ("id", id.ToString())
        );
    }

    public string VisitFileSource(FileSource node)
    {
        var fieldSpec = Visit(node.FieldSpec);
        return snippetProvider.GetSnippet("fileSource",
            ("fieldSpec", fieldSpec),
            ("filename", node.FileName)
        );
    }

    public string VisitIdentifier(Identifier identifier)
    {
        return identifier.Value + "_" + variableCount[identifier.Value];
    }

    public string VisitOutput(Output node)
    {
        var variableName = getOutputName(outputCount++);
        var source = Visit(node.Source);
        return snippetProvider.GetSnippet("output",
            ("variableName", variableName),
            ("source", source),
            ("fileName", node.OutputFile)
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
        return "output_" + count;
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

    public string VisitAliasedField(AliasedField node)
    {
        throw new NotImplementedException();
    }
}