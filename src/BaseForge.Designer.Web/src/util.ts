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
