function __name__(__left___in, __right___in, record) {
  // TODO: Should use namespace instead of the left/right names.
  record = recordToObject(record);
  const __left__: any = {};
  const __right__: any = {};
  for (const field of __left___in) {
    __left__[field.name.name] = field.value;
  }
  for (const field of __right___in) {
    __right__[field.name.name] = field.value;
  }
  const res = // Condition must be on new line to accomodate for the tsIgnore flag
    __condition__;
  return res;
}
