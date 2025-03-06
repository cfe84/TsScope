import * as fs from "fs";
import * as path from "path";

interface IConsumer {
  receiveRecord(record: Record<string, any>): void;
  receiveSchema(schema: string[]): void;
}

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

interface IStartableSource {
  start(): void;
}

class FileSource extends Source implements IStartableSource {
  private fields: string[] = [];

  constructor(filePath: string, private filter: (field: string) => boolean) {
    super();
    this.file = fs.createReadStream(filePath);
    startableSources.push(this);
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
      this.notifyConsumersSchema(this.fields);
      // TODO: filter fields
    } else {
      const record: Record<string, any> = {};
      this.fields.forEach((field, index) => {
        record[field] = valuesInLine[index];
      });
      this.notifyConsumers(record);
    }
  }

  private file: fs.ReadStream;
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

const startableSources: IStartableSource[] = [];
const closableOutputs: IClosableOutput[] = [];

const input_0 = new FileSource("input.csv", (_: string) => true);
const output_0 = new FileOutput("output.csv");
input_0.registerConsumer(output_0);

startableSources.forEach((source) => source.start());
closableOutputs.forEach((output) => output.close());
