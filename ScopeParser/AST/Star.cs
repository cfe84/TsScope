// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class Star(Token token) : FieldSpec {
      
    public override T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitStar(this);
    }

    public override Token Token => token;
      

}
      