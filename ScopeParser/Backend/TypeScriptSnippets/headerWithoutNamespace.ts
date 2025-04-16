    // __name__
    // Find a header with only the name
    let header___count___n = headers.find(header => header.name === "__name__");
    if (header___count___n) {
      res.push(header___count___n);
    } else {
      throw new Error(`Header not found: __name__. Available headers were: ${headers.map(header => header.name).join(", ")}`);
    }