{
    fieldFilter: (field: QualifiedName) => [__fields__]
        // TODO: Fix for namespace 
        .includes(field.name) || [__fields__].includes(`${field.namespace}.${field.name}`),
    missingFields: (fields: QualifiedName[]) => {
        const fieldNames = fields.map((field) => field.name);
        const qualifiedNames = fields.map((field) => field.namespace + "." + field.name);
        // TODO: Fix for namespace 
        const result = [__fields__].filter((field) => !(qualifiedNames.includes(field) || fieldNames.includes(field)));
        return { result, position: "__position__" };
    }
}