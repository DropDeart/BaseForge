import { Fragment } from "react";
import type { EntitySpec } from "../types";
import { RoleChips } from "./AccessEditor";
import { useT } from "../i18n";

// Liste filtreleri (filterable) ve okuma görünürlüğü (readFilter) — bkz. docs/ARCH.md §6.2.

const FILTERABLE_TYPES = ["string", "int", "long", "short", "bool", "guid", "uuid", "date", "enum"];
const WHERE_TYPES = ["bool", "enum", "string", "int", "long", "short"];

const pascal = (s: string) => (s ? s[0].toUpperCase() + s.slice(1) : s);

/** SpecValidator.ScalarFieldTypes ile aynı: props + many/one-to-one FK'leri + dış referans alanları. */
function scalarFields(entity: EntitySpec): { name: string; type: string }[] {
  const out = Object.entries(entity.props ?? {}).map(([n, p]) => ({ name: pascal(n), type: p.type }));
  for (const [n, r] of Object.entries(entity.relations ?? {})) {
    if (r.kind === "many-to-one" || r.kind === "one-to-one") out.push({ name: `${pascal(n)}Id`, type: "guid" });
  }
  for (const x of Object.values(entity.externalRefs ?? {})) {
    if (x.store) out.push({ name: pascal(x.store), type: "guid" });
  }
  return out;
}

export function ListOptionsEditor({
  entity,
  knownRoles,
  onChange,
}: {
  entity: EntitySpec;
  knownRoles: string[];
  onChange: (entity: EntitySpec) => void;
}) {
  const m = useT();
  const paginated = entity.paginated !== false;
  const filterable = entity.filterable ?? [];
  const candidates = scalarFields(entity).filter((f) => FILTERABLE_TYPES.includes(f.type));
  const whereProps = Object.entries(entity.props ?? {}).filter(([, p]) => WHERE_TYPES.includes(p.type));
  const rf = entity.readFilter;

  const toggleFilter = (name: string) =>
    onChange({ ...entity, filterable: filterable.includes(name) ? filterable.filter((f) => f !== name) : [...filterable, name] });

  const setWhere = (where: Record<string, string>) => onChange({ ...entity, readFilter: { ...rf!, where } });

  const defaultValueFor = (propName: string) => {
    const p = entity.props[propName];
    return p.type === "bool" ? "true" : p.type === "enum" ? (p.values?.[0] ?? "") : "";
  };

  const camel = (s: string) => s[0].toLowerCase() + s.slice(1);
  const exampleQuery = filterable.map((f) => `${camel(f)}=…`).join("&");
  const whereCount = rf ? Object.keys(rf.where).length : 0;

  return (
    <div className="opt-stack">
      {/* Liste filtreleri */}
      <section className="opt-card">
        <div className="opt-head">
          <div>
            <div className="opt-title">{m.list.filtersTitle}</div>
            <div className="opt-desc">
              {m.list.filtersDescBefore} <b>{m.list.filtersDescStrong}</b> {m.list.filtersDescAfter}
            </div>
          </div>
          {paginated && candidates.length > 0 && <span className="opt-count">{m.list.selected(filterable.length, candidates.length)}</span>}
        </div>

        {!paginated ? (
          <div className="opt-empty">{m.list.needsPagination}</div>
        ) : candidates.length === 0 ? (
          <div className="opt-empty">{m.list.noCandidates}</div>
        ) : (
          <>
            <div className="chips roomy">
              {candidates.map((f) => (
                <button key={f.name} type="button" className={`chip ${filterable.includes(f.name) ? "on" : ""}`} onClick={() => toggleFilter(f.name)}>
                  {f.name}
                  <span className="chip-type">{f.type}</span>
                </button>
              ))}
            </div>
            <div className="opt-example">
              GET /api/…?{filterable.length > 0 ? exampleQuery : <span className="opt-muted">{m.list.pickField}</span>}
            </div>
          </>
        )}
      </section>

      {/* Görünürlük filtresi */}
      <section className="opt-card">
        <div className="opt-head">
          <div>
            <div className="opt-title">{m.list.visibilityTitle}</div>
            <div className="opt-desc">{m.list.visibilityDesc}</div>
          </div>
          <button
            className={`toggle ${rf ? "on" : ""}`}
            disabled={!rf && whereProps.length === 0}
            title={rf ? m.list.turnOff : m.list.turnOn}
            onClick={() =>
              onChange({
                ...entity,
                readFilter: rf ? null : { where: { [pascal(whereProps[0][0])]: defaultValueFor(whereProps[0][0]) }, bypassRoles: ["Admin"] },
              })
            }
          >
            <span className="knob" />
          </button>
        </div>

        {!rf ? (
          whereProps.length === 0 && <div className="opt-empty">{m.list.visibilityNeedsField}</div>
        ) : (
          <>
            <div className="opt-sub-title">{m.list.condition} {whereCount > 1 && <span className="opt-muted">{m.list.allMustMatch}</span>}</div>
            <div className="where-grid">
              <span className="field-label">{m.list.field}</span>
              <span />
              <span className="field-label">{m.list.value}</span>
              <span />
              {Object.entries(rf.where).map(([field, value]) => {
                const [propName, prop] = whereProps.find(([n]) => pascal(n) === pascal(field)) ?? [field, undefined];
                return (
                  <Fragment key={field}>
                    <select
                      className="uselect"
                      value={pascal(propName)}
                      onChange={(e) => {
                        const next = { ...rf.where };
                        delete next[field];
                        next[e.target.value] = defaultValueFor(whereProps.find(([n]) => pascal(n) === e.target.value)![0]);
                        setWhere(next);
                      }}
                    >
                      {whereProps.map(([n]) => (
                        <option key={n} value={pascal(n)}>{pascal(n)}</option>
                      ))}
                    </select>
                    <span className="where-eq">=</span>
                    {prop?.type === "bool" || prop?.type === "enum" ? (
                      <select className="uselect" value={value} onChange={(e) => setWhere({ ...rf.where, [field]: e.target.value })}>
                        {(prop.type === "bool" ? ["true", "false"] : prop.values ?? []).map((v) => (
                          <option key={v} value={v}>{v}</option>
                        ))}
                      </select>
                    ) : (
                      <input className="uinput mono" value={value} onChange={(e) => setWhere({ ...rf.where, [field]: e.target.value })} />
                    )}
                    <button
                      className="icon-btn"
                      disabled={whereCount === 1}
                      title={whereCount === 1 ? m.list.needOneCondition : m.list.removeCondition}
                      onClick={() => {
                        const next = { ...rf.where };
                        delete next[field];
                        setWhere(next);
                      }}
                    >
                      ×
                    </button>
                  </Fragment>
                );
              })}
            </div>
            {whereProps.some(([n]) => !(pascal(n) in rf.where)) && (
              <button
                className="btn-link"
                style={{ alignSelf: "flex-start" }}
                onClick={() => {
                  const [n] = whereProps.find(([p]) => !(pascal(p) in rf.where))!;
                  setWhere({ ...rf.where, [pascal(n)]: defaultValueFor(n) });
                }}
              >
                {m.list.addCondition}
              </button>
            )}

            <div className="opt-sub">
              <div className="opt-sub-title">{m.list.bypassTitle}</div>
              <RoleChips
                roles={knownRoles}
                // "sahibi" çipi spec'te ayrı bir bayrak (bypassOwner); rol listesine karışmaz.
                selected={[...(rf.bypassRoles ?? []), ...(rf.bypassOwner ? ["owner"] : [])]}
                onChange={(next) =>
                  onChange({
                    ...entity,
                    readFilter: { ...rf, bypassRoles: next.filter((r) => r !== "owner"), bypassOwner: next.includes("owner") },
                  })
                }
                owner={{
                  enabled: !!entity.ownerField,
                  title: entity.ownerField ? m.list.ownerSeesOwn : m.list.pickOwnerInAccess,
                }}
              />
              <div className="opt-desc">{m.list.superRolesIncluded}</div>
            </div>
          </>
        )}
      </section>
    </div>
  );
}
