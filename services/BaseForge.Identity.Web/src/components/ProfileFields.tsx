import { Label } from "./ui/label";
import { Input } from "./ui/input";
import { NUMERIC, fieldLabel, type ProfileFormState } from "../lib/profile";
import type { ProfileField } from "../types";

interface Props {
  fields: ProfileField[];
  state: ProfileFormState;
  onChange: (next: ProfileFormState) => void;
  /** Bu alan düzenlenebilir mi? (profil sayfasında admin-only alanlar salt okunur) */
  editable: (f: ProfileField) => boolean;
}

/** auth.yaml userProfile alanlarını metadata'dan dinamik çizen form alanları. */
export function ProfileFields({ fields, state, onChange, editable }: Props) {
  const set = (name: string, value: string | boolean) => onChange({ ...state, [name]: value });
  const inputClass = "flex flex-col gap-1.5";
  const labelClass = "text-xs font-medium text-slate-500";

  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {fields.map((f) => {
        const id = `profile-${f.name}`;
        const disabled = !editable(f);
        const hint = disabled ? "Yalnızca yönetici değiştirebilir" : !f.nullable && f.type !== "bool" ? "Zorunlu" : undefined;
        const value = state[f.name];

        if (f.type === "bool") {
          return (
            <label key={f.name} className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2">
              <input
                id={id}
                type="checkbox"
                checked={value === true}
                disabled={disabled}
                onChange={(e) => set(f.name, e.target.checked)}
                className="h-4 w-4 accent-emerald-600"
              />
              {fieldLabel(f.name)}
              {disabled && <span className="text-xs text-slate-400">— {hint}</span>}
            </label>
          );
        }

        const text = typeof value === "string" ? value : "";
        let control;
        if (f.type === "enum") {
          control = (
            <select
              id={id}
              value={text}
              disabled={disabled}
              onChange={(e) => set(f.name, e.target.value)}
              className="h-8 rounded-lg border border-input bg-transparent px-2 text-sm outline-none disabled:cursor-not-allowed disabled:opacity-50"
            >
              {(f.nullable || text === "") && <option value="">—</option>}
              {f.values.map((v) => (
                <option key={v} value={v}>{v}</option>
              ))}
            </select>
          );
        } else if (f.type === "text") {
          control = (
            <textarea
              id={id}
              value={text}
              disabled={disabled}
              maxLength={f.maxLength ?? undefined}
              rows={3}
              onChange={(e) => set(f.name, e.target.value)}
              className="rounded-lg border border-input bg-transparent px-2.5 py-1.5 text-sm outline-none disabled:cursor-not-allowed disabled:opacity-50"
            />
          );
        } else {
          const type = NUMERIC.includes(f.type) ? "number" : f.type === "date" ? "date" : f.type === "datetime" ? "datetime-local" : "text";
          control = (
            <Input
              id={id}
              type={type}
              step={["decimal", "double", "float"].includes(f.type) ? "any" : undefined}
              value={text}
              disabled={disabled}
              maxLength={f.maxLength ?? undefined}
              onChange={(e) => set(f.name, e.target.value)}
            />
          );
        }

        return (
          <div key={f.name} className={`${inputClass} ${f.type === "text" ? "sm:col-span-2" : ""}`}>
            <Label htmlFor={id} className={labelClass}>
              {fieldLabel(f.name)}
              {hint && <span className="ml-1 font-normal text-slate-400">· {hint}</span>}
            </Label>
            {control}
          </div>
        );
      })}
    </div>
  );
}
