class RecordMapper___id__ extends RecordMapper {
  mapRecord(record: SourceRecord): SourceRecord {
    Object.assign(globalThis, recordToObject(record));
    return [
      /*%mapRecord%*/
    ];
  }

  mapHeaders(headers: QualifiedName[]): QualifiedName[] {
    const res: QualifiedName[] = [];
  
    /*%mapHeaders%*/

    return res;
  }
}
