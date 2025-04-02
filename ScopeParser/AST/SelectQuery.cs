// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class SelectQuery(Token token, FieldSpec fields, SelectSource source, WhereStatement? where) : Source {
      
    public T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitSelectQuery(this);
    }

    public Token Token => token;
      
    public FieldSpec Fields => fields;

    public SelectSource Source => source;

    public WhereStatement? Where => where;
}
      