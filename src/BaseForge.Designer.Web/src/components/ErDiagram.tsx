import { useState } from "react";
import type { ServiceSpec } from "../types";
import { toDbml, fkName } from "../dbml";

interface Props {
  spec: ServiceSpec;
}

export function ErDiagram({ spec }: Props) {
  const [copied, setCopied] = useState(false);
  const entities = spec.entities ?? {};
  const names = Object.keys(entities);

  // Her entity için ilişkilerden gelen FK kolonları.
  const fkCols: Record<string, string[]> = {};
  const addFk = (t: string, c: string) => (fkCols[t] ??= []).push(c);
  for (const [name, e] of Object.entries(entities)) {
    for (const rel of Object.values(e.relations ?? {})) {
      if (!rel.target) continue;
      if (rel.kind === "many-to-one" || rel.kind === "one-to-one") addFk(name, fkName(rel.target));
      else if (rel.kind === "one-to-many") addFk(rel.target, fkName(name));
    }
  }

  // İlişki listesi (connector etiketleri için).
  const relations = Object.entries(entities).flatMap(([name, e]) =>
    Object.entries(e.relations ?? {})
      .filter(([, r]) => r.target)
      .map(([rn, r]) => ({ from: name, name: rn, kind: r.kind, to: r.target })),
  );

  // Dış referanslar (kesikli kutular).
  const externals = Object.entries(entities).flatMap(([name, e]) =>
    Object.entries(e.externalRefs ?? {}).map(([xn, x]) => ({ owner: name, name: xn, target: x.target, store: x.store, via: x.via })),
  );

  const copyDbml = async () => {
    await navigator.clipboard.writeText(toDbml(spec));
    setCopied(true);
    setTimeout(() => setCopied(false), 1800);
  };

  const openDbdiagram = async () => {
    await navigator.clipboard.writeText(toDbml(spec));
    window.open("https://dbdiagram.io/d", "_blank", "noopener");
  };

  return (
    <>
      <div className="header" style={{ borderTop: "none" }}>
        <div className="hint">
          Spec'ten canlı üretilir · solid = servis içi FK ilişkisi · kesikli = dış servis referansı (FK yok, sadece ID)
        </div>
        <div className="header-spacer" />
        <button className="btn mono" onClick={copyDbml}>{copied ? "kopyalandı ✓" : "DBML kopyala"}</button>
        <button className="btn btn-primary" onClick={openDbdiagram}>dbdiagram.io'da aç</button>
      </div>

      <div className="er-canvas">
        {names.length === 0 ? (
          <div className="empty">Henüz entity yok — Servis sekmesinden ekleyin.</div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 28 }}>
            {/* Entity kutuları */}
            <div style={{ display: "flex", gap: 40, flexWrap: "wrap" }}>
              {names.map((name) => (
                <div className="er-box" key={name}>
                  <div className="er-head">{name}</div>
                  <div className="er-fields">
                    <div className="pk">id · uuid</div>
                    {Object.entries(entities[name].props ?? {}).map(([p, prop]) => (
                      <div key={p}>
                        {p} · {prop.type}
                        {prop.maxLength ? `(${prop.maxLength})` : ""}
                        {prop.nullable ? "?" : ""}
                      </div>
                    ))}
                    {(fkCols[name] ?? []).map((c) => (
                      <div className="fk" key={c}>{c} · uuid (FK)</div>
                    ))}
                    {Object.values(entities[name].externalRefs ?? {}).map((x, i) => (
                      <div className="fk" key={i}>{x.store} · uuid (ext)</div>
                    ))}
                  </div>
                </div>
              ))}
            </div>

            {/* İlişkiler */}
            {relations.length > 0 && (
              <div>
                <div className="group-label" style={{ marginBottom: 10 }}>İlişkiler</div>
                <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                  {relations.map((r, i) => (
                    <div className="er-cluster" key={i} style={{ gap: 10 }}>
                      <span className="mono" style={{ fontSize: 12.5, minWidth: 140 }}>{r.from}</span>
                      <span className="er-conn" style={{ width: 120 }}>
                        <span className="card-label">{kindLabel(r.kind)}</span>
                        <span className="line" />
                        <span className="name">{r.name}</span>
                      </span>
                      <span className="mono" style={{ fontSize: 12.5 }}>{r.to}</span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Dış referanslar */}
            {externals.length > 0 && (
              <div>
                <div className="group-label" style={{ marginBottom: 10 }}>Dış referanslar</div>
                <div className="er-col">
                  {externals.map((x, i) => (
                    <div className="er-cluster" key={i}>
                      <span className="er-conn ext" style={{ width: 80 }}>
                        <span className="line" />
                        <span className="card-label">{x.via}</span>
                      </span>
                      <div className="er-box ext" style={{ width: 220 }}>
                        <div className="er-head">{x.name}</div>
                        <div className="er-sub">{x.target} · store: {x.store}</div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </>
  );
}

function kindLabel(kind: string): string {
  switch (kind) {
    case "one-to-many": return "1 — N";
    case "many-to-one": return "N — 1";
    case "one-to-one": return "1 — 1";
    default: return kind;
  }
}
