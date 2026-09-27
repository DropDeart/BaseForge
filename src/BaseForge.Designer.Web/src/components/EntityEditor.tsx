import { useEffect, useState } from "react";
import type { EntitySpec, Meta, PropSpec, ServiceAuthSpec } from "../types";
import { removeKey, renameKey, setKey, typeClass, uniqueKey } from "../util";
import { EntityAccessEditor } from "./AccessEditor";
import { ListOptionsEditor } from "./ListOptionsEditor";
import { useT } from "../i18n";

interface Props {
  name: string;
  entity: EntitySpec;
  meta: Meta;
  allEntities: string[];
  /** Servisin JWT ayarı; yoksa erişim bölümü "herkese açık" uyarısı gösterir. */
  auth?: ServiceAuthSpec | null;
  /** Identity'de tanımlı roller (rol seçicilerinde gösterilir). */
  knownRoles: string[];
  onRename: (newName: string) => void;
  onRemove: () => void;
  onChange: (entity: EntitySpec) => void;
}

export function EntityEditor({ name, entity, meta, allEntities, auth, knownRoles, onRename, onRemove, onChange }: Props) {
  const m = useT();
  const props = entity.props ?? {};
  const relations = entity.relations ?? {};
  const externalRefs = entity.externalRefs ?? {};
  const counters = entity.counters ?? [];
  const others = allEntities.filter((e) => e !== name);
  const [expandedProp, setExpandedProp] = useState<string | null>(null);

  // Ad girişi yerel taslak tutar: boş veya çakışan ad (geçici olarak) yazılabilsin, yalnızca geçerliyse uygulansın.
  // (Önceden input doğrudan spec'e bağlıydı; boş ad reddedildiği için son harf silinemiyordu.)
  const [nameDraft, setNameDraft] = useState(name);
  useEffect(() => setNameDraft(name), [name]);
  const nameProblem = !nameDraft.trim() ? m.entity.nameEmpty : others.includes(nameDraft) ? m.entity.nameTaken : null;
  const changeName = (value: string) => {
    setNameDraft(value);
    if (value.trim() && !others.includes(value)) onRename(value);
  };

  // Backend'de varsayılan true — alan yoksa (yeni entity) açık kabul edilir.
  const paginated = entity.paginated !== false;
  const sortable = entity.sortable !== false;
  const searchable = entity.searchable !== false;
  const appendOnly = !!entity.appendOnly;

  const listToggle = (label: string, value: boolean, enabled: boolean, onToggle: () => void) => (
    <div className="toggle-row" style={enabled ? undefined : { opacity: 0.4 }}>
      <button className={`toggle ${value ? "on" : ""}`} disabled={!enabled} onClick={onToggle}>
        <span className="knob" />
      </button>
      <span className="hint">{label}</span>
    </div>
  );

  const updateProp = (pName: string, patch: Partial<PropSpec>) =>
    onChange({
      ...entity,
      // 'values' yalnızca enum'da geçerli — tip enum'dan çıkarsa listeyi bırakma (SpecValidator hatası olurdu).
      props: setKey(props, pName, {
        ...props[pName],
        ...patch,
        ...(patch.type && patch.type !== "enum" ? { values: undefined } : {}),
      }),
      // counter yalnızca int alanlarda anlamlı — tip int'ten başka bir şeye değişirse geçersiz
      // bir spec'e (SpecValidator hatası) düşmemek için işaret otomatik kaldırılır.
      counters: patch.type && patch.type !== "int" ? counters.filter((c) => c !== pName) : counters,
    });

  const renameProp = (pName: string, newName: string) =>
    onChange({
      ...entity,
      props: renameKey(props, pName, newName),
      counters: counters.map((c) => (c === pName ? newName : c)),
      ownerField: entity.ownerField === pName ? newName : entity.ownerField,
    });

  const removeProp = (pName: string) =>
    onChange({
      ...entity,
      props: removeKey(props, pName),
      counters: counters.filter((c) => c !== pName),
      // Sahip alanı silinirse bağlantıyı kopar (aksi halde SpecValidator "bu adda bir prop değil" hatası verir).
      ownerField: entity.ownerField === pName ? null : entity.ownerField,
      readFilter:
        entity.readFilter && entity.ownerField === pName ? { ...entity.readFilter, bypassOwner: false } : entity.readFilter,
      filterable: (entity.filterable ?? []).filter((f) => f.toLowerCase() !== pName.toLowerCase()),
    });

  const toggleCounter = (pName: string) =>
    onChange({
      ...entity,
      counters: counters.includes(pName) ? counters.filter((c) => c !== pName) : [...counters, pName],
    });

  return (
    <>
      {/* Entity name */}
      <div className="divider">
        <div className="field-row" style={{ alignItems: "flex-end" }}>
          <div className="field">
            <span className="field-label">{m.entity.name}</span>
            <input
              className="uinput mono"
              value={nameDraft}
              onChange={(e) => changeName(e.target.value)}
              // Geçersiz bir taslakla çıkılırsa son geçerli ada dön.
              onBlur={() => nameProblem && setNameDraft(name)}
              style={nameProblem ? { borderBottomColor: "var(--red)" } : undefined}
            />
            {nameProblem && <span className="hint" style={{ color: "var(--red)" }}>{nameProblem}</span>}
          </div>
          <div style={{ flex: 0 }}>
            <button className="btn" onClick={onRemove}>{m.entity.remove}</button>
          </div>
        </div>
        <div className="field-row" style={{ marginTop: 12, gap: 24 }}>
          {listToggle(m.entity.pagination, paginated, true, () => onChange({ ...entity, paginated: !paginated }))}
          {listToggle(m.entity.sorting, sortable, paginated, () => onChange({ ...entity, sortable: !sortable }))}
          {listToggle(m.entity.search, searchable, paginated, () => onChange({ ...entity, searchable: !searchable }))}
          {listToggle(m.entity.appendOnly, appendOnly, true, () => onChange({ ...entity, appendOnly: !appendOnly }))}
        </div>
        {appendOnly && (
          <div className="hint" style={{ marginTop: 4 }}>
            {m.entity.appendOnlyHint}
          </div>
        )}
      </div>

      {/* Properties */}
      <div>
        <div className="group-head">
          <span className="group-label">{m.entity.fields(name)}</span>
          <button
            className="btn-link"
            onClick={() => onChange({ ...entity, props: setKey(props, uniqueKey(props, "field"), { type: meta.types[0] }) })}
          >
            {m.common.addField}
          </button>
        </div>
        <div className="hint" style={{ marginBottom: 8 }}>
          {m.entity.auditHint}
        </div>
        {Object.entries(props).map(([pName, pSpec], index) => {
          const isOpen = expandedProp === pName;
          const isStringLike = pSpec.type === "string" || pSpec.type === "text";
          const isInt = pSpec.type === "int";
          const isEnum = pSpec.type === "enum";
          const isCounter = counters.includes(pName);
          return (
            <div className="prop-row-wrap" key={index}>
              <div className="prop-row">
                <input className="uinput mono" value={pName} onChange={(e) => renameProp(pName, e.target.value)} />
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
                    title={m.common.advancedTitle}
                    onClick={() => setExpandedProp(isOpen ? null : pName)}
                  >
                    ⚙
                  </button>
                  <button className="icon-btn" onClick={() => removeProp(pName)}>{m.common.delete}</button>
                </div>
              </div>
              {isOpen && (
                <div className={`prop-adv ${isEnum ? "enum" : ""}`}>
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
                  ) : isEnum ? (
                    <input
                      className="uinput mono"
                      placeholder={m.entity.enumValuesPlaceholder}
                      title={m.entity.enumValuesTitle}
                      value={(pSpec.values ?? []).join(", ")}
                      onChange={(e) =>
                        updateProp(pName, {
                          values: e.target.value.split(",").map((v) => v.trim()).filter((v, i, all) => v !== "" || i === all.length - 1),
                        })
                      }
                    />
                  ) : (
                    <span />
                  )}
                  {isEnum ? (
                    <select
                      className="uselect"
                      title={m.common.defaultValueTitle}
                      value={pSpec.default ?? ""}
                      onChange={(e) => updateProp(pName, { default: e.target.value === "" ? null : e.target.value })}
                    >
                      <option value="">{m.common.noDefault}</option>
                      {(pSpec.values ?? []).filter((v) => v !== "").map((v) => (
                        <option key={v} value={v}>{v}</option>
                      ))}
                    </select>
                  ) : (
                    <input
                      className="uinput"
                      placeholder={m.common.defaultValue}
                      value={pSpec.default ?? ""}
                      onChange={(e) => updateProp(pName, { default: e.target.value === "" ? null : e.target.value })}
                    />
                  )}
                  {isInt ? (
                    <label title={m.entity.counterTitle}>
                      <input type="checkbox" checked={isCounter} onChange={() => toggleCounter(pName)} />
                      {m.entity.counter}
                    </label>
                  ) : (
                    <span />
                  )}
                </div>
              )}
            </div>
          );
        })}
        {Object.keys(props).length === 0 && <div className="hint">{m.entity.noFields}</div>}
      </div>

      {/* Erişim: sahip alanı + action başına kural (docs/ARCH.md §6.1) */}
      <div className="divider">
        <EntityAccessEditor entity={entity} auth={auth} knownRoles={knownRoles} onChange={onChange} />
      </div>

      {/* Liste filtreleri + okuma görünürlüğü (docs/ARCH.md §6.2) */}
      <div className="divider">
        <ListOptionsEditor entity={entity} knownRoles={knownRoles} onChange={onChange} />
      </div>

      {/* Relations + External refs side by side */}
      <div className="field-row" style={{ gap: 40 }}>
        <div className="field">
          <div className="group-head">
            <span className="group-label">{m.entity.relations}</span>
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
                {m.entity.addRelation}
              </button>
            )}
          </div>
          {Object.entries(relations).map(([rName, rel], index) => (
            <div className="rel-row" key={index}>
              <input className="uinput mono" style={{ width: 90 }} value={rName} onChange={(e) => onChange({ ...entity, relations: renameKey(relations, rName, e.target.value) })} />
              <select
                className="uselect"
                value={rel.kind}
                onChange={(e) =>
                  onChange({
                    ...entity,
                    // one-to-many'de FK karşı tarafta durur — opsiyonel işareti orada geçersiz (SpecValidator hatası).
                    relations: setKey(relations, rName, { ...rel, kind: e.target.value, nullable: e.target.value === "one-to-many" ? false : rel.nullable }),
                  })
                }
              >
                {meta.relationKinds.map((k) => (
                  <option key={k} value={k}>{k}</option>
                ))}
              </select>
              <select className="uselect grow" value={rel.target} onChange={(e) => onChange({ ...entity, relations: setKey(relations, rName, { ...rel, target: e.target.value }) })}>
                <option value="">{m.entity.target}</option>
                {others.map((t) => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
              {rel.kind !== "one-to-many" && (
                <label className="hint" title={m.entity.optionalTitle} style={{ whiteSpace: "nowrap" }}>
                  <input
                    type="checkbox"
                    checked={!!rel.nullable}
                    onChange={(e) => onChange({ ...entity, relations: setKey(relations, rName, { ...rel, nullable: e.target.checked }) })}
                  />{" "}
                  {m.entity.optional}
                </label>
              )}
              <button className="icon-btn" onClick={() => onChange({ ...entity, relations: removeKey(relations, rName) })}>×</button>
            </div>
          ))}
          {others.length === 0 && <div className="hint">{m.entity.relationNeedsOther}</div>}
        </div>

        <div className="field">
          <div className="group-head">
            <span className="group-label">{m.entity.externalRefs}</span>
            <button
              className="btn-link"
              onClick={() =>
                onChange({
                  ...entity,
                  externalRefs: setKey(externalRefs, uniqueKey(externalRefs, "ref"), { target: "", store: "", via: meta.via[0] }),
                })
              }
            >
              {m.entity.addRef}
            </button>
          </div>
          <div className="hint" style={{ marginBottom: 4 }}>{m.entity.externalRefsHint}</div>
          {Object.entries(externalRefs).map(([xName, x], index) => (
            <div className="ext-row" key={index}>
              <input className="uinput mono" style={{ width: 80 }} value={xName} onChange={(e) => onChange({ ...entity, externalRefs: renameKey(externalRefs, xName, e.target.value) })} />
              <input className="uinput grow" placeholder={m.entity.refTarget} value={x.target} onChange={(e) => onChange({ ...entity, externalRefs: setKey(externalRefs, xName, { ...x, target: e.target.value }) })} />
              <input className="uinput mono" style={{ width: 110 }} placeholder={m.entity.refStore} value={x.store} onChange={(e) => onChange({ ...entity, externalRefs: setKey(externalRefs, xName, { ...x, store: e.target.value }) })} />
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
