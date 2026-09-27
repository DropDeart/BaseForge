import { type ReactNode, useState } from "react";
import { BUILT_IN_ROLES, type AuthSpec, type Meta, type ProviderSpec, type ProvidersSpec } from "../types";
import { UserProfileEditor } from "./UserProfileEditor";
import { useT } from "../i18n";

interface Props {
  meta: Meta;
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

/** services/BaseForge.Identity/Program.cs'deki AddIdentity Password politikasıyla aynı kural seti. */
function isPasswordPolicyValid(password: string): boolean {
  return (
    password.length >= 8 &&
    /[A-Z]/.test(password) &&
    /[a-z]/.test(password) &&
    /[0-9]/.test(password) &&
    /[^A-Za-z0-9]/.test(password)
  );
}

export function IdentityPanel({ meta, auth, onChange, children }: Props) {
  const m = useT();
  const [selectedLabel, setSelectedLabel] = useState(meta.providers[0]);
  const providers = auth.providers ?? {};
  const selectedKey = PROVIDER_KEYS[selectedLabel];
  const selected = providers[selectedKey];

  const setProvider = (key: keyof ProvidersSpec, value: ProviderSpec | null) =>
    onChange({ ...auth, providers: { ...providers, [key]: value } });

  const passwordInvalid = !!auth.seedAdmin?.password && !isPasswordPolicyValid(auth.seedAdmin.password);

  return (
    <div className="cols">
      <div className="list-col">
        <div className="list-label">{m.identity.providers}</div>
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
          <div className="group-label">{m.identity.central}</div>
          <div className="field-row">
            <div className="field">
              <span className="field-label">{m.common.serviceName}</span>
              <input className="uinput" value={auth.service} onChange={(e) => onChange({ ...auth, service: e.target.value })} />
            </div>
            <div className="field">
              <span className="field-label">{m.common.database}</span>
              <input className="uinput mono" value={auth.database} onChange={(e) => onChange({ ...auth, database: e.target.value })} />
            </div>
          </div>
          <div className="field" style={{ marginTop: 12 }}>
            <span className="field-label">Issuer</span>
            <input className="uinput mono" placeholder="http://localhost:5090/" value={auth.issuer} onChange={(e) => onChange({ ...auth, issuer: e.target.value })} />
          </div>
          <div className="field-row" style={{ marginTop: 12 }}>
            <div className="field">
              <span className="field-label">{m.common.restPort}</span>
              <input
                className="uinput mono"
                type="number"
                placeholder="8081"
                value={auth.dockerPorts?.rest ?? ""}
                onChange={(e) => onChange({ ...auth, dockerPorts: { ...auth.dockerPorts, rest: e.target.value === "" ? null : Number(e.target.value) } })}
              />
            </div>
            <div className="field">
              <span className="field-label">{m.common.grpcPort}</span>
              <input
                className="uinput mono"
                type="number"
                placeholder="8082"
                value={auth.dockerPorts?.grpc ?? ""}
                onChange={(e) => onChange({ ...auth, dockerPorts: { ...auth.dockerPorts, grpc: e.target.value === "" ? null : Number(e.target.value) } })}
              />
            </div>
            <div className="field">
              <span className="field-label">{m.common.postgresPort}</span>
              <input
                className="uinput mono"
                type="number"
                placeholder="5432"
                value={auth.dockerPorts?.postgres ?? ""}
                onChange={(e) => onChange({ ...auth, dockerPorts: { ...auth.dockerPorts, postgres: e.target.value === "" ? null : Number(e.target.value) } })}
              />
            </div>
          </div>
          <div className="hint" style={{ marginTop: 4 }}>{m.common.portsHint}</div>
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
                <input className="uinput mono" type="password" placeholder={m.identity.storedSecret} value={selected.clientSecret} onChange={(e) => setProvider(selectedKey, { ...selected, clientSecret: e.target.value })} />
              </div>
            </div>
          ) : (
            <div className="hint">{m.identity.enableProvider}</div>
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
            <>
              <div className="field-row">
                <div className="field">
                  <span className="field-label">{m.identity.email}</span>
                  <input className="uinput" placeholder="admin@baseforge.local" value={auth.seedAdmin.email} onChange={(e) => onChange({ ...auth, seedAdmin: { ...auth.seedAdmin!, email: e.target.value } })} />
                </div>
                <div className="field">
                  <span className="field-label">{m.identity.password}</span>
                  <input className="uinput" type="password" placeholder={m.identity.storedSecret} value={auth.seedAdmin.password} onChange={(e) => onChange({ ...auth, seedAdmin: { ...auth.seedAdmin!, password: e.target.value } })} />
                </div>
              </div>
              <div className="hint" style={passwordInvalid ? { color: "var(--red)" } : undefined}>
                {m.identity.passwordPolicy}
              </div>
            </>
          )}
        </div>

        <RolesAndRegistration auth={auth} onChange={onChange} />

        <UserProfileEditor meta={meta} auth={auth} onChange={onChange} />

        {children}
      </div>
    </div>
  );
}

const ROLE_NAME = /^[A-Za-z0-9_-]+$/;

/** Ek roller + kendi kendine kayıt ayarı (bkz. docs/ARCH.md §6.1). */
function RolesAndRegistration({ auth, onChange }: { auth: AuthSpec; onChange: (auth: AuthSpec) => void }) {
  const m = useT();
  const [draft, setDraft] = useState("");
  const roles = auth.roles ?? [];
  const registration = auth.registration ?? { enabled: false, defaultRole: "User" };
  const allRoles = [...new Set<string>([...BUILT_IN_ROLES, ...roles])];
  const draftValid = ROLE_NAME.test(draft) && !allRoles.includes(draft);

  const addRole = () => {
    if (!draftValid) return;
    onChange({ ...auth, roles: [...roles, draft] });
    setDraft("");
  };

  const removeRole = (role: string) =>
    onChange({
      ...auth,
      roles: roles.filter((r) => r !== role),
      // Silinen rol kayıt varsayılanıysa User'a dön (aksi halde AuthSpecValidator hata verir).
      registration: registration.defaultRole === role ? { ...registration, defaultRole: "User" } : registration,
    });

  return (
    <div className="divider">
      <div className="group-label">{m.identity.roles}</div>
      <div className="chips">
        {BUILT_IN_ROLES.map((r) => (
          <span key={r} className="chip on" title={m.identity.alwaysPresent}>{r}</span>
        ))}
        {roles.map((r) => (
          <button key={r} type="button" className="chip on" title={m.identity.removeRole} onClick={() => removeRole(r)}>
            {r}<span className="x">×</span>
          </button>
        ))}
        <input
          className="uinput mono"
          style={{ width: 170 }}
          placeholder={m.identity.newRole}
          value={draft}
          onChange={(e) => setDraft(e.target.value.trim())}
          onKeyDown={(e) => e.key === "Enter" && addRole()}
        />
        <button className="btn-link" disabled={!draftValid} onClick={addRole}>{m.common.add}</button>
      </div>
      <div className="hint" style={{ marginTop: 4 }}>
        {m.identity.rolesHint}
      </div>

      <div className="toggle-row" style={{ marginTop: 16 }}>
        <button
          className={`toggle ${registration.enabled ? "on" : ""}`}
          onClick={() => onChange({ ...auth, registration: { ...registration, enabled: !registration.enabled } })}
        >
          <span className="knob" />
        </button>
        <span className="group-label" style={{ margin: 0 }}>{m.identity.registration}</span>
      </div>
      {registration.enabled ? (
        <div className="field" style={{ marginTop: 8, maxWidth: 240 }}>
          <span className="field-label">{m.identity.registrationRole}</span>
          <select
            className="uselect"
            value={registration.defaultRole}
            onChange={(e) => onChange({ ...auth, registration: { ...registration, defaultRole: e.target.value } })}
          >
            {allRoles.map((r) => (
              <option key={r} value={r}>{r}</option>
            ))}
          </select>
        </div>
      ) : (
        <div className="hint" style={{ marginTop: 4 }}>
          {m.identity.registrationOff}
        </div>
      )}
    </div>
  );
}
