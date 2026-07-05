// Sırayı koruyarak record anahtarı yeniden adlandırma / ekleme / silme yardımcıları.

export function renameKey<T>(obj: Record<string, T>, oldKey: string, newKey: string): Record<string, T> {
  const out: Record<string, T> = {};
  for (const [k, v] of Object.entries(obj)) {
    out[k === oldKey ? newKey : k] = v;
  }
  return out;
}

export function setKey<T>(obj: Record<string, T>, key: string, value: T): Record<string, T> {
  return { ...obj, [key]: value };
}

export function removeKey<T>(obj: Record<string, T>, key: string): Record<string, T> {
  const out = { ...obj };
  delete out[key];
  return out;
}

export function uniqueKey(obj: Record<string, unknown>, base: string): string {
  if (!(base in obj)) return base;
  let i = 2;
  while (`${base}${i}` in obj) i++;
  return `${base}${i}`;
}

/** Spec tipini renk sınıfına eşler (inspector'da tip etiketi renklendirme). */
export function typeClass(type: string): string {
  switch (type) {
    case "string":
    case "text":
      return "type-string";
    case "int":
    case "long":
    case "short":
    case "decimal":
    case "double":
    case "float":
      return "type-number";
    case "datetime":
    case "date":
      return "type-date";
    case "bool":
      return "type-bool";
    case "guid":
    case "uuid":
      return "type-id";
    default:
      return "";
  }
}
