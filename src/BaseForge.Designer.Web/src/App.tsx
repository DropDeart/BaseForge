import { useEffect, useState } from "react";
import { api } from "./api/client";
import { EntityEditor } from "./components/EntityEditor";
import { IdentityPanel } from "./components/IdentityPanel";
import { ErDiagram } from "./components/ErDiagram";
import { UiDesignLauncher } from "./components/UiDesignLauncher";
import type { AuthSpec, DockerPortsSpec, GenerateResponse, Meta, ServiceSpec, WorkspaceEntry } from "./types";
import { removeKey, renameKey, setKey, uniqueKey } from "./util";

type View = "service" | "identity" | "er";

/** Workspace'teki en yüksek kullanılan portun bir fazlası; hiç kayıt yoksa verilen taban. */
function nextPort(workspace: WorkspaceEntry[], key: "restPort" | "grpcPort" | "postgresPort", base: number): number {
  const used = workspace.map((e) => e[key]).filter((p): p is number => typeof p === "number" && p > 0);
  return used.length > 0 ? Math.max(...used) + 1 : base;
}

/** identity'nin varsayılan tabanı (8081/8082) diğer servislerinkinden (8080/8081) farklı — mevcut IdentityGenerator konvansiyonuyla tutarlı. */
function suggestPorts(workspace: WorkspaceEntry[], isIdentity: boolean): DockerPortsSpec {
  return {
    rest: nextPort(workspace, "restPort", isIdentity ? 8081 : 8080),
    grpc: nextPort(workspace, "grpcPort", isIdentity ? 8082 : 8081),
    postgres: nextPort(workspace, "postgresPort", 5432),
  };
}

/** Workspace'te identity zaten üretilmişse Docker-doğru adresi (host.docker.internal:{gerçek REST portu}) önerir. */
function suggestAuthority(workspace: WorkspaceEntry[]): string {
  const identity = workspace.find((e) => e.isIdentity);
  return identity?.restPort ? `http://host.docker.internal:${identity.restPort}` : "http://localhost:5090";
}

function portsEqual(a: DockerPortsSpec, b: DockerPortsSpec): boolean {
  return a.rest === b.rest && a.grpc === b.grpc && a.postgres === b.postgres;
}

export function App() {
  const [meta, setMeta] = useState<Meta | null>(null);
  const [spec, setSpec] = useState<ServiceSpec | null>(null);
  const [auth, setAuth] = useState<AuthSpec | null>(null);
  const [view, setView] = useState<View>("service");
  const [selected, setSelected] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<string[]>([]);
  const [result, setResult] = useState<GenerateResponse | null>(null);
  const [includeInSolution, setIncludeInSolution] = useState(false);
  const [workspace, setWorkspace] = useState<WorkspaceEntry[]>([]);
  const [suggested, setSuggested] = useState<{ ports: DockerPortsSpec; authority: string } | null>(null);

  useEffect(() => {
    Promise.all([api.meta(), api.spec(), api.workspace()]).then(([m, s, ws]) => {
      setMeta(m);
      setWorkspace(ws);
      setIncludeInSolution(m.solutionFound);

      const suggestedPorts = suggestPorts(ws, false);
      const suggestedAuthority = suggestAuthority(ws);
      setSuggested({ ports: suggestedPorts, authority: suggestedAuthority });

      let service = s.service;
      if (m.serviceIsNew && !service.dockerPorts) {
        service = { ...service, dockerPorts: suggestedPorts };
      }

      let authSpec = s.auth;
      if (m.identityIsNew && !authSpec.dockerPorts) {
        authSpec = { ...authSpec, dockerPorts: suggestPorts(ws, true) };
      }

      setSpec(service);
      setAuth(authSpec);
      setSelected(Object.keys(service.entities ?? {})[0] ?? null);
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

    // Anahtarı yeniden adlandır, sonra tüm entity'lerdeki ilişkilerde bu
    // entity'yi hedefleyenleri de güncelle — aksi halde hedef stringi eskide
    // kalır (dangling reference: boş select, ErDiagram'da kopuk bağlantı,
    // geçersiz DBML).
    const renamed = renameKey(entities, oldName, newName);
    const out = Object.fromEntries(
      Object.entries(renamed).map(([k, v]) => {
        if (!v.relations) return [k, v];
        const relations = Object.fromEntries(
          Object.entries(v.relations).map(([rName, rel]) =>
            rel.target === oldName ? [rName, { ...rel, target: newName }] : [rName, rel],
          ),
        );
        return [k, { ...v, relations }];
      }),
    );

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
        view === "identity"
          ? await api.generateIdentity(auth, includeInSolution)
          : await api.generateService(spec, includeInSolution);
      if ("errors" in res) {
        setErrors(res.errors);
      } else {
        setResult(res);

        // Identity ve normal bir servis aynı Designer oturumundan art arda üretilebildiği için,
        // az önce üretilen servisin portu/authority'si diğer sekmenin önerisini etkileyebilir.
        // Workspace kaydını tazeleyip, kullanıcının HÂLÂ ELLE DEĞİŞTİRMEDİĞİ (önceki önerilen
        // değerle aynı kalan) alanları güncelliyoruz — elle girilmiş bir değeri asla ezmiyoruz.
        const ws = await api.workspace();
        setWorkspace(ws);
        const nextSuggested = { ports: suggestPorts(ws, false), authority: suggestAuthority(ws) };

        if (suggested) {
          const patch: Partial<ServiceSpec> = {};
          if (spec.dockerPorts && portsEqual(spec.dockerPorts, suggested.ports)) {
            patch.dockerPorts = nextSuggested.ports;
          }
          if (spec.auth && spec.auth.authority === suggested.authority) {
            patch.auth = { ...spec.auth, authority: nextSuggested.authority };
          }
          if (Object.keys(patch).length > 0) {
            setSpec({ ...spec, ...patch });
          }
        }
        setSuggested(nextSuggested);
      }
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
              <div
                className="toggle-row"
                title={
                  meta.solutionFound
                    ? `Üretilen proje ${meta.solutionName} içine eklenir.`
                    : "Yakında bir .slnx/.sln bulunamadı — servis diskte ayrı bir klasör olarak kalır."
                }
              >
                <button
                  className={`toggle ${includeInSolution ? "on" : ""}`}
                  disabled={!meta.solutionFound}
                  onClick={() => setIncludeInSolution((v) => !v)}
                >
                  <span className="knob" />
                </button>
                <span className="hint">
                  {meta.solutionFound ? `Solution'a ekle (${meta.solutionName})` : "Solution bulunamadı — ayrı kalacak"}
                </span>
              </div>
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
                          : { authority: suggested?.authority ?? "http://localhost:5090", audience: "baseforge-api", requireHttpsMetadata: false, protect: true },
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
                <div className="toggle-row" style={{ marginTop: 14 }}>
                  <button
                    className={`toggle ${spec.multiTenant ? "on" : ""}`}
                    onClick={() => setSpec({ ...spec, multiTenant: !spec.multiTenant })}
                  >
                    <span className="knob" />
                  </button>
                  <span className="hint">Multi-tenancy (tüm entity'lere TenantId + izolasyon)</span>
                </div>
                <div className="field-row" style={{ marginTop: 14 }}>
                  <div className="field">
                    <span className="field-label">REST portu</span>
                    <input
                      className="uinput mono"
                      type="number"
                      placeholder="8080"
                      value={spec.dockerPorts?.rest ?? ""}
                      onChange={(e) => setSpec({ ...spec, dockerPorts: { ...spec.dockerPorts, rest: e.target.value === "" ? null : Number(e.target.value) } })}
                    />
                  </div>
                  <div className="field">
                    <span className="field-label">gRPC portu</span>
                    <input
                      className="uinput mono"
                      type="number"
                      placeholder="8081"
                      value={spec.dockerPorts?.grpc ?? ""}
                      onChange={(e) => setSpec({ ...spec, dockerPorts: { ...spec.dockerPorts, grpc: e.target.value === "" ? null : Number(e.target.value) } })}
                    />
                  </div>
                  <div className="field">
                    <span className="field-label">Postgres portu</span>
                    <input
                      className="uinput mono"
                      type="number"
                      placeholder="5432"
                      value={spec.dockerPorts?.postgres ?? ""}
                      onChange={(e) => setSpec({ ...spec, dockerPorts: { ...spec.dockerPorts, postgres: e.target.value === "" ? null : Number(e.target.value) } })}
                    />
                  </div>
                </div>
                <div className="hint" style={{ marginTop: 4 }}>
                  {workspace.length > 0
                    ? `Bu workspace'te zaten üretilmiş: ${workspace.map((w) => `${w.name} (REST ${w.restPort ?? "?"}, gRPC ${w.grpcPort ?? "?"}, PG ${w.postgresPort ?? "?"})`).join(" · ")}. Portlar bunlarla çakışmayacak şekilde otomatik önerildi, istersen elle değiştir.`
                    : "Boş = varsayılan. Başka bir projeyle port çakışıyorsa değiştirin."}
                </div>
                <div className="field-row" style={{ marginTop: 14 }}>
                  <div className="field">
                    <span className="field-label">Outbox max retry</span>
                    <input
                      className="uinput mono"
                      type="number"
                      placeholder="10"
                      value={spec.rabbitMqTuning?.outboxMaxRetries ?? ""}
                      onChange={(e) => setSpec({ ...spec, rabbitMqTuning: { ...spec.rabbitMqTuning, outboxMaxRetries: e.target.value === "" ? null : Number(e.target.value) } })}
                    />
                  </div>
                  <div className="field">
                    <span className="field-label">Outbox retention (gün)</span>
                    <input
                      className="uinput mono"
                      type="number"
                      placeholder="7"
                      value={spec.rabbitMqTuning?.outboxRetentionDays ?? ""}
                      onChange={(e) => setSpec({ ...spec, rabbitMqTuning: { ...spec.rabbitMqTuning, outboxRetentionDays: e.target.value === "" ? null : Number(e.target.value) } })}
                    />
                  </div>
                </div>
                <div className="hint" style={{ marginTop: 4 }}>
                  Yalnızca <code>publishes</code>/<code>subscribes</code> kullanan servislerde anlamlıdır. Boş = kütüphane varsayılanı (10 deneme / 7 gün).
                </div>
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

              <GenerateResult
                key={result?.output}
                errors={errors}
                result={result}
                restPort={spec.dockerPorts?.rest ?? 8080}
                linkPath="/scalar/v1"
                workspace={workspace}
                defaultService={spec.service}
              />
            </div>
          </div>
        )}

        {view === "identity" && (
          <IdentityPanel meta={meta} auth={auth} onChange={setAuth}>
            <GenerateResult key={result?.output} errors={errors} result={result} restPort={auth.dockerPorts?.rest ?? 8081} linkPath="/Account/Login" />
          </IdentityPanel>
        )}

        {view === "er" && <ErDiagram spec={spec} />}
      </div>
    </div>
  );
}

function GenerateResult({
  errors,
  result,
  restPort,
  linkPath,
  workspace,
  defaultService,
}: {
  errors: string[];
  result: GenerateResponse | null;
  restPort: number;
  linkPath: string;
  workspace?: WorkspaceEntry[];
  defaultService?: string;
}) {
  const [running, setRunning] = useState<"idle" | "starting" | "running" | "stopping">("idle");
  const [runUrl, setRunUrl] = useState<string | null>(null);
  const [runError, setRunError] = useState<string | null>(null);

  if (errors.length === 0 && !result) return null;

  const start = async () => {
    if (!result) return;
    setRunning("starting");
    setRunError(null);
    try {
      const res = await api.run(result.output, restPort);
      if (res.success) {
        setRunUrl(res.url);
        setRunning("running");
      } else {
        setRunError(res.dockerOutput || "Postgres başlatılamadı.");
        setRunning("idle");
      }
    } catch (e) {
      setRunError(String(e));
      setRunning("idle");
    }
  };

  const stop = async () => {
    if (!result) return;
    setRunning("stopping");
    await api.stop(result.output);
    setRunning("idle");
    setRunUrl(null);
  };

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
          {result.solutionMessage && <div className="hint" style={{ marginTop: 4 }}>{result.solutionMessage}</div>}
          {!result.buildSuccess && <pre className="build">{result.buildOutput}</pre>}
          {result.buildSuccess && (
            <div className="run-row">
              {running === "running" && runUrl ? (
                <>
                  <a className="btn" href={`${runUrl}${linkPath}`} target="_blank" rel="noopener noreferrer">{runUrl}{linkPath} ↗</a>
                  <button className="btn" onClick={stop} disabled={running !== "running"}>Durdur</button>
                </>
              ) : (
                <button className="btn btn-primary" onClick={start} disabled={running === "starting" || running === "stopping"}>
                  {running === "starting" ? "Başlatılıyor…" : "Çalıştır"}
                </button>
              )}
              {runError && <div className="hint" style={{ color: "var(--red)" }}>{runError}</div>}
            </div>
          )}
          {result.buildSuccess && workspace && defaultService && (
            <UiDesignLauncher workspace={workspace} defaultService={defaultService} />
          )}
        </>
      )}
    </div>
  );
}
