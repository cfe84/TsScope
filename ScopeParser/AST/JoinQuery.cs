// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class JoinQuery(Token token, SelectSource left, Source right, JoinType joinType, string condition) : SelectSource {
      
    public override T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitJoinQuery(this);
    }

    public override Token Token => token;
      
    public SelectSource Left => left;

    public Source Right => right;

    public JoinType JoinType => joinType;

    public string Condition => condition;
}
      