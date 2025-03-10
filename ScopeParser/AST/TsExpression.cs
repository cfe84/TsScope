// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class TsExpression(Token token, string expression) : Node {
      
    public override T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitTsExpression(this);
    }

    public override Token Token => token;
      
    public string Expression => expression;
}
      