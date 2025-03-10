import * as fs from "fs";
import * as path from "path";

interface QualifiedName {
  name: string;
  namespace?: string;
}

interface Field {
  name: QualifiedName;
  value: string;
}

interface FieldsSpec {
  fieldFilter: (field: QualifiedName) => boolean;
  missingFields: (field: QualifiedName[]) => {
    result: string[];
    position: string;
  };
}

const star: FieldsSpec = {
  fieldFilter: (_: QualifiedName) => true,
  missingFields: (_: QualifiedName[]) => ({ result: [], position: "" }),
};

type Record = Field[];

function recordToObject(record: Record): { [key: string]: any } {
  const obj: { [key: string]: any } = {};
  // Assign to root.
  for (const field of record) {
    obj[field.name.name] = field.value;
  }
  // Assign to namespace if typed.
  for (const field of record) {
    if (field.name.namespace && !(field.name.namespace in obj)) {
      obj[field.name.namespace] = {};
    }
    if (field.name.namespace) {
      obj[field.name.namespace][field.name.name] = field.value;
    }
  }
  return obj;
}

interface IConsumer {
  receiveRecord(source: Source, record: Record): void;
  receiveSchema(source: Source, schema: QualifiedName[]): void;
  done(source: Source): void;
}

// Check if field should be included
type FieldsFilter = (field: QualifiedName) => boolean;
// Check if all the fields we're expecting are in the list.
type FieldCheck = (field: QualifiedName[]) => string | true;
type RecordFilter = (record: Record) => boolean;

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

  protected notifyConsumers(record: Record): void {
    this.consumers.forEach((consumer) => consumer.receiveRecord(this, record));
  }

  protected notifyConsumersSchema(schema: QualifiedName[]): void {
    this.consumers.forEach((consumer) => consumer.receiveSchema(this, schema));
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

  constructor(filePath: string, private fieldsSpec: FieldsSpec) {
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
      const missingFields = this.fieldsSpec.missingFields(this.fields);
      if (missingFields.result.length > 0) {
        throw new Error(
          `Missing field(s) in ${
            missingFields.position
          }: ${missingFields.result.join(", ")}`
        );
      }
      this.notifyConsumersSchema(
        this.fields.filter(this.fieldsSpec.fieldFilter)
      );
    } else {
      const record = this.fields
        .map((field, index) => {
          if (!this.fieldsSpec.fieldFilter(field)) {
            return;
          }
          const value = valuesInLine[index];
          return {
            name: field,
            value: value,
          };
        })
        .filter((field) => field !== undefined);
      this.notifyConsumers(record);
    }
  }

  private file: fs.ReadStream;
}

class SelectQuerySource extends Source implements IConsumer {
  constructor(
    private source: Source,
    private fieldsSpec: FieldsSpec,
    private where?: RecordFilter
  ) {
    super();
    source.registerConsumer(this);
  }

  receiveSchema(_: Source, schema: QualifiedName[]): void {
    const missingFields = this.fieldsSpec.missingFields(schema);
    if (missingFields.result.length > 0) {
      throw new Error(
        `Missing field(s) in ${
          missingFields.position
        }: ${missingFields.result.join(", ")}`
      );
    }

    this.notifyConsumersSchema(
      schema.filter((field) => this.fieldsSpec.fieldFilter(field))
    );
  }

  receiveRecord(_: Source, record: Record): void {
    if (this.where && !this.where(record)) {
      return;
    }
    const result = record.filter((field) =>
      this.fieldsSpec.fieldFilter(field.name)
    );

    this.notifyConsumers(result);
  }
}

class NamedSource extends Source implements IConsumer {
  constructor(private source: Source, private name: string) {
    super();
    source.registerConsumer(this);
  }

  receiveRecord(_: Source, record: Record): void {
    const newRecord = record.map((field) => ({
      name: { ...field.name, namespace: this.name },
      value: field.value,
    }));
    this.notifyConsumers(newRecord);
  }

  receiveSchema(_: Source, schema: QualifiedName[]): void {
    const newSchema = schema.map((field) => ({
      ...field,
      namespace: this.name,
    }));
    this.notifyConsumersSchema(newSchema);
  }
}

enum JoinType {
  Inner = "Inner",
  Left = "Left",
  Right = "Right",
}

type JoinCondition = (left: Record, right: Record, record: Record) => boolean;

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

  private leftRecords: Record[] = [];
  private rightRecords: Record[] = [];
  private leftSchema: QualifiedName[] = [];
  private rightSchema: QualifiedName[] = [];
  private leftDone: boolean = false;
  private rightDone: boolean = false;

  receiveRecord(source, record: Record): void {
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
    this.notifyConsumersSchema(this.leftSchema.concat(this.rightSchema));
    if (this.joinType === JoinType.Inner) {
      this.innerJoin();
    }
    // Other types not implemented yet.
    this.notifyConsumersDone();
  }

  private innerJoin(): void {
    for (const leftRecord of this.leftRecords) {
      for (const rightRecord of this.rightRecords) {
        const fullRecord = [...leftRecord, ...rightRecord];
        if (this.condition(leftRecord, rightRecord, fullRecord)) {
          this.notifyConsumers(fullRecord);
        }
      }
    }
  }
}

interface IClosableOutput {
  close(): void;
}

class FileOutput implements IConsumer, IClosableOutput {
  constructor(private filePath: string) {}

  done(source: Source): void {
    this.close();
  }

  receiveRecord(_: Source, record: Record): void {
    fs.appendFileSync(
      this.filePath,
      record.map((field) => field.value).join(",") + "\n"
    );
  }

  receiveSchema(_: Source, schema: QualifiedName[]): void {
    fs.writeFileSync(
      this.filePath,
      schema.map((field) => field.name).join(",") + "\n"
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


const input_0 = new NamedSource(new FileSource("input.csv", {
    fieldFilter: (field: QualifiedName) => ["id", "firstName", "age"]
        .includes(field.name) || ["id", "firstName", "age"].includes(`${field.namespace}.${field.name}`),
    missingFields: (fields: QualifiedName[]) => {
        const fieldNames = fields.map((field) => field.name);
        const qualifiedNames = fields.map((field) => field.namespace + "." + field.name);
        const result = ["id", "firstName", "age"].filter((field) => !(qualifiedNames.includes(field) || fieldNames.includes(field)));
        return { result, position: "Identifier \"id\" (line 1, column 17)" };
    }
}), "input");
const fields_0 = new NamedSource(new SelectQuerySource(input_0, {
    fieldFilter: (field: QualifiedName) => ["id", "firstName", "age"]
        .includes(field.name) || ["id", "firstName", "age"].includes(`${field.namespace}.${field.name}`),
    missingFields: (fields: QualifiedName[]) => {
        const fieldNames = fields.map((field) => field.name);
        const qualifiedNames = fields.map((field) => field.namespace + "." + field.name);
        const result = ["id", "firstName", "age"].filter((field) => !(qualifiedNames.includes(field) || fieldNames.includes(field)));
        return { result, position: "Identifier \"id\" (line 2, column 17)" };
    }
}, undefined), "fields");
const filtered_0 = new NamedSource(new SelectQuerySource(fields_0, {
    fieldFilter: (field: QualifiedName) => ["firstName", "age"]
        .includes(field.name) || ["firstName", "age"].includes(`${field.namespace}.${field.name}`),
    missingFields: (fields: QualifiedName[]) => {
        const fieldNames = fields.map((field) => field.name);
        const qualifiedNames = fields.map((field) => field.namespace + "." + field.name);
        const result = ["firstName", "age"].filter((field) => !(qualifiedNames.includes(field) || fieldNames.includes(field)));
        return { result, position: "Identifier \"firstName\" (line 3, column 19)" };
    }
}, (record: any) => {
    record = recordToObject(record);
    Object.assign(globalThis, record);
    const res = // Condition must be on new line to accomodate for the tsIgnore flag
        age > 30
    return res;
}), "filtered");
const output_0 = new FileOutput("select_fields.csv");
fields_0.registerConsumer(output_0);
const output_1 = new FileOutput("select_input_copy.csv");
input_0.registerConsumer(output_1);
const output_2 = new FileOutput("select_filtered.csv");
filtered_0.registerConsumer(output_2);

///////////////////////////////////////////////
//                                           //
//             Resume boilerplate            //
//                                           //
///////////////////////////////////////////////

startable.forEach((source) => source.start());
closableOutputs.forEach((output) => output.close());
