const fs = require('fs');
const path = require('path');

function field(f) {
  const [type, name] = f.split(' ');
  const baseType = getType(type);
  return {
    propertyType: "field", 
    type: baseType,
    name,
    isOptional: type.indexOf('?') >= 0,
    isArray: type.indexOf('[') >= 0,
    toString: () => f,
    toBNF: (addBefore = "", addAfter = "") => formatType(type, addBefore, addAfter),
  }
}

function keyword(s, modifier) {
  return {
    propertyType: "separator",
    separator: s,
    modifier,
    toString: () => "",
    toBNF: () => {
      return `"${s}"`;
    }
  }
}

function run() {
  const outputDirectory = getOutputDir();
  const terminalTypes = {
    "AliasedField": ["FieldValue field", keyword("AS"), "string alias"],
    "AliasedSource": ["Identifier alias", keyword("="), "Source source"], // TODO: Fix
    "Assignment":  ["Identifier variableName", keyword("="), "Source source"],
    "BooleanLiteral": ["bool value"],
    "InputField": ["string name", keyword("."), "string? ns"],
    "Export": [keyword("EXPORT"), "Source source", keyword("AS", "with next"), "string? exportName"],
    "FieldList": ["Field[] fields", keyword(",", "with previous")],
    "FileSource": [keyword("EXTRACT"), "FieldSpec fieldSpec", keyword("FROM"), "StringValue fileName"],
    "Identifier": ["string value"],
    "Import": [keyword("IMPORT"), "Identifier name", keyword("{"), "List<TypedField> fields", keyword(",", "with previous"), keyword("}")], // TODO: fix
    "JoinQuery": ["SelectSource left", "AliasableSource right", "JoinType joinType", keyword("ON"), "TsExpression condition"],
    "NumberLiteral": ["decimal value"], // since we output TS we only need number
    "Output": [keyword("OUTPUT"), "Source source", keyword("TO"), "StringValue outputFile"],
    "Param": [keyword("PARAM"), "string name", keyword(":", "with next"), "ParamDefaultValue? defaultValue"],
    "SelectQuery": [keyword("SELECT"), "FieldSpec fields", keyword("FROM"), "SelectSource source", "WhereStatement? where"],
    "Script": ["Statement[] statements", keyword(";", "with previous")],
    "StringLiteral": ["string value"],
    "Star": [keyword("*")],
    "TsExpression": [keyword("{{"), "string expression", keyword("}}")],
    "TypedField": ["string name", keyword(":"), "string type"],
    "VariableAssignment": [keyword("@"), "string variableName", keyword("="), "VariableValue value"],
    "VariableDefinition": [keyword("@"), "string variableName", keyword(":"), "string type", keyword("="), "VariableValue value"],
    "VariableIdentifier": [keyword("@"), "string variableName"],
    "WhereStatement": [keyword("WHERE"), "TsExpression condition"],
  };
  Object.keys(terminalTypes).forEach(key => {
    const components = terminalTypes[key];
    terminalTypes[key] = components.map(t => typeof t === "string" ? field(t) : t);
  });
  const compositeTypes = {
    "AliasableSource": ["Source", "AliasedSource"],
    "FieldValue": ["InputField", "TsExpression", "StringLiteral", "NumberLiteral", "BooleanLiteral"],
    "Field": ["AliasedField", "FieldValue"],
    "FieldSpec": ["FieldList", "Star"],
    "SelectSource": ["AliasableSource", "JoinQuery"],
    "Source": ["FileSource", "SelectQuery", "Identifier"],
    "Statement": ["Assignment", "VariableDefinition", "VariableAssignment", "Param", "Output", "Import", "Export"],
    "ParamDefaultValue": ["StringLiteral", "TsExpression"],
    "StringValue": ["StringLiteral", "VariableIdentifier", "TsExpression"],
    "VariableValue": ["TsExpression", "StringLiteral", "NumberLiteral", "BooleanLiteral", "VariableIdentifier"],
  };
  const enumTypes = {
    "JoinType": ["INNER", "LEFT OUTER", "RIGHT OUTER", "OUTER"],
  };
  const types = {};
  for (const [type, fields] of Object.entries(terminalTypes)) {
    const compositedIn = Object.keys(compositeTypes).filter(key => compositeTypes[key].indexOf(type) >= 0);
    const parentTypes = compositedIn.length > 0 ? compositedIn : ["Node"];
    types[type] = { parentTypes, fields: fields.filter(f => f.propertyType === "field").map(f => f.toString()), isComposite: false };
  };
  for (const type of Object.keys(compositeTypes)) {
    const compositedIn = Object.keys(compositeTypes).filter(key => compositeTypes[key].indexOf(type) >= 0);
    const parentTypes = compositedIn.length > 0 ? compositedIn : ["Node"];
    types[type] = { parentTypes, childrenTypes: compositeTypes[type], fields: [], isComposite: true };
  }
  createAst(outputDirectory, "Node", types, enumTypes);
  createBNF(outputDirectory, terminalTypes, compositeTypes, enumTypes);
}

function getOutputDir() {
  const args = process.argv.slice(2);
  if (args.length === 0) {
    throw new Error('No output directory provided.');
  }
  return args[0];
}

function createAst(outputDirectory, baseName, types, enumTypes) {
  createBaseNode(outputDirectory, baseName);

  for (const [name, config] of Object.entries(types)) {
    createType(outputDirectory, name, config, baseName);
  }

  for (const [name, options] of Object.entries(enumTypes)) {
    createEnum(outputDirectory, name, options);
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

function createEnum(outputDirectory, name, options) {
  const basePath = path.join(outputDirectory, `${name}.cs`);
  const enumOptions = options.map(
    option => `    ${option.split(" ").map(w => w[0] + w.substring(1).toLowerCase()).join("")},`)
    .join('\n');
  fs.writeFileSync(basePath, `${header}
public enum ${name} {
${enumOptions}
}
`);
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

function createBNF(outputDirectory, terminalTypes, compositeTypes, enumTypes) {
  const bnfPath = path.join(outputDirectory, "bnf.txt");
  const startWith = "Script";
  const bnf = generateBNF(terminalTypes, compositeTypes, enumTypes, startWith);
  fs.writeFileSync(bnfPath, bnf);
}

function generateBNF(terminalTypes, compositeTypes, enumTypes, startWith) {
  const visited = new Set();
  const visitNext = [startWith];
  let bnf = "";

  while (visitNext.length > 0) {
    const current = visitNext.pop();
    visited.add(current);

    if (terminalTypes[current]) {
      let components = terminalTypes[current];
      components.forEach(component => {
        if (component.propertyType === "field" && !visited.has(component.type) && !visitNext.includes(component.type)) {
          visitNext.push(component.type);
        }
      });

      let componentsStr = "";
      let addBefore = "";
      for (let i = 0; i < components.length; i++) {
        const c = components[i];
        if (componentsStr.length > 0) {
          componentsStr += " ";
        }

        if (c.propertyType === "separator") {
          if (c.modifier === "with next") {
            addBefore = c.toBNF();
            continue;
          } else if (c.modifier === "with previous") {
            continue;
          }
        }

        let addAfter = "";
        let nextComponent = i < components.length - 1 ? components[i + 1] : null;
        if (nextComponent && nextComponent.propertyType === "separator" && nextComponent.modifier === "with previous") {
          addAfter = nextComponent.toBNF();
        }

        componentsStr += c.toBNF(addBefore, addAfter);
        addBefore = "";
      }
      bnf += `${formatType(current)} ::= ${componentsStr}\n`;
    } else if (compositeTypes[current]) {
      let components = compositeTypes[current];
      components.forEach(component => {
        if (!visited.has(component) && !visitNext.includes(component)) {
          visitNext.push(component);
        }
      });
      bnf += `${formatType(current)} ::= \n      ${compositeTypes[current].map(c => formatType(c)).join('\n    | ')}\n`;
    } else if (enumTypes[current]) {
      bnf += `${formatType(current)} ::= ${enumTypes[current].map(opt => `"${opt}"`).join(' | ')}\n`;
    } else {
      if (current === "string" || current === "bool" || current === "decimal") {
        continue;
      }
      console.error(`Type not defined: ${current}`);
    }
    bnf += "\n";
  }

  return bnf;
}

function getType(componentName) {
  const match = componentName.match(/List<([^>]+)>/);
  if (match) {
    return match[1];
  }
  return componentName.replace(/([\[\]\?])/g, '');
}

function formatType(type, addBefore = "", addAfter = "") {
  const baseType = getType(type);
  var res = `<${baseType.replace(/(?!^)([A-Z])/g, '_$1').toUpperCase()}>`;

  if (addBefore) addBefore = `${addBefore} `;
  if (addAfter) addAfter = ` ${addAfter}`;
  const isMultiple = type.indexOf('[') >= 0 || type.startsWith("List");

  if (isMultiple) {
    if (addBefore || addAfter) {
      res = `(${addBefore}${res}${addAfter}) *`;
      addBefore = addAfter = "";
    } else {
      res = `${res}*`;
    }
  }
  
  res = `${addBefore}${res}${addAfter}`;
  if (type.indexOf('?') >= 0) {
    res = `[ ${res} ]`;
  }
  return res;
}

run();