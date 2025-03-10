(record: any) => {
    record = recordToObject(record);
    Object.assign(globalThis, record);
    const res = // Condition must be on new line to accomodate for the tsIgnore flag
        __condition__
    return res;
}