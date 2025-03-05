// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;

namespace ScopeParser.Ast;

public class FieldList(Field[] fields) : FieldSpec {
      
    public override void Visit<T>(INodeVisitor<T> visitor) {
        visitor.VisitFieldList(this);
    }
      
    public Field[] Fields => fields;
}
      