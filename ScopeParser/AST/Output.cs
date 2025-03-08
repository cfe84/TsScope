// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public class Output(Token token, Source source, string outputFile) : Statement {
      
    public override T Visit<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitOutput(this);
    }

    public override Token Token => token;
      
    public Source Source => source;

    public string OutputFile => outputFile;
}
      