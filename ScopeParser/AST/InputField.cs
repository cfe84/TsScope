// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class InputField(Token token, string name, string? ns) : Field {
      
    public T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitInputField(this);
    }

    public Token Token => token;
      
    public string Name => name;

    public string? Ns => ns;
}
      