import * as fs from "fs";
import * as path from "path";

export interface IExportSink {
  addEventHandlers(
    recordHandler: (record: any) => void,
    doneHandler?: () => void
  ): void;

  getAsyncIterator(): AsyncIterator<any, any, any>;
}

export interface IImportSource<T> {
  send(record: T);
  close();
}

/*************
 * The following code is a boilerplate for the script.
 * "Custom" code is inserted lower down.
 */

function createStream() {
  ///////////////////////////////////////////////
  //                                           //
  //             Start boilerplate             //
  //                                           //
  ///////////////////////////////////////////////

  interface QualifiedName {
    name: string;
    namespace?: string;
  }

  interface Field {
    name: QualifiedName;
    value: any;
  }

  type SourceRecord = Field[];

  abstract class RecordMapper {
    abstract mapRecord(record: SourceRecord): SourceRecord;
    abstract mapHeaders(fields: QualifiedName[]): QualifiedName[];

    private fieldPositions: Record<string, number> = {};

    protected findField(
      input: SourceRecord,
      namespace: string | undefined,
      name: string
    ) {
      const dictionaryIndex = `${namespace}.${name}`;

      let index = this.fieldPositions[dictionaryIndex];

      if (index === undefined) {
        index = input.findIndex(
          (field) =>
            (!namespace || field.name.namespace === namespace) &&
            field.name.name === name
        );
        if (index === -1) {
          throw new Error(`Field ${dictionaryIndex} not found`);
        }
        this.fieldPositions[dictionaryIndex] = index;
      }

      return input[index].value;
    }
  }

  // TODO: Reduce the clutter for features that are not used.
  class StarRecordMapper extends RecordMapper {
    mapRecord = (record: SourceRecord) => record;
    mapHeaders = (fields: QualifiedName[]): QualifiedName[] =>
      fields.map((field) => ({
        name: field.name,
      }));
  }

  function recordToObject(
    record: SourceRecord,
    addNamespace = true
  ): { [key: string]: any } {
    const obj: { [key: string]: any } = {};
    if (addNamespace) {
      // We give precedence to namespaces. If a field is conflicting
      // with a namespace, it should be ignored.
      for (const field of record) {
        if (field.name.namespace && !(field.name.namespace in obj)) {
          obj[field.name.namespace] = {};
        }
      }
    }

    // Assign to root. We keep only the first value. If two fields are
    // conflicting, we should return a warning.
    for (const field of record) {
      if (!(field.name.name in obj)) {
        obj[field.name.name] = field.value;
      } else {
        // TODO: Handle conflicts better.
      }
    }
    if (addNamespace) {
      // Assign to namespace if typed.
      for (const field of record) {
        if (field.name.namespace) {
          obj[field.name.namespace][field.name.name] = field.value;
        }
      }
    }
    return obj;
  }

  interface IConsumer {
    receiveSchema(source: Source, schema: QualifiedName[]): void;
    receiveRecord(source: Source, record: SourceRecord): void;
    done(source: Source): void;
  }

  // Check if field should be included
  type FieldsFilter = (field: QualifiedName) => boolean;
  // Check if all the fields we're expecting are in the list.
  type FieldCheck = (field: QualifiedName[]) => string | true;
  type RecordFilter = (record: SourceRecord) => boolean;

  abstract class Source {
    private static sourceCount = 0;
    private consumers: IConsumer[] = [];

    public registerConsumer(consumer: IConsumer): void {
      this.consumers.push(consumer);
    }

    protected sendSchema(schema: QualifiedName[]): void {
      this.consumers.forEach((consumer) =>
        consumer.receiveSchema(this, schema)
      );
    }

    protected notifyConsumers(record: SourceRecord): void {
      this.consumers.forEach((consumer) =>
        consumer.receiveRecord(this, record)
      );
    }

    protected notifyConsumersDone(): void {
      this.consumers.forEach((consumer) => consumer.done(this));
    }

    public done(source: Source): void {
      this.notifyConsumersDone();
    }
  }

  interface IStartable {
    start(): void;
  }

  class FileSourceFactory {
    static create(filePath: string, recordMapper: RecordMapper): Source {
      const ext = path.extname(filePath);
      if (ext === ".csv") {
        return new CsvFileSource(filePath, recordMapper, ",");
      }
      if (ext === ".tsv") {
        return new CsvFileSource(filePath, recordMapper, "\t");
      }
      if (ext === ".json") {
        return new JsonFileSource(filePath, recordMapper);
      }
      throw new Error(`Unsupported file type: ${ext}`);
    }
  }

  class JsonFileSource extends Source implements IStartable {
    constructor(private filePath: string, private recordMapper: RecordMapper) {
      super();
      startable.push(this);
    }

    start(): void {
      const records = JSON.parse(fs.readFileSync(this.filePath, "utf-8"));
      this.sendHeaders(records);
      for (const record of records) {
        const recordFields = Object.entries(record).map(([key, value]) => ({
          name: { name: key },
          value,
        }));
        const mappedRecord = this.recordMapper.mapRecord(recordFields);
        this.notifyConsumers(mappedRecord);
      }
      this.notifyConsumersDone();
    }

    private sendHeaders(records: any[]): void {
      if (records.length === 0) {
        this.sendSchema(this.recordMapper.mapHeaders([]));
        return;
      }
      const fields = Object.keys(records[0]).map((key) => ({
        name: key,
        namespace: undefined,
      }));
      const headers = this.recordMapper.mapHeaders(fields);
      this.sendSchema(headers);
    }
  }

  class CsvFileSource extends Source implements IStartable {
    private fields: QualifiedName[] = [];

    constructor(
      filePath: string,
      private recordMapper: RecordMapper,
      private delimiter: string = ","
    ) {
      super();
      this.file = fs.createReadStream(filePath);
      startable.push(this);
    }

    private aggregate = "";

    start(): void {
      this.file.on("data", (data) => {
        data = data.toString();
        for (let i = 0; i < data.length; i++) {
          const char = data[i];
          if (char === "\n") {
            this.sendRecord();
            this.aggregate = "";
          } else {
            this.aggregate += char;
          }
        }
      });

      this.file.on("end", () => {
        this.sendRecord();
        this.notifyConsumersDone();
      });
    }

    private sendRecord(): void {
      if (this.aggregate === "") {
        return;
      }
      const valuesInLine = this.aggregate.split(this.delimiter);
      if (this.fields.length === 0) {
        // first line, extract fields
        this.fields = valuesInLine.map((field) => ({
          name: field,
          namespace: undefined,
        }));
        const headers = this.recordMapper.mapHeaders(this.fields);
        this.sendSchema(headers);
      } else {
        const thisRecord = valuesInLine.map((value, i) => ({
          name: this.fields[i],
          value,
        }));
        const record = this.recordMapper.mapRecord(thisRecord);
        this.notifyConsumers(record);
      }
    }

    private file: fs.ReadStream;
  }

  class SelectQuerySource extends Source implements IConsumer {
    constructor(
      private source: Source,
      private recordMapper: RecordMapper,
      private where?: RecordFilter
    ) {
      super();
      source.registerConsumer(this);
    }

    receiveSchema(source: Source, schema: QualifiedName[]): void {
      const headers = this.recordMapper.mapHeaders(schema);
      this.sendSchema(headers);
    }

    receiveRecord(_: Source, record: SourceRecord): void {
      if (this.where && !this.where(record)) {
        return;
      }

      const mappedRecord = this.recordMapper.mapRecord(record);

      this.notifyConsumers(mappedRecord);
    }
  }

  class NamedSource extends Source implements IConsumer {
    constructor(private source: Source, private name: string) {
      super();
      source.registerConsumer(this);
    }

    receiveSchema(source: Source, schema: QualifiedName[]): void {
      this.sendSchema(
        schema.map((field) => ({
          name: field.name,
          namespace: this.name,
        }))
      );
    }

    receiveRecord(_: Source, record: SourceRecord): void {
      const newRecord = record.map((field) => ({
        name: { ...field.name, namespace: this.name },
        value: field.value,
      }));
      this.notifyConsumers(newRecord);
    }
  }

  enum JoinType {
    Inner = "Inner",
    LeftOuter = "LeftOuter",
    RightOuter = "RightOuter",
    Outer = "Outer",
  }

  type JoinCondition = (record: SourceRecord) => boolean;

  // This can be heavily optimized. A proper join algorithm should be used.
  // This is a naive implementation that just iterates over the records and saves
  // them in memory.
  class JoinSource extends Source implements IConsumer {
    constructor(
      private left: Source,
      private right: Source,
      private condition: JoinCondition,
      private joinType: JoinType
    ) {
      super();
      left.registerConsumer(this);
      right.registerConsumer(this);
    }

    private leftRecords: SourceRecord[] = [];
    private rightRecords: SourceRecord[] = [];
    private leftSchema?: QualifiedName[];
    private rightSchema?: QualifiedName[];
    private leftDone: boolean = false;
    private rightDone: boolean = false;

    receiveRecord(source, record: SourceRecord): void {
      if (source === this.left) {
        this.leftRecords.push(record);
      } else if (source === this.right) {
        this.rightRecords.push(record);
      }
    }

    receiveSchema(source, schema: QualifiedName[]): void {
      if (source === this.left) {
        this.leftSchema = schema;
      }
      if (source === this.right) {
        this.rightSchema = schema;
      }
      if (this.leftSchema && this.rightSchema) {
        const fullSchema = [...this.leftSchema, ...this.rightSchema];
        this.sendSchema(fullSchema);
      }
    }

    override done(source: Source): void {
      if (source === this.left) {
        this.leftDone = true;
      } else if (source === this.right) {
        this.rightDone = true;
      }
      // Todo: There's probably a smarter way to do that.
      // We only need one of the joins to be done before we
      // can start matching the rest of the incomings.
      if (this.leftDone && this.rightDone) {
        this.start();
      }
    }

    private start(): void {
      if (this.joinType === JoinType.Inner) {
        this.innerJoin();
      } else if (this.joinType === JoinType.LeftOuter) {
        this.directionalOuterJoin(this.leftRecords, this.rightRecords);
      } else if (this.joinType === JoinType.RightOuter) {
        this.directionalOuterJoin(this.rightRecords, this.leftRecords);
      } else {
        throw Error(`Join type not implemented: ${this.joinType}`);
      }
      // Other types not implemented yet.
      this.notifyConsumersDone();
    }

    private innerJoin(): void {
      for (const leftRecord of this.leftRecords) {
        for (const rightRecord of this.rightRecords) {
          const fullRecord = [...leftRecord, ...rightRecord];
          if (this.condition(fullRecord)) {
            this.notifyConsumers(fullRecord);
          }
        }
      }
    }

    private directionalOuterJoin(
      keepAll: SourceRecord[],
      onlyMatching: SourceRecord[]
    ): void {
      for (const leftRecord of keepAll) {
        let match = false;
        for (const rightRecord of onlyMatching) {
          const fullRecord = [...leftRecord, ...rightRecord];
          if (this.condition(fullRecord)) {
            match = true;
            this.notifyConsumers(fullRecord);
          }
        }
        if (!match) {
          const emptyRecord = this.generateEmptyRecord(leftRecord);
          const fullRecord = [...leftRecord, ...emptyRecord];
          this.notifyConsumers(fullRecord);
        }
      }
    }

    private generateEmptyRecord(exampleSource: SourceRecord): SourceRecord {
      const emptyRecord: SourceRecord = [];
      for (const field of exampleSource) {
        emptyRecord.push({ name: field.name, value: null });
      }
      return emptyRecord;
    }
  }

  interface IClosableOutput {
    close(): void;
  }

  class FileOutputFactory {
    static create(filePath: string): IConsumer {
      const ext = path.extname(filePath);
      if (ext === ".csv") {
        return new SeparatorFileOutput(filePath, ",");
      }
      if (ext === ".tsv") {
        return new SeparatorFileOutput(filePath, "\t");
      }
      if (ext === ".json") {
        return new JsonFileOutput(filePath);
      }
      throw new Error(`Unsupported file type: ${ext}`);
    }
  }

  class JsonFileOutput implements IConsumer {
    private firstRecord = true;
    constructor(private filePath: string) {}
    receiveSchema(_: Source, schema: QualifiedName[]): void {
      fs.writeFileSync(this.filePath, "[\n");
    }
    done(source: Source): void {
      this.close();
    }
    receiveRecord(_: Source, record: SourceRecord): void {
      let json = "  " + JSON.stringify(recordToObject(record, false));
      if (this.firstRecord) {
        this.firstRecord = false;
      } else {
        json = ",\n" + json;
      }
      fs.appendFileSync(this.filePath, json);
    }
    close(): void {
      fs.appendFileSync(this.filePath, "\n]\n");
    }
  }

  class SeparatorFileOutput implements IConsumer, IClosableOutput {
    constructor(private filePath: string, private separator: string) {}

    receiveSchema(_: Source, schema: QualifiedName[]): void {
      this.writeHeader(schema);
    }

    done(source: Source): void {
      this.close();
    }

    private fieldToString(value: any): string {
      if (typeof value === "string") {
        return `"${value.replace(/"/g, '\\"')}"`;
      }
      if (value === null || value === undefined) {
        return "";
      }
      return value.toString();
    }

    receiveRecord(_: Source, record: SourceRecord): void {
      fs.appendFileSync(
        this.filePath,
        record
          .map((field) => this.fieldToString(field.value))
          .join(this.separator) + "\n"
      );
    }

    private writeHeader(schema: QualifiedName[]): void {
      fs.writeFileSync(
        this.filePath,
        schema
          .map((field) => `"${field.name.replace(/"/g, '\\"')}"`)
          .join(",") + "\n"
      );
    }

    close(): void {}
  }

  class ImportSource<T> extends Source implements IImportSource<T> {
    constructor() {
      super();
      this.send = this.send.bind(this);
      this.close = this.close.bind(this);
    }

    send(record: T) {
      const sourceRecord = Object.entries(record as any).map(
        ([key, value]) => ({
          name: { name: key },
          value,
        })
      );
      this.notifyConsumers(sourceRecord);
    }

    close() {
      this.notifyConsumersDone();
    }
  }

  class ExportSink implements IConsumer, IClosableOutput, IExportSink {
    private isDone = false;
    constructor(private source: Source) {
      this.addEventHandlers = this.addEventHandlers.bind(this);
      this.getAsyncIterator = this.getAsyncIterator.bind(this);
      this.source.registerConsumer(this);
      closableOutputs.push(this);
    }

    receiveSchema(source: Source, schema: QualifiedName[]): void {}

    receiveRecord(source: Source, record: SourceRecord): void {
      this.onRecord.forEach((callback) =>
        callback(recordToObject(record, false))
      );
    }

    done(source: Source): void {
      this.onDone.forEach((callback) => callback());
      this.isDone = true;
    }

    close(): void {}

    addEventHandlers(
      // TODO: handle typing
      recordHandler: (record: any) => void,
      doneHandler?: () => void
    ) {
      this.onRecord.push(recordHandler);
      if (doneHandler) {
        this.onDone.push(doneHandler);
      }
    }

    getAsyncIterator(): AsyncIterator<any, any, any> {
      console.log("getAsyncIterator called");

      const asyncIterator = {
        next: (): Promise<IteratorResult<any>> => {
          console.log("Next called");
          return new Promise((resolve) => {
            console.log("Starting the promise");
            if (this.isDone) {
              console.warn(
                `Warning: ExportSink is already done. Returning empty iterator.`
              );
              resolve({ done: true, value: null });
              return;
            }
            const recordResolver = (record: any) => {
              console.log("Record resolved");
              resolve({ done: false, value: record });
              removeResolvers();
            };
            const doneResolver = () => {
              console.log("Done resolved");
              resolve({ done: true, value: null });
              removeResolvers();
            };
            const removeResolvers = () => {
              console.log("Removing resolvers");
              this.onRecord = this.onRecord.filter(
                (cb) => cb !== recordResolver
              );
              this.onDone = this.onDone.filter((cb) => cb !== doneResolver);
            };
            this.onRecord.push(recordResolver);
            this.onDone.push(doneResolver);
            console.log("Resolvers added");
          });
        },
      };
      return asyncIterator;
    }

    onRecord: ((record: any) => void)[] = [];
    onDone: (() => void)[] = [];
  }

  const startable: IStartable[] = [];
  const closableOutputs: IClosableOutput[] = [];

  ///////////////////////////////////////////////
  //                                           //
  //              End boilerplate              //
  //                                           //
  ///////////////////////////////////////////////

  // This is where your script code starts.

  class RecordMapper_0 extends RecordMapper {
  mapRecord(record: SourceRecord): SourceRecord {
    Object.assign(globalThis, recordToObject(record));
    return [
      {
        name: {
          namespace: undefined,
          name: "task",
        },
        value: this.findField(record, undefined, "name"),
      },
      {
        name: {
          namespace: undefined,
          name: "taskId",
        },
        value: this.findField(record, undefined, "id"),
      },
      {
        name: {
          namespace: undefined,
          name: "assignee",
        },
        value: this.findField(record, undefined, "assignee"),
      }
    ];
  }

  mapHeaders(headers: QualifiedName[]): QualifiedName[] {
    const res: QualifiedName[] = [];
  
    // Alias field: task
    res.push({ name: "task" });
    // Alias field: taskId
    res.push({ name: "taskId" });
    // assignee
    // Find a header with only the name
    let header_2_n = headers.find(header => header.name === "assignee");
    if (header_2_n) {
      res.push(header_2_n);
    } else {
      throw new Error(`Header not found: assignee. Available headers were: ${headers.map(header => header.name).join(", ")}`);
    }

    return res;
  }
}

  class RecordMapper_1 extends RecordMapper {
  mapRecord(record: SourceRecord): SourceRecord {
    Object.assign(globalThis, recordToObject(record));
    return [
      {
        name: {
          namespace: undefined,
          name: "firstName",
        },
        value: this.findField(record, undefined, "firstName"),
      },
      {
        name: {
          namespace: undefined,
          name: "task",
        },
        value: this.findField(record, undefined, "task"),
      }
    ];
  }

  mapHeaders(headers: QualifiedName[]): QualifiedName[] {
    const res: QualifiedName[] = [];
  
    // firstName
    // Find a header with only the name
    let header_0_n = headers.find(header => header.name === "firstName");
    if (header_0_n) {
      res.push(header_0_n);
    } else {
      throw new Error(`Header not found: firstName. Available headers were: ${headers.map(header => header.name).join(", ")}`);
    }
    // task
    // Find a header with only the name
    let header_1_n = headers.find(header => header.name === "task");
    if (header_1_n) {
      res.push(header_1_n);
    } else {
      throw new Error(`Header not found: task. Available headers were: ${headers.map(header => header.name).join(", ")}`);
    }

    return res;
  }
}

  function condition_0(record) {
  // Inner  (line 4, column 51)
  record = recordToObject(record);
  Object.assign(globalThis, record);
  const res = // Condition must be on new line to accomodate for the tsIgnore flag
    users.id === `${assignee}`;
  return res;
}

  const SOURCE__users_0 = new NamedSource(FileSourceFactory.create("inputs/users.csv", new StarRecordMapper()), "users");
  const SOURCE__tasks_0 = new NamedSource(FileSourceFactory.create("inputs/tasks.json", new RecordMapper_0()), "tasks");
  const SOURCE__assignedTasks_0 = new NamedSource(new SelectQuerySource(new JoinSource(SOURCE__users_0, SOURCE__tasks_0, condition_0, JoinType.Inner), new RecordMapper_1(), undefined), "assignedTasks");
  const OUTPUT_FILE__0 = FileOutputFactory.create("outputs/json_file_source.json");
  SOURCE__assignedTasks_0.registerConsumer(OUTPUT_FILE__0);
  

  ///////////////////////////////////////////////
  //                                           //
  //             Resume boilerplate            //
  //                                           //
  ///////////////////////////////////////////////

  function start() {
    startable.forEach((source) => source.start());
    closableOutputs.forEach((output) => output.close());
  }

  return {
    start,
    
  };
}


const isUsedAsExecutable = process.argv[1] === __filename;

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

  

  const obj = createStream();
  obj.start();
}

function createAsyncProcessor() {
  return function () {
    return new Promise((resolve) => {
      const stream = createStream();
      let done = 0;
      function returnIfDone() {
        if (++done === 0) {
          resolve({
            
          });
        }
      }
      
      
    });
  };
}

export { createStream, createAsyncProcessor };
