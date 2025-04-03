import * as fs from "fs";
import * as path from "path";

/*************
 * The following code is a boilerplate for the script.
 * "Custom" code is inserted lower down.
 */

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
  value: string;
}

interface FieldsSpec {
  fieldFilter: (field: QualifiedName) => boolean;
  missingFields: (field: QualifiedName[]) => {
    result: string[];
    position: string;
  };
}

abstract class RecordMapper {
  abstract map(record: SourceRecord): SourceRecord;

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
  map = (record: SourceRecord) => record;
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

  protected notifyConsumers(record: SourceRecord): void {
    this.consumers.forEach((consumer) => consumer.receiveRecord(this, record));
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
    } else {
      const thisRecord = valuesInLine.map((value, i) => ({
        name: this.fields[i],
        value,
      }));
      const record = this.recordMapper.map(thisRecord);
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

  receiveRecord(_: Source, record: SourceRecord): void {
    if (this.where && !this.where(record)) {
      return;
    }

    const mappedRecord = this.recordMapper.map(record);

    this.notifyConsumers(mappedRecord);
  }
}

class NamedSource extends Source implements IConsumer {
  constructor(private source: Source, private name: string) {
    super();
    source.registerConsumer(this);
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
  Left = "Left",
  Right = "Right",
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
}

interface IClosableOutput {
  close(): void;
}

class FileOutput implements IConsumer, IClosableOutput {
  private wroteHeader: boolean = false;

  constructor(private filePath: string) {}

  done(source: Source): void {
    this.close();
  }

  receiveRecord(_: Source, record: SourceRecord): void {
    if (!this.wroteHeader) {
      this.writeHeader(
        _,
        record.map((field) => field.name)
      );
    }

    fs.appendFileSync(
      this.filePath,
      record.map((field) => field.value).join(",") + "\n"
    );
  }

  private writeHeader(_: Source, schema: QualifiedName[]): void {
    this.wroteHeader = true;
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

// This is where your script code starts.

class RecordMapper_0 extends RecordMapper {
  map(input: SourceRecord): SourceRecord {
    const output: SourceRecord = [
      {
        name: {
          namespace: undefined,
          name: "id",
        },
        value: this.findField(input, undefined, "id"),
      },
      {
        name: {
          namespace: undefined,
          name: "firstName",
        },
        value: this.findField(input, undefined, "firstName"),
      },
      {
        name: {
          namespace: undefined,
          name: "roleId",
        },
        value: this.findField(input, undefined, "roleId"),
      },
      {
        name: {
          namespace: undefined,
          name: "age",
        },
        value: this.findField(input, undefined, "age"),
      },
    ];
    return output;
  }
}

const RecordMapper_1 = RecordMapper_0;

const input_0 = new NamedSource(
  new FileSource("inputs/users.csv", new RecordMapper_0()),
  "input"
);
const output_0 = new FileOutput("outputs/input-output_load_and_output.csv");
input_0.registerConsumer(output_0);
const output_1 = new FileOutput("outputs/input-output_direct_copy.csv");
new FileSource("inputs/users.csv", new RecordMapper_1()).registerConsumer(
  output_1
);

///////////////////////////////////////////////
//                                           //
//             Resume boilerplate            //
//                                           //
///////////////////////////////////////////////

startable.forEach((source) => source.start());
closableOutputs.forEach((output) => output.close());
