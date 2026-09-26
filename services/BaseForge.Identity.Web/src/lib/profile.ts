import type { ProfileField, ProfileValues } from "../types";

/** Form durumu: checkbox'lar boolean, diğer her şey input metni. */
export type ProfileFormState = Record<string, string | boolean>;

export const NUMERIC = ["int", "long", "short", "decimal", "double", "float"];

/** camelCase alan adını okunur etikete çevirir (yearsOfExperience → Years of experience). */
export function fieldLabel(name: string): string {
  const words = name.replace(/([a-z0-9])([A-Z])/g, "$1 $2").toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

/** ISO tarih-saatini datetime-local input biçimine (yerel saat, dakika hassasiyeti) çevirir. */
function toLocalInput(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** API'den gelen profil değerlerini form durumuna çevirir. */
export function toFormState(fields: ProfileField[], values: ProfileValues): ProfileFormState {
  const state: ProfileFormState = {};
  for (const f of fields) {
    const v = values[f.name];
    if (f.type === "bool") state[f.name] = v === true;
    else if (v === null || v === undefined) state[f.name] = "";
    else if (f.type === "datetime") state[f.name] = toLocalInput(String(v));
    else state[f.name] = String(v);
  }
  return state;
}

/** Form durumunu API gövdesine çevirir; yalnızca `include`'a uyan alanlar gönderilir. */
export function toApiValues(fields: ProfileField[], state: ProfileFormState, include: (f: ProfileField) => boolean): ProfileValues {
  const out: ProfileValues = {};
  for (const f of fields.filter(include)) {
    const v = state[f.name];
    if (f.type === "bool") {
      out[f.name] = v === true;
      continue;
    }
    const text = typeof v === "string" ? v : "";
    if (text === "" && !(f.type === "string" || f.type === "text")) {
      out[f.name] = null;
    } else if (NUMERIC.includes(f.type)) {
      // Geçersiz sayıyı metin olarak gönder — sunucu anlaşılır bir hata döner.
      out[f.name] = Number.isNaN(Number(text)) ? text : Number(text);
    } else if (f.type === "datetime") {
      out[f.name] = new Date(text).toISOString();
    } else {
      out[f.name] = f.nullable && text.trim() === "" ? null : text;
    }
  }
  return out;
}
