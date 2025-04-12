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

// Async processor takes parameters as an array,
// and returns results as an array.
const processorAsync = createAsyncProcessor(30);
const result = await processorAsync({ users });
console.log(result);
