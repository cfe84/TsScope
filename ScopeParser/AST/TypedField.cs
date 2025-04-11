// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class TypedField(Token token, string name, string type) : Node {
      
    public T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitTypedField(this);
    }

    public Token Token => token;
      
    public string Name => name;

    public string Type => type;
}
      