import * as fs from "fs";
import * as path from "path";

interface IConsumer {
  receiveRecord(record: Record<string, any>): void;
  receiveSchema(schema: string[]): void;
}

type FieldsFilter = (field: string) => boolean;
type RecordFilter = (record: Record<string, any>) => boolean;

abstract class Source {
  private consumers: IConsumer[] = [];

  registerConsumer(consumer: IConsumer): void {
    this.consumers.push(consumer);
  }

  protected notifyConsumers(record: Record<string, any>): void {
    this.consumers.forEach((consumer) => consumer.receiveRecord(record));
  }

  protected notifyConsumersSchema(schema: string[]): void {
    this.consumers.forEach((consumer) => consumer.receiveSchema(schema));
  }
}

interface IStartable {
  start(): void;
}

class FileSource extends Source implements IStartable {
  private fields: string[] = [];

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
      this.fields = valuesInLine;
      this.notifyConsumersSchema(this.fields.filter(this.filter));
    } else {
      const record: Record<string, any> = {};
      this.fields.forEach((field, index) => {
        if (!this.filter(field)) {
          return;
        }
        record[field] = valuesInLine[index];
      });
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

  receiveSchema(schema: string[]): void {
    this.notifyConsumersSchema(
      schema.filter((field) => this.fieldsFilter(field))
    );
  }

  receiveRecord(record: Record<string, any>): void {
    if (this.where && !this.where(record)) {
      return;
    }
    this.notifyConsumers(
      Object.fromEntries(
        Object.entries(record).filter(([field]) => this.fieldsFilter(field))
      )
    );
  }
}

interface IClosableOutput {
  close(): void;
}

class FileOutput implements IConsumer, IClosableOutput {
  constructor(private filePath: string) {}

  receiveRecord(record: Record<string, any>): void {
    fs.appendFileSync(this.filePath, Object.values(record).join(",") + "\n");
  }

  receiveSchema(schema: string[]): void {
    fs.writeFileSync(this.filePath, schema.join(",") + "\n");
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

const input_0 = new FileSource("input.csv", (field: string) => ["id", "firstName", "age"].includes(field));
const fields_0 = new SelectQuerySource(input_0, (field: string) => ["id", "firstName"].includes(field), null);
const filtered_0 = new SelectQuerySource(input_0, (field: string) => ["firstName", "age"].includes(field), (record: any) => {
    Object.assign(globalThis, record);
    return age > 30
});
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
