// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class Assignment(Token token, Identifier variableName, Source source) : Statement {
      
    public override T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitAssignment(this);
    }

    public override Token Token => token;
      
    public Identifier VariableName => variableName;

    public Source Source => source;
}
      