// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;

namespace ScopeParser.Ast;

public class SelectQuery(FieldSpec fields, Source source) : Source {
      
    public override void Visit<T>(INodeVisitor<T> visitor) {
        visitor.VisitSelectQuery(this);
    }
      
    public FieldSpec Fields => fields;

    public Source Source => source;
}
      