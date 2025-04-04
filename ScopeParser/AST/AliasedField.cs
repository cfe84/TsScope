// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class AliasedField(Token token, FieldValue field, string alias) : Field {
      
    public T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitAliasedField(this);
    }

    public Token Token => token;
      
    public FieldValue Field => field;

    public string Alias => alias;
}
      