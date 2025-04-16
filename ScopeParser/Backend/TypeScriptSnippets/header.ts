    // __namespace__.__name__
    // Find a header with both namespace and name
    let header___count___ns_and_n = headers.filter(header => header.namespace).find(header => header.namespace === "__namespace__" && header.name === "__name__");
    if (header___count___ns_and_n) {
      res.push(header___count___ns_and_n);
    } else {
        // Find a header with only the name
      let header___count___n = headers.find(header => header.name === "__name__");
      if (header___count___n) {
        res.push(header___count___n);
      } else {
        throw new Error(`Header not found: __namespace__.__name__. Available headers were: ${headers.join(", ")}`);
      }
    }