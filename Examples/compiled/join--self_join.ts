import * as fs from "fs";
import * as path from "path";

/*************
 * The following code is a boilerplate for the script.
 * "Custom" code is inserted lower down.
 */

function run() {
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

  interface FieldsSpec {
    fieldFilter: (field: QualifiedName) => boolean;
    missingFields: (field: QualifiedName[]) => {
      result: string[];
      position: string;
    };
  }

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

  class StarRecordMapper extends RecordMapper {
    mapRecord = (record: SourceRecord) => record;
    mapHeaders = (fields: QualifiedName[]): QualifiedName[] =>
      fields.map((field) => ({
        name: field.name,
      }));
  }

  type SourceRecord = Field[];

  function recordToObject(record: SourceRecord): { [key: string]: any } {
    const obj: { [key: string]: any } = {};
    // We give precedence to namespaces. If a field is conflicting
    // with a namespace, it should be ignored.
    for (const field of record) {
      if (field.name.namespace && !(field.name.namespace in obj)) {
        obj[field.name.namespace] = {};
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
    // Assign to namespace if typed.
    for (const field of record) {
      if (field.name.namespace) {
        obj[field.name.namespace][field.name.name] = field.value;
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

    private _id: string = (Source.sourceCount++).toString();
    public get id(): string {
      return this._id;
    }

    registerConsumer(consumer: IConsumer): void {
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

  class FileSource extends Source implements IStartable {
    private fields: QualifiedName[] = [];

    constructor(filePath: string, private recordMapper: RecordMapper) {
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
      const valuesInLine = this.aggregate.split(",");
      if (this.fields.length === 0) {
        // first line, extract fields
        this.fields = valuesInLine.map((field) => ({
          name: field,
          namespace: undefined,
        }));
        this.sendSchema(this.fields);
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
    private leftSchema: QualifiedName[] = [];
    private rightSchema: QualifiedName[] = [];
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
      const fullSchema = [...this.leftSchema, ...this.rightSchema];
      this.sendSchema(fullSchema);
    }

    override done(source: Source): void {
      if (source === this.left) {
        this.leftDone = true;
      } else if (source === this.right) {
        this.rightDone = true;
      }
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

  class FileOutput implements IConsumer, IClosableOutput {
    constructor(private filePath: string) {}

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
        record.map((field) => this.fieldToString(field.value)).join(",") + "\n"
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

  const startable: IStartable[] = [];
  const closableOutputs: IClosableOutput[] = [];

  ///////////////////////////////////////////////
  //                                           //
  //              End boilerplate              //
  //                                           //
  ///////////////////////////////////////////////

  // This is where your script code starts.

  /*%params%*/
  class RecordMapper_0 extends RecordMapper {
  mapRecord(record: SourceRecord): SourceRecord {
    Object.assign(globalThis, recordToObject(record));
    return [
      {
    name: {
        namespace: "users",
        name: "id",
    },
    value: this.findField(record, "users", "id"),
},
{
    name: {
        namespace: "users",
        name: "firstName",
    },
    value: this.findField(record, "users", "firstName"),
},
{
    name: {
        namespace: "users",
        name: "adminId",
    },
    value: this.findField(record, "users", "adminId"),
},
{
    name: {
        namespace: "admins",
        name: "id",
    },
    value: this.findField(record, "admins", "id"),
},
{
    name: {
        namespace: "admins",
        name: "firstName",
    },
    value: this.findField(record, "admins", "firstName"),
}
    ];
  }

  mapHeaders(headers: QualifiedName[]): QualifiedName[] {
    const res: QualifiedName[] = [];
  
    // users.id
// Find a header with both namespace and name
let header_0_ns_and_n = headers.filter(header => header.namespace).find(header => header.namespace === "users" && header.name === "id");
if (header_0_ns_and_n) {
    res.push(header_0_ns_and_n);
} else {
    // Find a header with only the name
    let header_0_n = headers.find(header => header.name === "id");
    if (header_0_n) {
        res.push(header_0_n);
    }
}
// users.firstName
// Find a header with both namespace and name
let header_1_ns_and_n = headers.filter(header => header.namespace).find(header => header.namespace === "users" && header.name === "firstName");
if (header_1_ns_and_n) {
    res.push(header_1_ns_and_n);
} else {
    // Find a header with only the name
    let header_1_n = headers.find(header => header.name === "firstName");
    if (header_1_n) {
        res.push(header_1_n);
    }
}
// users.adminId
// Find a header with both namespace and name
let header_2_ns_and_n = headers.filter(header => header.namespace).find(header => header.namespace === "users" && header.name === "adminId");
if (header_2_ns_and_n) {
    res.push(header_2_ns_and_n);
} else {
    // Find a header with only the name
    let header_2_n = headers.find(header => header.name === "adminId");
    if (header_2_n) {
        res.push(header_2_n);
    }
}
// admins.id
// Find a header with both namespace and name
let header_3_ns_and_n = headers.filter(header => header.namespace).find(header => header.namespace === "admins" && header.name === "id");
if (header_3_ns_and_n) {
    res.push(header_3_ns_and_n);
} else {
    // Find a header with only the name
    let header_3_n = headers.find(header => header.name === "id");
    if (header_3_n) {
        res.push(header_3_n);
    }
}
// admins.firstName
// Find a header with both namespace and name
let header_4_ns_and_n = headers.filter(header => header.namespace).find(header => header.namespace === "admins" && header.name === "firstName");
if (header_4_ns_and_n) {
    res.push(header_4_ns_and_n);
} else {
    // Find a header with only the name
    let header_4_n = headers.find(header => header.name === "firstName");
    if (header_4_n) {
        res.push(header_4_n);
    }
}

    return res;
  }
}

  function condition_0(record) {
  record = recordToObject(record);
  Object.assign(globalThis, record);
  const res = // Condition must be on new line to accomodate for the tsIgnore flag
    users.adminId === admins.id;
  return res;
}

  const SOURCE__users_0 = new NamedSource(new FileSource("inputs/users.csv", new StarRecordMapper()), "users");
const SOURCE__admins_0 = new NamedSource(new SelectQuerySource(new JoinSource(SOURCE__users_0, new NamedSource(SOURCE__users_0, "admins"), condition_0, JoinType.Inner), new RecordMapper_0(), undefined), "admins");
const OUTPUT_FILE__0 = new FileOutput("outputs/join--self_join.csv");
SOURCE__admins_0.registerConsumer(OUTPUT_FILE__0);


  ///////////////////////////////////////////////
  //                                           //
  //             Resume boilerplate            //
  //                                           //
  ///////////////////////////////////////////////

  startable.forEach((source) => source.start());
  closableOutputs.forEach((output) => output.close());

  /*%exports%*/
}

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



run();
