if (isUsedAsExecutable) {
  console.error(`Error: this script contains an IMPORT statement, and as such can only be used in library mode.
To use this script as an executable, please remove the IMPORT statement.`);
  process.exit(1);
}