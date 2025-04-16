if (isUsedAsExecutable) {
  function loadParameter(paramName: string, defaultValue?: string): string {
    const value = process.env[paramName];
    if (value === undefined) {
      if (defaultValue === undefined) {
        throw new Error(`Missing parameter '${paramName}'`);
      }
      return defaultValue;
    }
    return value;
  }

  /*%loadParameters%*/

  const obj = createStream(/*%paramInvokes%*/);
  obj.start();
}