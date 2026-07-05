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
  const refs: string[] = [];
  const add = (table: string, col: string) => (fkCols[table] ??= new Set()).add(col);

  for (const [name, e] of Object.entries(entities)) {
    for (const rel of Object.values(e.relations ?? {})) {
      if (!rel.target) continue;
      if (rel.kind === "many-to-one" || rel.kind === "one-to-one") {
        const col = fkName(rel.target);
        add(name, col);
        refs.push(`Ref: ${name}.${col} ${rel.kind === "one-to-one" ? "-" : ">"} ${rel.target}.id`);
      } else if (rel.kind === "one-to-many") {
        const col = fkName(name);
        add(rel.target, col);
        refs.push(`Ref: ${rel.target}.${col} > ${name}.id`);
      }
    }
  }

  const lines: string[] = [];
  for (const [name, e] of Object.entries(entities)) {
    lines.push(`Table ${name} {`);
    lines.push(`  id uuid [pk]`);
    for (const [p, t] of Object.entries(e.props ?? {})) lines.push(`  ${p} ${dbType(t)}`);
    for (const col of fkCols[name] ?? []) lines.push(`  ${col} uuid`);
    for (const [xn, x] of Object.entries(e.externalRefs ?? {})) {
      lines.push(`  ${x.store || `${xn}Id`} uuid [note: 'external: ${x.target} via ${x.via}']`);
    }
    lines.push(`}`);
    lines.push("");
  }

  return [...lines, ...refs].join("\n").trim() + "\n";
}
