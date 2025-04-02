// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;

/// <summary>
/// Can be one of:
/// - Source
/// - AliasedSource
/// </summary>
public interface AliasableSource : SelectSource {}
