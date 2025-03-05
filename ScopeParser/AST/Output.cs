// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;

namespace ScopeParser.Ast;

public class Output(Source source, string outputFile) : Statement {
      
    public override void Visit<T>(INodeVisitor<T> visitor) {
        visitor.VisitOutput(this);
    }
      
    public Source Source => source;

    public string OutputFile => outputFile;
}
      