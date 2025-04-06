// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class Output(Token token, Source source, StringValue outputFile) : Statement {
      
    public T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitOutput(this);
    }

    public Token Token => token;
      
    public Source Source => source;

    public StringValue OutputFile => outputFile;
}
      