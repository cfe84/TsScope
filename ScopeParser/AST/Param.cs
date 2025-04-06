// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class Param(Token token, string name, ParamDefaultValue? defaultValue) : Statement {
      
    public T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitParam(this);
    }

    public Token Token => token;
      
    public string Name => name;

    public ParamDefaultValue? DefaultValue => defaultValue;
}
      