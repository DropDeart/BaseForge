import type { EntitySpec } from "../types";
import { RoleChips } from "./AccessEditor";

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

  return (
    <div>
      <div className="group-label">Liste filtreleri</div>
      {!paginated ? (
        <div className="hint">Filtreler yalnızca sayfalı listelerde kullanılabilir.</div>
      ) : candidates.length === 0 ? (
        <div className="hint">Filtrelenebilir alan yok (string, sayı, bool, guid, tarih, enum veya ilişki alanı ekleyin).</div>
      ) : (
        <>
          <div className="chips">
            {candidates.map((f) => (
              <button key={f.name} type="button" className={`chip ${filterable.includes(f.name) ? "on" : ""}`} onClick={() => toggleFilter(f.name)}>
                {f.name}
              </button>
            ))}
          </div>
          <div className="hint" style={{ marginTop: 4 }}>
            Seçilenler liste ucunda eşitlik filtresi olur
            {filterable.length > 0 && <> (örn. <code>?{filterable[0][0].toLowerCase() + filterable[0].slice(1)}=…</code>)</>}.
          </div>
        </>
      )}

      <div className="toggle-row" style={{ marginTop: 16 }}>
        <button
          className={`toggle ${rf ? "on" : ""}`}
          disabled={!rf && whereProps.length === 0}
          onClick={() =>
            onChange({
              ...entity,
              readFilter: rf ? null : { where: { [pascal(whereProps[0][0])]: defaultValueFor(whereProps[0][0]) }, bypassRoles: ["Admin"] },
            })
          }
        >
          <span className="knob" />
        </button>
        <span className="group-label" style={{ margin: 0 }}>Görünürlük filtresi</span>
      </div>
      {!rf ? (
        <div className="hint" style={{ marginTop: 4 }}>
          {whereProps.length === 0
            ? "Bool, enum, string veya sayı tipinde bir alan gerekir (örn. IsPublished)."
            : "Açılırsa list/getById yalnızca koşula uyan kayıtları gösterir (örn. taslakları gizler); gRPC etkilenmez."}
        </div>
      ) : (
        <div style={{ marginTop: 8 }}>
          {Object.entries(rf.where).map(([field, value]) => {
            const [propName, prop] = whereProps.find(([n]) => pascal(n) === pascal(field)) ?? [field, undefined];
            return (
              <div className="rel-row" key={field}>
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
                <span className="hint">=</span>
                {prop?.type === "bool" || prop?.type === "enum" ? (
                  <select className="uselect grow" value={value} onChange={(e) => setWhere({ ...rf.where, [field]: e.target.value })}>
                    {(prop.type === "bool" ? ["true", "false"] : prop.values ?? []).map((v) => (
                      <option key={v} value={v}>{v}</option>
                    ))}
                  </select>
                ) : (
                  <input className="uinput grow mono" value={value} onChange={(e) => setWhere({ ...rf.where, [field]: e.target.value })} />
                )}
                <button
                  className="icon-btn"
                  disabled={Object.keys(rf.where).length === 1}
                  title={Object.keys(rf.where).length === 1 ? "En az bir koşul gerekir" : "Koşulu kaldır"}
                  onClick={() => {
                    const next = { ...rf.where };
                    delete next[field];
                    setWhere(next);
                  }}
                >
                  ×
                </button>
              </div>
            );
          })}
          {whereProps.some(([n]) => !(pascal(n) in rf.where)) && (
            <button
              className="btn-link"
              onClick={() => {
                const [n] = whereProps.find(([p]) => !(pascal(p) in rf.where))!;
                setWhere({ ...rf.where, [pascal(n)]: defaultValueFor(n) });
              }}
            >
              + koşul (VE)
            </button>
          )}
          <div className="field" style={{ marginTop: 10 }}>
            <span className="field-label">Filtreye takılmadan her şeyi görenler</span>
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
                title: entity.ownerField ? "Sahibi kendi kayıtlarını her zaman görür" : "Önce Erişim bölümünden bir sahip alanı seçin",
              }}
            />
            <div className="hint" style={{ marginTop: 4 }}>Servisin süper rolleri otomatik dahildir.</div>
          </div>
        </div>
      )}
    </div>
  );
}
