// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public interface INodeVisitor<T> {
    public T Visit(Node node);

    public T VisitScript(Script node);
    public T VisitWhereStatement(WhereStatement node);
    public T VisitAssignment(Assignment node);
    public T VisitFileSource(FileSource node);
    public T VisitFieldList(FieldList node);
    public T VisitStar(Star node);
    public T VisitField(Field node);
    public T VisitIdentifier(Identifier node);
    public T VisitSelectQuery(SelectQuery node);
    public T VisitOutput(Output node);
    public T VisitJoinQuery(JoinQuery node);
}
