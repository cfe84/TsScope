// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;

namespace ScopeParser.Ast;

public class FileSource(FieldSpec fieldSpec, string fileName) : Source {
      
    public override void Visit<T>(INodeVisitor<T> visitor) {
        visitor.VisitFileSource(this);
    }
      
    public FieldSpec FieldSpec => fieldSpec;

    public string FileName => fileName;
}
      