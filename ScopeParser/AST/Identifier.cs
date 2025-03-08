// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class Identifier(Token token, string value) : Source {
      
    public override T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitIdentifier(this);
    }

    public override Token Token => token;
      
    public string Value => value;
}
      