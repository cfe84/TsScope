// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class Import(Token token, Identifier name, List<TypedField> fields) : Statement {
      
    public T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitImport(this);
    }

    public Token Token => token;
      
    public Identifier Name => name;

    public List<TypedField> Fields => fields;
}
      