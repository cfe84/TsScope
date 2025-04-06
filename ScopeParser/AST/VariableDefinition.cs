// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class VariableDefinition(Token token, string variableName, string type, VariableValue value) : Statement {
      
    public T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitVariableDefinition(this);
    }

    public Token Token => token;
      
    public string VariableName => variableName;

    public string Type => type;

    public VariableValue Value => value;
}
      