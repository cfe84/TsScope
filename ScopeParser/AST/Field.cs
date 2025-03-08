// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class Field(Token token, string name, string? ns) : Node {
      
    public override T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitField(this);
    }

    public override Token Token => token;
      
    public string Name => name;

    public string? Ns => ns;
}
      