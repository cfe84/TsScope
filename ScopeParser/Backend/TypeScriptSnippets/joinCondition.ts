function __name__(record) {
  // __token__
  record = recordToObject(record);
  Object.assign(globalThis, record);
  const res = // Condition must be on new line to accomodate for the tsIgnore flag
    __condition__;
  return res;
}
