// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;

namespace ScopeParser.Ast;

public class WhereStatement(string condition) : Node {
      
    public override T Visit<T>(INodeVisitor<T> visitor) {
        return visitor.VisitWhereStatement(this);
    }
      
    public string Condition => condition;
}
      