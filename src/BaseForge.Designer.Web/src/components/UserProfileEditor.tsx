import { useState } from "react";
import type { AuthSpec, Meta, PropSpec } from "../types";
import { removeKey, renameKey, setKey, typeClass, uniqueKey } from "../util";

/** auth.yaml userProfile — kullanıcıya eklenen domain alanları (docs/ARCH.md §6.3). */
export function UserProfileEditor({ meta, auth, onChange }: { meta: Meta; auth: AuthSpec; onChange: (auth: AuthSpec) => void }) {
  const props = auth.userProfile?.props ?? {};
  const [expanded, setExpanded] = useState<string | null>(null);
  // json profil alanlarında desteklenmiyor (AuthSpecValidator).
  const types = meta.types.filter((t) => t !== "json");

  const setProps = (next: Record<string, PropSpec>) =>
    onChange({ ...auth, userProfile: Object.keys(next).length === 0 ? null : { props: next } });

  const update = (name: string, patch: Partial<PropSpec>) =>
    setProps(
      setKey(props, name, {
        ...props[name],
        ...patch,
        // 'values' yalnızca enum'da geçerli.
        ...(patch.type && patch.type !== "enum" ? { values: undefined } : {}),
      }),
    );

  return (
    <div className="divider">
      <div className="group-head">
        <span className="group-label">Profil alanları</span>
        <button className="btn-link" onClick={() => setProps(setKey(props, uniqueKey(props, "Field"), { type: "string" }))}>
          + alan
        </button>
      </div>
      <div className="hint" style={{ marginBottom: 8 }}>
        Kullanıcıya (ApplicationUser) eklenir: profil sayfası, admin paneli, /api/account/me ve identity/User gRPC'si. FullName/Email gibi mevcut alanlar tekrar yazılmaz.
      </div>
      {Object.entries(props).map(([name, spec], index) => {
        const isOpen = expanded === name;
        const isStringLike = spec.type === "string" || spec.type === "text";
        const isEnum = spec.type === "enum";
        return (
          <div className="prop-row-wrap" key={index}>
            <div className="prop-row">
              <input className="uinput mono" value={name} onChange={(e) => setProps(renameKey(props, name, e.target.value))} />
              <select className={`uselect type-select ${typeClass(spec.type)}`} value={spec.type} onChange={(e) => update(name, { type: e.target.value })}>
                {types.map((t) => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
              <div className="prop-row-actions">
                <button className="icon-btn" title="Gelişmiş ayarlar (nullable / maxLength / default / düzenleyen / token)" onClick={() => setExpanded(isOpen ? null : name)}>
                  ⚙
                </button>
                <button className="icon-btn" onClick={() => setProps(removeKey(props, name))}>Sil</button>
              </div>
            </div>
            {isOpen && (
              <>
                <div className={`prop-adv ${isEnum ? "enum" : ""}`}>
                  <label>
                    <input type="checkbox" checked={!!spec.nullable} onChange={(e) => update(name, { nullable: e.target.checked })} />
                    nullable
                  </label>
                  {isStringLike ? (
                    <input
                      className="uinput"
                      type="number"
                      min={1}
                      placeholder="maxLength"
                      value={spec.maxLength ?? ""}
                      onChange={(e) => update(name, { maxLength: e.target.value === "" ? null : Number(e.target.value) })}
                    />
                  ) : isEnum ? (
                    <input
                      className="uinput mono"
                      placeholder="değerler: Pending, Approved"
                      value={(spec.values ?? []).join(", ")}
                      onChange={(e) =>
                        update(name, { values: e.target.value.split(",").map((v) => v.trim()).filter((v, i, all) => v !== "" || i === all.length - 1) })
                      }
                    />
                  ) : (
                    <span />
                  )}
                  {isEnum ? (
                    <select className="uselect" title="Varsayılan değer" value={spec.default ?? ""} onChange={(e) => update(name, { default: e.target.value === "" ? null : e.target.value })}>
                      <option value="">— varsayılan yok —</option>
                      {(spec.values ?? []).filter((v) => v !== "").map((v) => (
                        <option key={v} value={v}>{v}</option>
                      ))}
                    </select>
                  ) : (
                    <input className="uinput" placeholder="default değer" value={spec.default ?? ""} onChange={(e) => update(name, { default: e.target.value === "" ? null : e.target.value })} />
                  )}
                </div>
                <div className="prop-adv">
                  <label title="self: kullanıcı kendi profilinden düzenler. admin: yalnızca admin panelinden (örn. doğrulama durumu).">
                    düzenleyen
                  </label>
                  <select
                    className="uselect"
                    value={spec.editableBy === "admin" ? "admin" : "self"}
                    onChange={(e) => update(name, { editableBy: e.target.value === "admin" ? "admin" : null })}
                  >
                    <option value="self">kullanıcı</option>
                    <option value="admin">yalnızca admin</option>
                  </select>
                  <label title="Alan JWT claim'i olur (camelCase adıyla). Değer token yenilenene kadar eski kalır.">
                    <input type="checkbox" checked={!!spec.inToken} onChange={(e) => update(name, { inToken: e.target.checked })} />
                    token'a ekle
                  </label>
                </div>
              </>
            )}
          </div>
        );
      })}
      {Object.keys(props).length === 0 && <div className="hint">Profil alanı yok.</div>}
    </div>
  );
}
