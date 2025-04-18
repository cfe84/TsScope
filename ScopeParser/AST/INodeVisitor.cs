// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

public interface INodeVisitor<T> {
    public T Visit(Node node);

    public T VisitAliasedField(AliasedField node);
    public T VisitAliasedSource(AliasedSource node);
    public T VisitAssignment(Assignment node);
    public T VisitBooleanLiteral(BooleanLiteral node);
    public T VisitInputField(InputField node);
    public T VisitExport(Export node);
    public T VisitFieldList(FieldList node);
    public T VisitFileSource(FileSource node);
    public T VisitIdentifier(Identifier node);
    public T VisitImport(Import node);
    public T VisitJoinQuery(JoinQuery node);
    public T VisitNumberLiteral(NumberLiteral node);
    public T VisitOutput(Output node);
    public T VisitParam(Param node);
    public T VisitSelectQuery(SelectQuery node);
    public T VisitScript(Script node);
    public T VisitStringLiteral(StringLiteral node);
    public T VisitStar(Star node);
    public T VisitTsExpression(TsExpression node);
    public T VisitTypedField(TypedField node);
    public T VisitVariableAssignment(VariableAssignment node);
    public T VisitVariableDefinition(VariableDefinition node);
    public T VisitVariableIdentifier(VariableIdentifier node);
    public T VisitWhereStatement(WhereStatement node);
}
