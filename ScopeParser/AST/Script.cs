// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;

namespace ScopeParser.Ast;

public class Script(Statement[] statements) : Node {
      
    public override void Visit<T>(INodeVisitor<T> visitor) {
        visitor.VisitScript(this);
    }
      
    public Statement[] Statements => statements;
}
      