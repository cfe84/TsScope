// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;

namespace ScopeParser.Ast;

public class Assignment(Identifier variableName, Source source) : Statement {
      
    public override T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitAssignment(this);
    }
      
    public Identifier VariableName => variableName;

    public Source Source => source;
}
      