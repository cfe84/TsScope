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
    { name: "Amelia", age: 24 }
  ];

const stream = createStream(30);
const records = stream.users_above_age.addIterator();

setTimeout(() => {
    for (let record of records) {
        console.log(record);
    }
}, 1);

users.forEach(stream.send);
