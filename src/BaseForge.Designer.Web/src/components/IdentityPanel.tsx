import type { AuthSpec, ProviderSpec, ProvidersSpec } from "../types";

interface Props {
  meta: { providers: string[] };
  auth: AuthSpec;
  onChange: (auth: AuthSpec) => void;
}

const PROVIDER_KEYS: Record<string, keyof ProvidersSpec> = {
  Google: "google",
  GitHub: "gitHub",
  Microsoft: "microsoft",
  Facebook: "facebook",
};

export function IdentityPanel({ meta, auth, onChange }: Props) {
  const providers = auth.providers ?? {};

  const setProvider = (key: keyof ProvidersSpec, value: ProviderSpec | null) =>
    onChange({ ...auth, providers: { ...providers, [key]: value } });

  return (
    <>
      <div className="card">
        <h3>Merkez Identity</h3>
        <div className="row">
          <label>Servis adı</label>
          <input className="grow" value={auth.service} onChange={(e) => onChange({ ...auth, service: e.target.value })} />
        </div>
        <div className="row">
          <label>Veritabanı</label>
          <input className="grow" value={auth.database} onChange={(e) => onChange({ ...auth, database: e.target.value })} />
        </div>
        <div className="row">
          <label>Issuer</label>
          <input
            className="grow"
            placeholder="http://localhost:5090/"
            value={auth.issuer}
            onChange={(e) => onChange({ ...auth, issuer: e.target.value })}
          />
        </div>
      </div>

      <div className="card">
        <h3>Sosyal Sağlayıcılar</h3>
        <div className="hint" style={{ marginBottom: 12 }}>
          Etkinleştirdiğin sağlayıcı için ClientId / ClientSecret gir. Boş bırakılanlar üretilmez.
        </div>
        {meta.providers.map((label) => {
          const key = PROVIDER_KEYS[label];
          const p = providers[key];
          const enabled = p != null;
          return (
            <div key={label} style={{ marginBottom: 14 }}>
              <div className="provider-toggle">
                <input
                  type="checkbox"
                  checked={enabled}
                  onChange={(e) => setProvider(key, e.target.checked ? { clientId: "", clientSecret: "" } : null)}
                />
                <strong>{label}</strong>
              </div>
              {enabled && (
                <div className="row" style={{ marginLeft: 24 }}>
                  <input
                    className="grow"
                    placeholder="ClientId"
                    value={p!.clientId}
                    onChange={(e) => setProvider(key, { ...p!, clientId: e.target.value })}
                  />
                  <input
                    className="grow"
                    placeholder="ClientSecret"
                    value={p!.clientSecret}
                    onChange={(e) => setProvider(key, { ...p!, clientSecret: e.target.value })}
                  />
                </div>
              )}
            </div>
          );
        })}
      </div>

      <div className="card">
        <h3>Seed Admin</h3>
        <div className="provider-toggle">
          <input
            type="checkbox"
            checked={auth.seedAdmin != null}
            onChange={(e) => onChange({ ...auth, seedAdmin: e.target.checked ? { email: "", password: "" } : null })}
          />
          <span className="hint">İlk açılışta bir admin kullanıcı oluştur</span>
        </div>
        {auth.seedAdmin && (
          <div className="row" style={{ marginLeft: 24 }}>
            <input
              className="grow"
              placeholder="admin@example.com"
              value={auth.seedAdmin.email}
              onChange={(e) => onChange({ ...auth, seedAdmin: { ...auth.seedAdmin!, email: e.target.value } })}
            />
            <input
              className="grow"
              type="password"
              placeholder="parola"
              value={auth.seedAdmin.password}
              onChange={(e) => onChange({ ...auth, seedAdmin: { ...auth.seedAdmin!, password: e.target.value } })}
            />
          </div>
        )}
      </div>
    </>
  );
}
