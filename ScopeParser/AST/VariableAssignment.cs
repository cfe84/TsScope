// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class VariableAssignment(Token token, string variableName, VariableValue value) : Statement {
      
    public T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitVariableAssignment(this);
    }

    public Token Token => token;
      
    public string VariableName => variableName;

    public VariableValue Value => value;
}
      