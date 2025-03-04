const fs = require('fs');
const path = require('path');

function getOutputDir() {
  const args = process.argv.slice(2);
  if (args.length === 0) {
    throw new Error('No output directory provided.');
  }
  return args[0];
}

function createAst(outputDirectory, baseName, fields) {
  createBaseNode(outputDirectory, baseName);
  for (const [name, fieldList] of Object.entries(fields)) {
    createType(outputDirectory, name, baseName, fieldList);
  }
  createVisitor(outputDirectory, baseName, Object.entries(fields));
}

const header = `// This file is auto-generated. Do not modify it directly.
// Instead, use the generateAst.js script in Tools.
// Example usage: node Tools/generateAst.js ScopeParser/AST
using System;

namespace ScopeParser.Ast;
`;

function createType(outputDirectory, name, baseName, fields) {
  const basePath = path.join(outputDirectory, `${name}.cs`);

  function generateFieldGetter(field) {
    const [type, name] = field.split(' ');
    const capitalizedName = name.charAt(0).toUpperCase() + name.slice(1);
    return `public ${type} ${capitalizedName} => ${name};`;
  }

  fs.writeFileSync(basePath, `${header}
public class ${name}(${fields.join(', ')}) : ${baseName} {
    public override void Visit<T>(I${baseName}Visitor<T> visitor) {
        visitor.Visit${name}(this);
    }

    ${fields.map(field => generateFieldGetter(field)).join('\n\n')}
}
`);
}

function createBaseNode(outputDirectory, baseName) {
  const basePath = path.join(outputDirectory, `${baseName}.cs`);
  fs.writeFileSync(basePath, `${header}
public abstract class ${baseName} {
    public abstract void Visit<T>(I${baseName}Visitor<T> visitor);
}
`);
}

function createVisitor(outputDirectory, baseName, fields) {
  const basePath = path.join(outputDirectory, `I${baseName}Visitor.cs`);
  fs.writeFileSync(basePath, `${header}
public interface I${baseName}Visitor<T> {
    ${fields.map(([name]) => `T Visit${name}(${name} node);`).join('\n    ')}
}
`);
}

const outputDirectory = getOutputDir();
createAst(outputDirectory, "Node", {
  "FileSource": ["FieldList fields", "string fileName"],
  "Stream": ["FieldList fields", "string fileName"],
  "FieldList": ["Field[] fields"],
  "Field": ["string name"],
});