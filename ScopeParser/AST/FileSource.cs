// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class FileSource(Token token, FieldSpec fieldSpec, string fileName) : Source {
      
    public T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitFileSource(this);
    }

    public Token Token => token;
      
    public FieldSpec FieldSpec => fieldSpec;

    public string FileName => fileName;
}
      