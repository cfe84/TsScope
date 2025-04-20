import { createStream } from "./compiled/library_mode--basic_select.ts";

const users = [
  { name: "Rebecca", age: 30 },
  { name: "Ethan", age: 25 },
  { name: "Olivia", age: 35 },
  { name: "Noah", age: 22 },
  { name: "Isabella", age: 28 },
  { name: "James", age: 40 },
  { name: "Sophia", age: 27 },
  { name: "Alexander", age: 32 },
  { name: "Mia", age: 29 },
  { name: "William", age: 31 },
  { name: "Amelia", age: 24 },
];

async function runAsync() {
  const stream = createStream(30);
  const recordsProcessor = stream.users_above_age.getAsyncIterator();

  setTimeout(async () => {
    console.log("Starting to iterate over records");
    let record: any;
    let max = 10;
    while ((record = await recordsProcessor.next()).done === false) {
      console.log(record);
    }
    console.log("Finished iterating over records");
  }, 1);

  users.forEach(stream.users.send);
  stream.users.close();
}

runAsync().then();
