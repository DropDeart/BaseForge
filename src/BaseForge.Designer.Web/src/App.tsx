import { useEffect, useState } from "react";
import { api } from "./api/client";
import { EntityEditor } from "./components/EntityEditor";
import { IdentityPanel } from "./components/IdentityPanel";
import { ErDiagram } from "./components/ErDiagram";
import type { AuthSpec, GenerateResponse, Meta, ServiceSpec } from "./types";
import { removeKey, setKey, uniqueKey } from "./util";

type View = "service" | "identity" | "er";

export function App() {
  const [meta, setMeta] = useState<Meta | null>(null);
  const [spec, setSpec] = useState<ServiceSpec | null>(null);
  const [auth, setAuth] = useState<AuthSpec | null>(null);
  const [view, setView] = useState<View>("service");
  const [selected, setSelected] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<string[]>([]);
  const [result, setResult] = useState<GenerateResponse | null>(null);

  useEffect(() => {
    Promise.all([api.meta(), api.spec()]).then(([m, s]) => {
      setMeta(m);
      setSpec(s.service);
      setAuth(s.auth);
      setSelected(Object.keys(s.service.entities ?? {})[0] ?? null);
    });
  }, []);

  if (!meta || !spec || !auth) {
    return <div className="empty">Yükleniyor…</div>;
  }

  const entities = spec.entities ?? {};
  const entityNames = Object.keys(entities);

  const addEntity = () => {
    const name = uniqueKey(entities, "Entity");
    setSpec({ ...spec, entities: setKey(entities, name, { props: {} }) });
    setSelected(name);
  };

  const renameEntity = (oldName: string, newName: string) => {
    if (!newName || newName === oldName || newName in entities) return;
    const out: Record<string, (typeof entities)[string]> = {};
    for (const [k, v] of Object.entries(entities)) out[k === oldName ? newName : k] = v;
    setSpec({ ...spec, entities: out });
    if (selected === oldName) setSelected(newName);
  };

  const removeEntity = (name: string) => {
    const next = removeKey(entities, name);
    setSpec({ ...spec, entities: next });
    if (selected === name) setSelected(Object.keys(next)[0] ?? null);
  };

  const generate = async () => {
    setBusy(true);
    setErrors([]);
    setResult(null);
    try {
      const res = view === "identity" ? await api.generateIdentity(auth) : await api.generateService(spec);
      if ("errors" in res) setErrors(res.errors);
      else setResult(res);
    } catch (e) {
      setErrors([String(e)]);
    } finally {
      setBusy(false);
    }
  };

  const headerTitle =
    view === "service"
      ? { name: spec.service, sub: "servis tasarımcısı" }
      : view === "identity"
        ? { name: auth.service, sub: "merkez kimlik doğrulama" }
        : { name: spec.service, sub: "ER diyagramı" };

  const railItem = (id: View, glyph: string, label: string) => (
    <button
      className={`rail-item ${view === id ? "active" : ""}`}
      title={label}
      onClick={() => setView(id)}
    >
      {glyph}
    </button>
  );

  return (
    <div className="app">
      <div className="rail">
        {railItem("service", "S", "Servis")}
        {railItem("identity", "I", "Identity")}
        {railItem("er", "E", "ER diyagramı")}
        <div className="rail-spacer" />
        <button className="rail-item" title="Kapat" onClick={() => api.shutdown()}>
          ⏻
        </button>
      </div>

      <div className="workspace">
        <div className="header">
          <div className="header-title">
            {headerTitle.name} <span className="sub">· {headerTitle.sub}</span>
          </div>
          <div className="header-spacer" />
          {view === "er" ? null : (
            <>
              <button className="btn" onClick={() => api.shutdown()}>Kapat</button>
              <button className="btn btn-primary" disabled={busy} onClick={generate}>
                {busy ? "Üretiliyor…" : view === "identity" ? "Identity Üret + Derle" : "Üret + Derle"}
              </button>
            </>
          )}
        </div>

        {view === "service" && (
          <div className="cols">
            <div className="list-col">
              <div className="list-label">Entity'ler</div>
              {entityNames.map((name) => (
                <div
                  key={name}
                  className={`list-item ${selected === name ? "active" : ""}`}
                  onClick={() => setSelected(name)}
                >
                  <span>{name}</span>
                  <span className="count">{Object.keys(entities[name].props ?? {}).length}</span>
                </div>
              ))}
              <button className="btn-link" style={{ marginTop: 8 }} onClick={addEntity}>+ ekle</button>
            </div>

            <div className="inspector">
              <div>
                <div className="group-label">Servis ayarları</div>
                <div className="field-row">
                  <div className="field">
                    <span className="field-label">Servis adı</span>
                    <input className="uinput" value={spec.service} onChange={(e) => setSpec({ ...spec, service: e.target.value })} />
                  </div>
                  <div className="field">
                    <span className="field-label">Veritabanı</span>
                    <input className="uinput mono" value={spec.database} onChange={(e) => setSpec({ ...spec, database: e.target.value })} />
                  </div>
                </div>
                <div className="toggle-row" style={{ marginTop: 14 }}>
                  <button
                    className={`toggle ${spec.auth ? "on" : ""}`}
                    onClick={() =>
                      setSpec({
                        ...spec,
                        auth: spec.auth
                          ? null
                          : { authority: "http://localhost:5090", audience: "baseforge-api", requireHttpsMetadata: false, protect: true },
                      })
                    }
                  >
                    <span className="knob" />
                  </button>
                  <span className="hint">Merkez Identity'ye JWT ile bağla</span>
                </div>
                {spec.auth && (
                  <div className="field-row" style={{ marginTop: 12 }}>
                    <div className="field">
                      <span className="field-label">Authority</span>
                      <input className="uinput mono" value={spec.auth.authority} onChange={(e) => setSpec({ ...spec, auth: { ...spec.auth!, authority: e.target.value } })} />
                    </div>
                    <div className="field">
                      <span className="field-label">Audience</span>
                      <input className="uinput mono" value={spec.auth.audience} onChange={(e) => setSpec({ ...spec, auth: { ...spec.auth!, audience: e.target.value } })} />
                    </div>
                    <div className="field">
                      <span className="field-label">Koruma</span>
                      <div className="toggle-row" style={{ paddingTop: 6 }}>
                        <button className={`toggle ${spec.auth.protect ? "on" : ""}`} onClick={() => setSpec({ ...spec, auth: { ...spec.auth!, protect: !spec.auth!.protect } })}>
                          <span className="knob" />
                        </button>
                        <span className="hint">[Authorize]</span>
                      </div>
                    </div>
                  </div>
                )}
              </div>

              {selected && entities[selected] ? (
                <EntityEditor
                  name={selected}
                  entity={entities[selected]}
                  meta={meta}
                  allEntities={entityNames}
                  onRename={(n) => renameEntity(selected, n)}
                  onRemove={() => removeEntity(selected)}
                  onChange={(en) => setSpec({ ...spec, entities: setKey(entities, selected, en) })}
                />
              ) : (
                <div className="empty">Soldan bir entity seçin ya da yeni ekleyin.</div>
              )}

              <GenerateResult errors={errors} result={result} />
            </div>
          </div>
        )}

        {view === "identity" && (
          <IdentityPanel meta={meta} auth={auth} onChange={setAuth}>
            <GenerateResult errors={errors} result={result} />
          </IdentityPanel>
        )}

        {view === "er" && <ErDiagram spec={spec} />}
      </div>
    </div>
  );
}

function GenerateResult({ errors, result }: { errors: string[]; result: GenerateResponse | null }) {
  if (errors.length === 0 && !result) return null;
  return (
    <div className="result">
      {errors.length > 0 && (
        <>
          <span className="badge fail">Hatalı</span>
          <ul className="errors">
            {errors.map((e, i) => (
              <li key={i}>{e}</li>
            ))}
          </ul>
        </>
      )}
      {result && (
        <>
          <span className={`badge ${result.buildSuccess ? "ok" : "fail"}`}>
            {result.buildSuccess ? "derleme başarılı" : "derleme hatası"}
          </span>
          <span className="result-meta">{result.files.length} dosya → {result.output}</span>
          <div className="files">{result.files.map((f) => f.split(/[\\/]/).slice(-2).join("/")).join(" · ")}</div>
          {!result.buildSuccess && <pre className="build">{result.buildOutput}</pre>}
        </>
      )}
    </div>
  );
}
