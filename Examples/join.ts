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

type Record = Field[];

interface IConsumer {
  receiveRecord(record: Record): void;
  receiveSchema(schema: QualifiedName[]): void;
}

// Check if field should be included
type FieldsFilter = (field: QualifiedName) => boolean;
// Check if all the fields we're expecting are in the list.
type FieldCheck = (field: QualifiedName[]) => string | true;
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

  receiveSchema(schema: QualifiedName[]): void {
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

  receiveRecord(record: Record): void {
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

const input_0 = new NamedSource(new FileSource("input.csv", {
    fieldFilter: (_: QualifiedName) => true,
    missingFields: (_: QualifiedName[]) => ({result: [], position: ""}),
}), "input");
const fields_0 = new NamedSource(new SelectQuerySource(input_0, {
    fieldFilter: (field: QualifiedName) => ["id", "firstName"]
        .includes(field.name) || ["id", "firstName"].includes(`${field.namespace}.${field.name}`),
    missingFields: (fields: QualifiedName[]) => {
        const fieldNames = fields.map((field) => field.name);
        const qualifiedNames = fields.map((field) => field.namespace + "." + field.name);
        const result = ["id", "firstName"].filter((field) => !(qualifiedNames.includes(field) || fieldNames.includes(field)));
        return { result, position: "Identifier \"id\" (line 2, column 17)" };
    }
}, undefined), "fields");
const fields_1 = new NamedSource(new SelectQuerySource(fields_0, {
    fieldFilter: (field: QualifiedName) => ["fields.id", "firstName", "age"]
        .includes(field.name) || ["fields.id", "firstName", "age"].includes(`${field.namespace}.${field.name}`),
    missingFields: (fields: QualifiedName[]) => {
        const fieldNames = fields.map((field) => field.name);
        const qualifiedNames = fields.map((field) => field.namespace + "." + field.name);
        const result = ["fields.id", "firstName", "age"].filter((field) => !(qualifiedNames.includes(field) || fieldNames.includes(field)));
        return { result, position: "Identifier \"fields\" (line 3, column 17)" };
    }
}, undefined), "fields");
const output_0 = new FileOutput("fields.csv");
fields_1.registerConsumer(output_0);

///////////////////////////////////////////////
//                                           //
//             Resume boilerplate            //
//                                           //
///////////////////////////////////////////////

startable.forEach((source) => source.start());
closableOutputs.forEach((output) => output.close());
