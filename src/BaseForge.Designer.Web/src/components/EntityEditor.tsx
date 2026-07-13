import { useState } from "react";
import type { EntitySpec, Meta, PropSpec } from "../types";
import { removeKey, renameKey, setKey, typeClass, uniqueKey } from "../util";

interface Props {
  name: string;
  entity: EntitySpec;
  meta: Meta;
  allEntities: string[];
  onRename: (newName: string) => void;
  onRemove: () => void;
  onChange: (entity: EntitySpec) => void;
}

export function EntityEditor({ name, entity, meta, allEntities, onRename, onRemove, onChange }: Props) {
  const props = entity.props ?? {};
  const relations = entity.relations ?? {};
  const externalRefs = entity.externalRefs ?? {};
  const others = allEntities.filter((e) => e !== name);
  const [expandedProp, setExpandedProp] = useState<string | null>(null);

  // Backend'de varsayılan true — alan yoksa (yeni entity) açık kabul edilir.
  const paginated = entity.paginated !== false;
  const sortable = entity.sortable !== false;
  const searchable = entity.searchable !== false;

  const listToggle = (label: string, value: boolean, enabled: boolean, onToggle: () => void) => (
    <div className="toggle-row" style={enabled ? undefined : { opacity: 0.4 }}>
      <button className={`toggle ${value ? "on" : ""}`} disabled={!enabled} onClick={onToggle}>
        <span className="knob" />
      </button>
      <span className="hint">{label}</span>
    </div>
  );

  const updateProp = (pName: string, patch: Partial<PropSpec>) =>
    onChange({ ...entity, props: setKey(props, pName, { ...props[pName], ...patch }) });

  return (
    <>
      {/* Entity name */}
      <div className="divider">
        <div className="field-row" style={{ alignItems: "flex-end" }}>
          <div className="field">
            <span className="field-label">Entity adı</span>
            <input className="uinput mono" value={name} onChange={(e) => onRename(e.target.value)} />
          </div>
          <div style={{ flex: 0 }}>
            <button className="btn" onClick={onRemove}>Entity'yi sil</button>
          </div>
        </div>
        <div className="field-row" style={{ marginTop: 12, gap: 24 }}>
          {listToggle("Sayfalama", paginated, true, () => onChange({ ...entity, paginated: !paginated }))}
          {listToggle("Sıralama", sortable, paginated, () => onChange({ ...entity, sortable: !sortable }))}
          {listToggle("Arama", searchable, paginated, () => onChange({ ...entity, searchable: !searchable }))}
        </div>
      </div>

      {/* Properties */}
      <div>
        <div className="group-head">
          <span className="group-label">Alanlar — {name}</span>
          <button
            className="btn-link"
            onClick={() => onChange({ ...entity, props: setKey(props, uniqueKey(props, "field"), { type: meta.types[0] }) })}
          >
            + alan
          </button>
        </div>
        <div className="hint" style={{ marginBottom: 8 }}>
          Id, CreatedAt, UpdatedAt gibi audit alanları BaseEntity'den gelir.
        </div>
        {Object.entries(props).map(([pName, pSpec], index) => {
          const isOpen = expandedProp === pName;
          const isStringLike = pSpec.type === "string" || pSpec.type === "text";
          return (
            <div className="prop-row-wrap" key={index}>
              <div className="prop-row">
                <input className="uinput mono" value={pName} onChange={(e) => onChange({ ...entity, props: renameKey(props, pName, e.target.value) })} />
                <select
                  className={`uselect type-select ${typeClass(pSpec.type)}`}
                  value={pSpec.type}
                  onChange={(e) => updateProp(pName, { type: e.target.value })}
                >
                  {meta.types.map((t) => (
                    <option key={t} value={t}>{t}</option>
                  ))}
                </select>
                <div className="prop-row-actions">
                  <button
                    className="icon-btn"
                    title="Gelişmiş ayarlar (nullable / maxLength / default)"
                    onClick={() => setExpandedProp(isOpen ? null : pName)}
                  >
                    ⚙
                  </button>
                  <button className="icon-btn" onClick={() => onChange({ ...entity, props: removeKey(props, pName) })}>Sil</button>
                </div>
              </div>
              {isOpen && (
                <div className="prop-adv">
                  <label>
                    <input
                      type="checkbox"
                      checked={!!pSpec.nullable}
                      onChange={(e) => updateProp(pName, { nullable: e.target.checked })}
                    />
                    nullable
                  </label>
                  {isStringLike ? (
                    <input
                      className="uinput"
                      type="number"
                      min={1}
                      placeholder="maxLength"
                      value={pSpec.maxLength ?? ""}
                      onChange={(e) => updateProp(pName, { maxLength: e.target.value === "" ? null : Number(e.target.value) })}
                    />
                  ) : (
                    <span />
                  )}
                  <input
                    className="uinput"
                    placeholder="default değer"
                    value={pSpec.default ?? ""}
                    onChange={(e) => updateProp(pName, { default: e.target.value === "" ? null : e.target.value })}
                  />
                </div>
              )}
            </div>
          );
        })}
        {Object.keys(props).length === 0 && <div className="hint">Henüz alan yok.</div>}
      </div>

      {/* Relations + External refs side by side */}
      <div className="field-row" style={{ gap: 40 }}>
        <div className="field">
          <div className="group-head">
            <span className="group-label">İlişkiler</span>
            {others.length > 0 && (
              <button
                className="btn-link"
                onClick={() =>
                  onChange({
                    ...entity,
                    relations: setKey(relations, uniqueKey(relations, "related"), { kind: meta.relationKinds[0], target: others[0] }),
                  })
                }
              >
                + ilişki
              </button>
            )}
          </div>
          {Object.entries(relations).map(([rName, rel], index) => (
            <div className="rel-row" key={index}>
              <input className="uinput mono" style={{ width: 90 }} value={rName} onChange={(e) => onChange({ ...entity, relations: renameKey(relations, rName, e.target.value) })} />
              <select className="uselect" value={rel.kind} onChange={(e) => onChange({ ...entity, relations: setKey(relations, rName, { ...rel, kind: e.target.value }) })}>
                {meta.relationKinds.map((k) => (
                  <option key={k} value={k}>{k}</option>
                ))}
              </select>
              <select className="uselect grow" value={rel.target} onChange={(e) => onChange({ ...entity, relations: setKey(relations, rName, { ...rel, target: e.target.value }) })}>
                <option value="">— hedef —</option>
                {others.map((t) => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
              <button className="icon-btn" onClick={() => onChange({ ...entity, relations: removeKey(relations, rName) })}>×</button>
            </div>
          ))}
          {others.length === 0 && <div className="hint">İlişki için en az bir başka entity gerekir.</div>}
        </div>

        <div className="field">
          <div className="group-head">
            <span className="group-label">Dış referanslar</span>
            <button
              className="btn-link"
              onClick={() =>
                onChange({
                  ...entity,
                  externalRefs: setKey(externalRefs, uniqueKey(externalRefs, "ref"), { target: "customers/Customer", store: "CustomerId", via: meta.via[0] }),
                })
              }
            >
              + referans
            </button>
          </div>
          <div className="hint" style={{ marginBottom: 4 }}>FK üretilmez; sadece ID + grpc/event.</div>
          {Object.entries(externalRefs).map(([xName, x], index) => (
            <div className="ext-row" key={index}>
              <input className="uinput mono" style={{ width: 80 }} value={xName} onChange={(e) => onChange({ ...entity, externalRefs: renameKey(externalRefs, xName, e.target.value) })} />
              <input className="uinput grow" placeholder="servis/Entity" value={x.target} onChange={(e) => onChange({ ...entity, externalRefs: setKey(externalRefs, xName, { ...x, target: e.target.value }) })} />
              <select className="uselect" value={x.via} onChange={(e) => onChange({ ...entity, externalRefs: setKey(externalRefs, xName, { ...x, via: e.target.value }) })}>
                {meta.via.map((v) => (
                  <option key={v} value={v}>{v}</option>
                ))}
              </select>
              <button className="icon-btn" onClick={() => onChange({ ...entity, externalRefs: removeKey(externalRefs, xName) })}>×</button>
            </div>
          ))}
        </div>
      </div>
    </>
  );
}
