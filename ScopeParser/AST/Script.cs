// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class Script(Token token, Statement[] statements) : Node {
      
    public T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitScript(this);
    }

    public Token Token => token;
      
    public Statement[] Statements => statements;
}
      