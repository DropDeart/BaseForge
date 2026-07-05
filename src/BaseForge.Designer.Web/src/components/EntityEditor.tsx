import type { EntitySpec, Meta } from "../types";
import { removeKey, renameKey, setKey, uniqueKey } from "../util";

interface Props {
  name: string;
  entity: EntitySpec;
  meta: Meta;
  allEntities: string[];
  onChange: (entity: EntitySpec) => void;
}

export function EntityEditor({ name, entity, meta, allEntities, onChange }: Props) {
  const props = entity.props ?? {};
  const relations = entity.relations ?? {};
  const externalRefs = entity.externalRefs ?? {};

  const otherEntities = allEntities.filter((e) => e !== name);

  return (
    <>
      {/* Properties */}
      <div className="card">
        <h3>Alanlar (Properties)</h3>
        <div className="hint" style={{ marginBottom: 10 }}>
          Id, CreatedAt, UpdatedAt gibi audit alanları BaseEntity'den gelir — burada eklemeyin.
        </div>
        {Object.keys(props).length > 0 && (
          <div className="grid-head">
            <div>Ad</div>
            <div>Tip</div>
            <div />
          </div>
        )}
        {Object.entries(props).map(([pName, pType]) => (
          <div className="grid-row" key={pName}>
            <input
              value={pName}
              onChange={(e) => onChange({ ...entity, props: renameKey(props, pName, e.target.value) })}
            />
            <select value={pType} onChange={(e) => onChange({ ...entity, props: setKey(props, pName, e.target.value) })}>
              {meta.types.map((t) => (
                <option key={t} value={t}>{t}</option>
              ))}
            </select>
            <button className="danger ghost" onClick={() => onChange({ ...entity, props: removeKey(props, pName) })}>
              Sil
            </button>
          </div>
        ))}
        <button
          onClick={() =>
            onChange({ ...entity, props: setKey(props, uniqueKey(props, "field"), meta.types[0]) })
          }
        >
          + Alan ekle
        </button>
      </div>

      {/* Relations */}
      <div className="card">
        <h3>İlişkiler (aynı servis içi)</h3>
        {Object.keys(relations).length > 0 && (
          <div className="grid-head">
            <div>Navigasyon adı</div>
            <div>Tür / Hedef</div>
            <div />
          </div>
        )}
        {Object.entries(relations).map(([rName, rel]) => (
          <div className="grid-row" key={rName}>
            <input
              value={rName}
              onChange={(e) => onChange({ ...entity, relations: renameKey(relations, rName, e.target.value) })}
            />
            <div className="row" style={{ margin: 0 }}>
              <select
                value={rel.kind}
                onChange={(e) => onChange({ ...entity, relations: setKey(relations, rName, { ...rel, kind: e.target.value }) })}
              >
                {meta.relationKinds.map((k) => (
                  <option key={k} value={k}>{k}</option>
                ))}
              </select>
              <select
                value={rel.target}
                onChange={(e) => onChange({ ...entity, relations: setKey(relations, rName, { ...rel, target: e.target.value }) })}
              >
                <option value="">— hedef —</option>
                {otherEntities.map((t) => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
            </div>
            <button className="danger ghost" onClick={() => onChange({ ...entity, relations: removeKey(relations, rName) })}>
              Sil
            </button>
          </div>
        ))}
        {otherEntities.length === 0 ? (
          <div className="hint">İlişki için en az bir başka entity gerekir.</div>
        ) : (
          <button
            onClick={() =>
              onChange({
                ...entity,
                relations: setKey(relations, uniqueKey(relations, "related"), {
                  kind: meta.relationKinds[0],
                  target: otherEntities[0],
                }),
              })
            }
          >
            + İlişki ekle
          </button>
        )}
      </div>

      {/* External refs */}
      <div className="card">
        <h3>Dış Referanslar (başka servis)</h3>
        <div className="hint" style={{ marginBottom: 10 }}>
          FK/navigation üretilmez; yalnızca ID tutulur. Veriye grpc (senkron) veya event (asenkron) ile erişilir.
        </div>
        {Object.entries(externalRefs).map(([xName, x]) => (
          <div className="grid-row" key={xName} style={{ gridTemplateColumns: "1fr 1fr 1fr auto" }}>
            <input
              placeholder="ad"
              value={xName}
              onChange={(e) => onChange({ ...entity, externalRefs: renameKey(externalRefs, xName, e.target.value) })}
            />
            <input
              placeholder="hedef: servis/Entity"
              value={x.target}
              onChange={(e) => onChange({ ...entity, externalRefs: setKey(externalRefs, xName, { ...x, target: e.target.value }) })}
            />
            <input
              placeholder="store: CustomerId"
              value={x.store}
              onChange={(e) => onChange({ ...entity, externalRefs: setKey(externalRefs, xName, { ...x, store: e.target.value }) })}
            />
            <div className="row" style={{ margin: 0 }}>
              <select
                value={x.via}
                onChange={(e) => onChange({ ...entity, externalRefs: setKey(externalRefs, xName, { ...x, via: e.target.value }) })}
              >
                {meta.via.map((v) => (
                  <option key={v} value={v}>{v}</option>
                ))}
              </select>
              <button className="danger ghost" onClick={() => onChange({ ...entity, externalRefs: removeKey(externalRefs, xName) })}>
                Sil
              </button>
            </div>
          </div>
        ))}
        <button
          onClick={() =>
            onChange({
              ...entity,
              externalRefs: setKey(externalRefs, uniqueKey(externalRefs, "ref"), {
                target: "customers/Customer",
                store: "CustomerId",
                via: meta.via[0],
              }),
            })
          }
        >
          + Dış referans ekle
        </button>
      </div>
    </>
  );
}
