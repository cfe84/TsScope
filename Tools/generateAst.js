const fs = require('fs');
const path = require('path');


function run() {
  const outputDirectory = getOutputDir();
  const terminalTypes = {
    "AliasedField": ["Field field", "string alias"],
    "AliasedSource": ["Source source", "Identifier alias"],
    "Assignment":  ["Identifier variableName", "Source source"],
    "InputField": ["string name", "string? ns"],
    "FieldList": ["Field[] fields"],
    "FileSource": ["FieldSpec fieldSpec", "string fileName"],
    "Identifier": ["string value"],
    "JoinQuery": ["SelectSource left", "AliasableSource right", "JoinType joinType", "TsExpression condition"],
    "Output": ["Source source", "string outputFile"],
    "SelectQuery": ["FieldSpec fields", "SelectSource source", "WhereStatement? where"],
    "Script": ["Statement[] statements"],
    "Star": [],
    "TsExpression": ["string expression"],
    "WhereStatement": ["TsExpression condition"],
  };
  const compositeTypes = {
    "AliasableSource": ["Source", "AliasedSource"],
    "Field": ["InputField", "AliasedField"], // TODO: add a ts expression
    "FieldSpec": ["FieldList", "Star"],
    "SelectSource": ["AliasableSource", "JoinQuery"],
    "Source": ["FileSource", "SelectQuery", "Identifier"],
    "Statement": ["Assignment", "Output"],
  };
  const types = {};
  for (const [type, fields] of Object.entries(terminalTypes)) {
    const compositedIn = Object.keys(compositeTypes).filter(key => compositeTypes[key].indexOf(type) >= 0);
    const parentTypes = compositedIn.length > 0 ? compositedIn : ["Node"];
    types[type] = { parentTypes, fields, isComposite: false };
  };
  for (const type of Object.keys(compositeTypes)) {
    const compositedIn = Object.keys(compositeTypes).filter(key => compositeTypes[key].indexOf(type) >= 0);
    const parentTypes = compositedIn.length > 0 ? compositedIn : ["Node"];
    types[type] = { parentTypes, childrenTypes: compositeTypes[type], fields: [], isComposite: true };
  }
  createAst(outputDirectory, "Node", types);
}


function getOutputDir() {
  const args = process.argv.slice(2);
  if (args.length === 0) {
    throw new Error('No output directory provided.');
  }
  return args[0];
}

function createAst(outputDirectory, baseName, types) {
  createBaseNode(outputDirectory, baseName);
  for (const [name, config] of Object.entries(types)) {
    createType(outputDirectory, name, config, baseName);
  }
  createVisitor(outputDirectory, baseName, Object.entries(types));
}

const header = `// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;
using ScopeParser.Lexing;

namespace ScopeParser.Ast;
`;

function createType(outputDirectory, name, config, baseName) {
  const basePath = path.join(outputDirectory, `${name}.cs`);

  function generateFieldGetter(field) {
    const [type, name] = field.split(' ');
    const capitalizedName = name.charAt(0).toUpperCase() + name.slice(1);
    return `    public ${type} ${capitalizedName} => ${name};`;
  }

  const parameters = `(${["Token token"].concat(config.fields).join(', ')})`;
  if (config.isComposite) {
    fs.writeFileSync(basePath, `${header}
/// <summary>
/// Can be one of:
${config.childrenTypes.map(type => `/// - ${type}`).join('\n')}
/// </summary>
public interface ${name} : ${config.parentTypes.join(", ")} {}
`);
  } else {
    fs.writeFileSync(basePath, `${header}
public class ${name}${parameters} : ${config.parentTypes.join(", ")} {
      
    public T Visit<T>(I${baseName}Visitor<T> visitor)
    {
        return visitor.Visit${name}(this);
    }

    public Token Token => token;
      
${config.fields.map(field => generateFieldGetter(field)).join('\n\n')}
}
      `);    
  }
}

function createBaseNode(outputDirectory, baseName) {
  const basePath = path.join(outputDirectory, `${baseName}.cs`);
  fs.writeFileSync(basePath, `${header}
public interface ${baseName} {
    public T Visit<T>(I${baseName}Visitor<T> visitor);
    public Token Token { get; }
}
`);
}

function createVisitor(outputDirectory, baseName, fields) {
  const basePath = path.join(outputDirectory, `I${baseName}Visitor.cs`);
  fs.writeFileSync(basePath, `${header}
public interface I${baseName}Visitor<T> {
    public T Visit(${baseName} node);

    ${fields
      .filter(([_, config]) => !config.isComposite)
      .map(([name]) => `public T Visit${name}(${name} node);`)
      .join('\n    ')}
}
`);
}

run();