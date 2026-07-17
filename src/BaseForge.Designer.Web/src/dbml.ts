import type { ServiceSpec } from "./types";

const DB_TYPE: Record<string, string> = {
  string: "varchar",
  text: "text",
  int: "integer",
  long: "bigint",
  short: "smallint",
  decimal: "numeric",
  double: "double",
  float: "real",
  bool: "boolean",
  datetime: "timestamptz",
  date: "date",
  guid: "uuid",
  uuid: "uuid",
};

const dbType = (t: string) => DB_TYPE[t] ?? t;
const camel = (s: string) => s.charAt(0).toLowerCase() + s.slice(1);
export const fkName = (target: string) => `${camel(target)}Id`;

/** ServiceSpec'i dbdiagram.io uyumlu DBML metnine çevirir. */
export function toDbml(spec: ServiceSpec): string {
  const entities = spec.entities ?? {};
  const fkCols: Record<string, Set<string>> = {};
  // Set: ilişki her iki entity'de de tanımlanmışsa (örn. Post.category many-to-one +
  // Category.posts one-to-many) aynı Ref satırı iki kez üretilmesin — dbdiagram.io
  // yinelenen ilişkiyi hata olarak reddediyor.
  const refs = new Set<string>();
  const add = (table: string, col: string) => (fkCols[table] ??= new Set()).add(col);

  for (const [name, e] of Object.entries(entities)) {
    for (const rel of Object.values(e.relations ?? {})) {
      if (!rel.target) continue;
      if (rel.kind === "many-to-one" || rel.kind === "one-to-one") {
        const col = fkName(rel.target);
        add(name, col);
        refs.add(`Ref: ${name}.${col} ${rel.kind === "one-to-one" ? "-" : ">"} ${rel.target}.id`);
      } else if (rel.kind === "one-to-many") {
        const col = fkName(name);
        add(rel.target, col);
        refs.add(`Ref: ${rel.target}.${col} > ${name}.id`);
      }
    }
  }

  const lines: string[] = [];
  for (const [name, e] of Object.entries(entities)) {
    lines.push(`Table ${name} {`);
    lines.push(`  id uuid [pk]`);
    for (const [p, prop] of Object.entries(e.props ?? {})) {
      const typeStr = dbType(prop.type) + (prop.maxLength ? `(${prop.maxLength})` : "");
      const attrs: string[] = [];
      if (!prop.nullable) attrs.push("not null");
      if (prop.default != null) attrs.push(`default: '${prop.default}'`);
      lines.push(`  ${p} ${typeStr}${attrs.length ? ` [${attrs.join(", ")}]` : ""}`);
    }
    for (const col of fkCols[name] ?? []) lines.push(`  ${col} uuid`);
    for (const [xn, x] of Object.entries(e.externalRefs ?? {})) {
      lines.push(`  ${x.store || `${xn}Id`} uuid [note: 'external: ${x.target} via ${x.via}']`);
    }
    // ServiceSpec.MultiTenant true iken CodeGen HER entity'ye bunu otomatik ekler
    // (bkz. CodeGenerator.BuildEntityModel) — YAML/spec'te elle tanımlanmaz, ER önizlemesi
    // gerçek üretilen şemayı yansıtsın diye burada da eklenir.
    if (spec.multiTenant) lines.push(`  TenantId uuid [not null]`);
    lines.push(`}`);
    lines.push("");
  }

  return [...lines, ...refs].join("\n").trim() + "\n";
}
