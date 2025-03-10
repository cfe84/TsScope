function __name__(__left___in, __right___in, record) {
  // TODO: Should use namespace instead of the left/right names.
  const __left__: any = {};
  const __right__: any = {};
  for (const field of __left___in) {
    __left__[field.name.name] = field.value;
  }
  for (const field of __right___in) {
    __right__[field.name.name] = field.value;
  }
  return __condition__;
}
