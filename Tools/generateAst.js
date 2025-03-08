const fs = require('fs');
const path = require('path');


function run() {
  const outputDirectory = getOutputDir();
  const terminalTypes = {
    "Script": ["Statement[] statements"],
    "WhereStatement": ["string condition"],
    "Assignment":  ["Identifier variableName", "Source source"],
    "FileSource": ["FieldSpec fieldSpec", "string fileName"],
    "FieldList": ["Field[] fields"],
    "Star": [],
    "Field": ["string name", "string? ns"],
    "Identifier": ["string value"],
    "SelectQuery": ["FieldSpec fields", "SelectSource source", "WhereStatement? where"],
    "Output": ["Source source", "string outputFile"],
    "JoinQuery": ["SelectSource left", "Source right", "JoinType joinType", "string condition"],
  };
  const compositeTypes = {
    "Statement": ["Assignment", "Output"],
    "Source": ["FileSource", "SelectQuery", "Identifier"],
    "SelectSource": ["Source", "JoinQuery"],
    "FieldSpec": ["FieldList", "Star"],
  };
  const types = {};
  for (const [type, fields] of Object.entries(terminalTypes)) {
    const compositedIn = Object.keys(compositeTypes).find(key => compositeTypes[key].indexOf(type) >= 0);
    const parentType = compositedIn || "Node";
    types[type] = { parentType, fields, isComposite: false };
  };
  for (const type of Object.keys(compositeTypes)) {
    const compositedIn = Object.keys(compositeTypes).find(key => compositeTypes[key].indexOf(type) >= 0);
    const parentType = compositedIn || "Node";
    types[type] = { parentType, fields: [], isComposite: true };
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
public abstract class ${name} : ${config.parentType} {}
`);
  } else {
    fs.writeFileSync(basePath, `${header}
public class ${name}${parameters} : ${config.parentType} {
      
    public override T Visit<T>(I${baseName}Visitor<T> visitor)
    {
        return visitor.Visit${name}(this);
    }

    public override Token Token => token;
      
${config.fields.map(field => generateFieldGetter(field)).join('\n\n')}
}
      `);    
  }
}

function createBaseNode(outputDirectory, baseName) {
  const basePath = path.join(outputDirectory, `${baseName}.cs`);
  fs.writeFileSync(basePath, `${header}
public abstract class ${baseName} {
    public abstract T Visit<T>(I${baseName}Visitor<T> visitor);
    public abstract Token Token { get; }
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