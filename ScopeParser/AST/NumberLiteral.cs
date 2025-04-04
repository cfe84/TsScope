// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class NumberLiteral(Token token, decimal value) : FieldValue {
      
    public T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitNumberLiteral(this);
    }

    public Token Token => token;
      
    public decimal Value => value;
}
      