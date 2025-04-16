// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class AliasedSource(Token token, Identifier alias, Source source) : AliasableSource {
      
    public T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitAliasedSource(this);
    }

    public Token Token => token;
      
    public Identifier Alias => alias;

    public Source Source => source;
}
      