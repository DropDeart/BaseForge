import { type ReactNode, useState } from "react";
import type { AuthSpec, ProviderSpec, ProvidersSpec } from "../types";

interface Props {
  meta: { providers: string[] };
  auth: AuthSpec;
  onChange: (auth: AuthSpec) => void;
  children?: ReactNode;
}

const PROVIDER_KEYS: Record<string, keyof ProvidersSpec> = {
  Google: "google",
  GitHub: "gitHub",
  Microsoft: "microsoft",
  Facebook: "facebook",
};

export function IdentityPanel({ meta, auth, onChange, children }: Props) {
  const [selectedLabel, setSelectedLabel] = useState(meta.providers[0]);
  const providers = auth.providers ?? {};
  const selectedKey = PROVIDER_KEYS[selectedLabel];
  const selected = providers[selectedKey];

  const setProvider = (key: keyof ProvidersSpec, value: ProviderSpec | null) =>
    onChange({ ...auth, providers: { ...providers, [key]: value } });

  return (
    <div className="cols">
      <div className="list-col">
        <div className="list-label">Sağlayıcılar</div>
        {meta.providers.map((label) => {
          const key = PROVIDER_KEYS[label];
          const enabled = providers[key] != null;
          return (
            <div
              key={label}
              className={`list-item ${selectedLabel === label ? "active" : ""}`}
              onClick={() => setSelectedLabel(label)}
            >
              <span>{label}</span>
              <span className={enabled ? "dot-on" : "dot-off"}>{enabled ? "●" : "○"}</span>
            </div>
          );
        })}
      </div>

      <div className="inspector narrow">
        {/* Merkez ayarlar */}
        <div>
          <div className="group-label">Merkez ayarlar</div>
          <div className="field-row">
            <div className="field">
              <span className="field-label">Servis adı</span>
              <input className="uinput" value={auth.service} onChange={(e) => onChange({ ...auth, service: e.target.value })} />
            </div>
            <div className="field">
              <span className="field-label">Veritabanı</span>
              <input className="uinput mono" value={auth.database} onChange={(e) => onChange({ ...auth, database: e.target.value })} />
            </div>
          </div>
          <div className="field" style={{ marginTop: 12 }}>
            <span className="field-label">Issuer</span>
            <input className="uinput mono" placeholder="http://localhost:5090/" value={auth.issuer} onChange={(e) => onChange({ ...auth, issuer: e.target.value })} />
          </div>
          <div className="field-row" style={{ marginTop: 12 }}>
            <div className="field">
              <span className="field-label">REST portu</span>
              <input
                className="uinput mono"
                type="number"
                placeholder="8081"
                value={auth.dockerPorts?.rest ?? ""}
                onChange={(e) => onChange({ ...auth, dockerPorts: { ...auth.dockerPorts, rest: e.target.value === "" ? null : Number(e.target.value) } })}
              />
            </div>
            <div className="field">
              <span className="field-label">gRPC portu</span>
              <input
                className="uinput mono"
                type="number"
                placeholder="8082"
                value={auth.dockerPorts?.grpc ?? ""}
                onChange={(e) => onChange({ ...auth, dockerPorts: { ...auth.dockerPorts, grpc: e.target.value === "" ? null : Number(e.target.value) } })}
              />
            </div>
            <div className="field">
              <span className="field-label">Postgres portu</span>
              <input
                className="uinput mono"
                type="number"
                placeholder="5432"
                value={auth.dockerPorts?.postgres ?? ""}
                onChange={(e) => onChange({ ...auth, dockerPorts: { ...auth.dockerPorts, postgres: e.target.value === "" ? null : Number(e.target.value) } })}
              />
            </div>
          </div>
          <div className="hint" style={{ marginTop: 4 }}>Boş = varsayılan. Başka bir projeyle port çakışıyorsa değiştirin.</div>
        </div>

        {/* Selected provider */}
        <div className="divider">
          <div className="toggle-row" style={{ marginBottom: 12 }}>
            <button
              className={`toggle ${selected ? "on" : ""}`}
              onClick={() => setProvider(selectedKey, selected ? null : { clientId: "", clientSecret: "" })}
            >
              <span className="knob" />
            </button>
            <span className="group-label" style={{ margin: 0 }}>{selectedLabel} — ClientId / Secret</span>
          </div>
          {selected ? (
            <div className="field-row">
              <div className="field">
                <span className="field-label">ClientId</span>
                <input className="uinput mono" value={selected.clientId} onChange={(e) => setProvider(selectedKey, { ...selected, clientId: e.target.value })} />
              </div>
              <div className="field">
                <span className="field-label">ClientSecret</span>
                <input className="uinput mono" type="password" placeholder="kayıtlı — değiştirmek için gir" value={selected.clientSecret} onChange={(e) => setProvider(selectedKey, { ...selected, clientSecret: e.target.value })} />
              </div>
            </div>
          ) : (
            <div className="hint">Bu sağlayıcıyı kullanmak için soldaki toggle'ı aç.</div>
          )}
        </div>

        {/* Seed admin */}
        <div className="divider">
          <div className="toggle-row" style={{ marginBottom: 12 }}>
            <button
              className={`toggle ${auth.seedAdmin ? "on" : ""}`}
              onClick={() => onChange({ ...auth, seedAdmin: auth.seedAdmin ? null : { email: "", password: "" } })}
            >
              <span className="knob" />
            </button>
            <span className="group-label" style={{ margin: 0 }}>Seed Admin</span>
          </div>
          {auth.seedAdmin && (
            <div className="field-row">
              <div className="field">
                <span className="field-label">E-posta</span>
                <input className="uinput" placeholder="admin@baseforge.local" value={auth.seedAdmin.email} onChange={(e) => onChange({ ...auth, seedAdmin: { ...auth.seedAdmin!, email: e.target.value } })} />
              </div>
              <div className="field">
                <span className="field-label">Parola</span>
                <input className="uinput" type="password" placeholder="kayıtlı — değiştirmek için gir" value={auth.seedAdmin.password} onChange={(e) => onChange({ ...auth, seedAdmin: { ...auth.seedAdmin!, password: e.target.value } })} />
              </div>
            </div>
          )}
        </div>

        {children}
      </div>
    </div>
  );
}
