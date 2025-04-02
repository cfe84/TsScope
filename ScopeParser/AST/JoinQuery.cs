// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class JoinQuery(Token token, SelectSource left, AliasableSource right, JoinType joinType, TsExpression condition) : SelectSource {
      
    public T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitJoinQuery(this);
    }

    public Token Token => token;
      
    public SelectSource Left => left;

    public AliasableSource Right => right;

    public JoinType JoinType => joinType;

    public TsExpression Condition => condition;
}
      