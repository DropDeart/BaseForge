import { useEffect, useState } from "react";
import { api } from "./api/client";
import { EntityEditor } from "./components/EntityEditor";
import { IdentityPanel } from "./components/IdentityPanel";
import type { AuthSpec, GenerateResponse, Meta, ServiceSpec } from "./types";
import { removeKey, setKey, uniqueKey } from "./util";

type Tab = "service" | "identity";

export function App() {
  const [meta, setMeta] = useState<Meta | null>(null);
  const [spec, setSpec] = useState<ServiceSpec | null>(null);
  const [auth, setAuth] = useState<AuthSpec | null>(null);
  const [tab, setTab] = useState<Tab>("service");
  const [selected, setSelected] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<string[]>([]);
  const [result, setResult] = useState<GenerateResponse | null>(null);

  useEffect(() => {
    Promise.all([api.meta(), api.spec()]).then(([m, s]) => {
      setMeta(m);
      setSpec(s.service);
      setAuth(s.auth);
      const first = Object.keys(s.service.entities ?? {})[0] ?? null;
      setSelected(first);
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
    const out: Record<string, typeof entities[string]> = {};
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
      const res =
        tab === "service" ? await api.generateService(spec) : await api.generateIdentity(auth);
      if ("errors" in res) {
        setErrors(res.errors);
      } else {
        setResult(res);
      }
    } catch (e) {
      setErrors([String(e)]);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="app">
      <div className="topbar">
        <div className="logo">Base<span>Forge</span> Designer</div>
        <div className="tabs">
          <button className={`tab ${tab === "service" ? "active" : ""}`} onClick={() => setTab("service")}>
            Servis: {spec.service}
          </button>
          <button className={`tab ${tab === "identity" ? "active" : ""}`} onClick={() => setTab("identity")}>
            Identity
          </button>
        </div>
        <div className="spacer" />
        <button className="ghost" onClick={() => api.shutdown()}>Kapat</button>
        <button className="primary" disabled={busy} onClick={generate}>
          {busy ? "Üretiliyor…" : tab === "service" ? "Servisi Üret + Derle" : "Identity Üret + Derle"}
        </button>
      </div>

      <div className="body">
        {tab === "service" && (
          <div className="sidebar">
            <p className="section-title">Entity'ler</p>
            {entityNames.map((name) => (
              <div
                key={name}
                className={`entity-item ${selected === name ? "active" : ""}`}
                onClick={() => setSelected(name)}
              >
                <span>{name}</span>
                <span className="count">{Object.keys(entities[name].props ?? {}).length}</span>
              </div>
            ))}
            <button style={{ width: "100%", marginTop: 8 }} onClick={addEntity}>+ Entity ekle</button>
          </div>
        )}

        <div className="main">
          {tab === "service" ? (
            <>
              <div className="card">
                <h3>Servis</h3>
                <div className="row">
                  <label>Servis adı</label>
                  <input className="grow" value={spec.service} onChange={(e) => setSpec({ ...spec, service: e.target.value })} />
                </div>
                <div className="row">
                  <label>Veritabanı</label>
                  <input className="grow" value={spec.database} onChange={(e) => setSpec({ ...spec, database: e.target.value })} />
                </div>
                <div className="provider-toggle" style={{ marginTop: 8 }}>
                  <input
                    type="checkbox"
                    checked={spec.auth != null}
                    onChange={(e) =>
                      setSpec({
                        ...spec,
                        auth: e.target.checked
                          ? { authority: "http://localhost:5090", audience: "baseforge-api", requireHttpsMetadata: false, protect: true }
                          : null,
                      })
                    }
                  />
                  <span className="hint">Merkez Identity'ye JWT ile bağla</span>
                </div>
                {spec.auth && (
                  <div style={{ marginLeft: 24, marginTop: 8 }}>
                    <div className="row">
                      <label>Authority</label>
                      <input className="grow" value={spec.auth.authority} onChange={(e) => setSpec({ ...spec, auth: { ...spec.auth!, authority: e.target.value } })} />
                    </div>
                    <div className="row">
                      <label>Audience</label>
                      <input className="grow" value={spec.auth.audience} onChange={(e) => setSpec({ ...spec, auth: { ...spec.auth!, audience: e.target.value } })} />
                    </div>
                    <div className="provider-toggle">
                      <input type="checkbox" checked={spec.auth.protect} onChange={(e) => setSpec({ ...spec, auth: { ...spec.auth!, protect: e.target.checked } })} />
                      <span className="hint">Tüm controller'lar [Authorize] olsun</span>
                    </div>
                  </div>
                )}
              </div>

              {selected && entities[selected] ? (
                <>
                  <div className="card">
                    <h3>Entity</h3>
                    <div className="row">
                      <label>Ad</label>
                      <input className="grow" value={selected} onChange={(e) => renameEntity(selected, e.target.value)} />
                      <button className="danger" onClick={() => removeEntity(selected)}>Entity'yi sil</button>
                    </div>
                  </div>
                  <EntityEditor
                    name={selected}
                    entity={entities[selected]}
                    meta={meta}
                    allEntities={entityNames}
                    onChange={(en) => setSpec({ ...spec, entities: setKey(entities, selected, en) })}
                  />
                </>
              ) : (
                <div className="empty">Soldan bir entity seçin ya da yeni ekleyin.</div>
              )}
            </>
          ) : (
            <IdentityPanel meta={meta} auth={auth} onChange={setAuth} />
          )}

          {errors.length > 0 && (
            <div className="result fail">
              <span className="badge fail">Hatalı</span>
              <ul className="errors" style={{ marginTop: 8 }}>
                {errors.map((e, i) => (
                  <li key={i}>{e}</li>
                ))}
              </ul>
            </div>
          )}

          {result && (
            <div className={`result ${result.buildSuccess ? "ok" : "fail"}`}>
              <span className={`badge ${result.buildSuccess ? "ok" : "fail"}`}>
                {result.buildSuccess ? "Derleme başarılı" : "Derleme hatası"}
              </span>
              <span className="hint" style={{ marginLeft: 10 }}>
                {result.files.length} dosya → {result.output}
              </span>
              <ul className="files">
                {result.files.map((f) => (
                  <li key={f}>{f}</li>
                ))}
              </ul>
              {!result.buildSuccess && <pre className="build">{result.buildOutput}</pre>}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
