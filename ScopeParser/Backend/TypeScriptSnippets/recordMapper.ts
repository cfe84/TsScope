class RecordMapper___id__ extends RecordMapper {
  map(record: SourceRecord): SourceRecord {
    Object.assign(globalThis, recordToObject(record));
    return [
      /*%mapRecord%*/
    ];
  }
}