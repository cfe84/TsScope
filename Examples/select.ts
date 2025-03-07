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

type Record = Field[];

interface IConsumer {
  receiveRecord(record: Record): void;
  receiveSchema(schema: QualifiedName[]): void;
}

type FieldsFilter = (field: QualifiedName) => boolean;
type RecordFilter = (record: Record) => boolean;

abstract class Source {
  private consumers: IConsumer[] = [];

  registerConsumer(consumer: IConsumer): void {
    this.consumers.push(consumer);
  }

  protected notifyConsumers(record: Record): void {
    this.consumers.forEach((consumer) => consumer.receiveRecord(record));
  }

  protected notifyConsumersSchema(schema: QualifiedName[]): void {
    this.consumers.forEach((consumer) => consumer.receiveSchema(schema));
  }
}

interface IStartable {
  start(): void;
}

class FileSource extends Source implements IStartable {
  private fields: QualifiedName[] = [];

  constructor(filePath: string, private filter: FieldsFilter) {
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
      this.notifyConsumersSchema(this.fields.filter(this.filter));
    } else {
      const record = this.fields
        .map((field, index) => {
          if (!this.filter(field)) {
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
    private fieldsFilter: FieldsFilter,
    private where?: RecordFilter
  ) {
    super();
    source.registerConsumer(this);
  }

  receiveSchema(schema: QualifiedName[]): void {
    this.notifyConsumersSchema(
      schema.filter((field) => this.fieldsFilter(field))
    );
  }

  receiveRecord(record: Record): void {
    if (this.where && !this.where(record)) {
      return;
    }
    const result = record.filter((field) => this.fieldsFilter(field.name));

    this.notifyConsumers(result);
  }
}

class NamedSource extends Source implements IConsumer {
  constructor(private source: Source, private name: string) {
    super();
    source.registerConsumer(this);
  }

  receiveRecord(record: Record): void {
    const newRecord = record.map((field) => ({
      name: { ...field.name, namespace: this.name },
      value: field.value,
    }));
    this.notifyConsumers(newRecord);
  }

  receiveSchema(schema: QualifiedName[]): void {
    const newSchema = schema.map((field) => ({
      ...field,
      namespace: this.name,
    }));
    this.notifyConsumersSchema(newSchema);
  }
}

interface IClosableOutput {
  close(): void;
}

class FileOutput implements IConsumer, IClosableOutput {
  constructor(private filePath: string) {}

  receiveRecord(record: Record): void {
    fs.appendFileSync(
      this.filePath,
      record.map((field) => field.value).join(",") + "\n"
    );
  }

  receiveSchema(schema: QualifiedName[]): void {
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

const input_0 = new NamedSource(new FileSource("input.csv", (field: string) => ["id", "firstName", "age"].includes(field)));
const fields_0 = new NamedSource(new SelectQuerySource(input_0, (field: string) => ["id", "firstName"].includes(field), null));
const filtered_0 = new NamedSource(new SelectQuerySource(input_0, (field: string) => ["firstName", "age"].includes(field), (record: any) => {
    Object.assign(globalThis, record);
    return age > 30
}));
const output_0 = new FileOutput("fields.csv");
fields_0.registerConsumer(output_0);
const output_1 = new FileOutput("input_copy.csv");
input_0.registerConsumer(output_1);
const output_2 = new FileOutput("filtered.csv");
filtered_0.registerConsumer(output_2);

///////////////////////////////////////////////
//                                           //
//             Resume boilerplate            //
//                                           //
///////////////////////////////////////////////

startable.forEach((source) => source.start());
closableOutputs.forEach((output) => output.close());
