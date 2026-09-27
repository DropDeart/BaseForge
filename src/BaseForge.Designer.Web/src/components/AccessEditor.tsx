import type { AccessRule, EntitySpec, ServiceAuthSpec } from "../types";
import { useT, type Messages } from "../i18n";

// Yetkilendirme modeli arayüzü — kurallar ve gerekçeler: docs/ARCH.md §6.1.

const ACTIONS = [
  { key: "list", label: "list" },
  { key: "getById", label: "getById" },
  { key: "create", label: "create" },
  { key: "update", label: "update" },
  { key: "delete", label: "delete" },
] as const;

type Mode = "default" | "anonymous" | "authenticated" | "roles";

const OWNER = "owner";

function modeOf(rule: AccessRule | undefined): Mode {
  if (rule === undefined) return "default";
  if (rule === "anonymous") return "anonymous";
  if (rule === "authenticated") return "authenticated";
  return "roles";
}

function rolesOf(rule: AccessRule | undefined | null): string[] {
  return Array.isArray(rule) ? rule : [];
}

/** Bir kuralın kısa, okunur özeti (örn. "Admin, owner"). */
export function describeRule(rule: AccessRule | undefined | null, m: Messages): string {
  if (!rule) return m.access.everyoneSignedIn;
  if (typeof rule === "string") return rule === "anonymous" ? m.access.everyone : m.access.everyoneSignedIn;
  if (rule.length === 0) return m.access.noRoleSelected;
  return rule.map((r) => (r === OWNER ? m.access.owner : r)).join(", ");
}

/** Seçilebilir rol etiketleri. `owner` true ise sona "sahibi" etiketi eklenir. */
export function RoleChips({
  roles,
  selected,
  onChange,
  owner,
}: {
  roles: string[];
  selected: string[];
  onChange: (next: string[]) => void;
  owner?: { enabled: boolean; title: string };
}) {
  const m = useT();
  const toggle = (value: string) =>
    onChange(selected.includes(value) ? selected.filter((v) => v !== value) : [...selected, value]);

  return (
    <div className="chips">
      {roles.map((r) => (
        <button key={r} type="button" className={`chip ${selected.includes(r) ? "on" : ""}`} onClick={() => toggle(r)}>
          {r}
        </button>
      ))}
      {owner && (
        <button
          type="button"
          className={`chip owner ${selected.includes(OWNER) ? "on" : ""}`}
          disabled={!owner.enabled && !selected.includes(OWNER)}
          title={owner.title}
          onClick={() => toggle(OWNER)}
        >
          {m.access.owner}
        </button>
      )}
    </div>
  );
}

/** Servis seviyesi: varsayılan erişim + süper roller. */
export function ServiceAccessEditor({
  auth,
  knownRoles,
  onChange,
}: {
  auth: ServiceAuthSpec;
  knownRoles: string[];
  onChange: (auth: ServiceAuthSpec) => void;
}) {
  const m = useT();
  const defaultIsRoles = Array.isArray(auth.defaultAccess);
  const superRoles = auth.superRoles ?? [];

  return (
    <div className="field-row" style={{ marginTop: 12, alignItems: "flex-start" }}>
      <div className="field">
        <span className="field-label">{m.access.defaultAccess}</span>
        <select
          className="uselect"
          disabled={!auth.protect}
          value={!auth.protect ? "anonymous" : defaultIsRoles ? "roles" : "authenticated"}
          onChange={(e) =>
            onChange({ ...auth, defaultAccess: e.target.value === "roles" ? ["Admin"] : null })
          }
        >
          {!auth.protect && <option value="anonymous">{m.access.everyoneProtectionOff}</option>}
          <option value="authenticated">{m.access.signedIn}</option>
          <option value="roles">{m.access.specificRoles}</option>
        </select>
        {auth.protect && defaultIsRoles && (
          <div style={{ marginTop: 8 }}>
            <RoleChips
              roles={knownRoles}
              selected={rolesOf(auth.defaultAccess)}
              onChange={(next) => onChange({ ...auth, defaultAccess: next })}
            />
          </div>
        )}
        <div className="hint" style={{ marginTop: 4 }}>
          {m.access.defaultAccessHint}
        </div>
      </div>
      <div className="field">
        <span className="field-label">{m.access.superRoles}</span>
        <RoleChips
          roles={knownRoles.filter((r) => r !== "User")}
          selected={superRoles}
          onChange={(next) => onChange({ ...auth, superRoles: next })}
        />
        <div className="hint" style={{ marginTop: 4 }}>
          {m.access.superRolesHint}
        </div>
      </div>
    </div>
  );
}

/** Entity seviyesi: sahip alanı + action başına erişim tablosu. */
export function EntityAccessEditor({
  entity,
  auth,
  knownRoles,
  onChange,
}: {
  entity: EntitySpec;
  auth: ServiceAuthSpec | null | undefined;
  knownRoles: string[];
  onChange: (entity: EntitySpec) => void;
}) {
  const m = useT();
  if (!auth) {
    return (
      <div>
        <div className="group-label">{m.access.title}</div>
        <div className="hint">{m.access.jwtOff}</div>
      </div>
    );
  }

  // Eski 'anonymousActions' biçimini tabloda göster; ilk düzenlemede 'access'e taşınır (ikisi birlikte geçersiz).
  const legacy = Object.fromEntries((entity.anonymousActions ?? []).map((a) => [a, "anonymous" as AccessRule]));
  const access: Record<string, AccessRule> = { ...legacy, ...entity.access };

  const guidProps = Object.entries(entity.props ?? {})
    .filter(([, p]) => (p.type === "guid" || p.type === "uuid") && !p.nullable)
    .map(([name]) => name);
  const ownerField = entity.ownerField ?? "";
  const defaultLabel = auth.protect ? describeRule(auth.defaultAccess, m) : m.access.everyone;

  const commit = (nextAccess: Record<string, AccessRule>, patch: Partial<EntitySpec> = {}) =>
    onChange({ ...entity, anonymousActions: undefined, access: nextAccess, ...patch });

  const setRule = (action: string, rule: AccessRule | undefined) => {
    const next = { ...access };
    if (rule === undefined) delete next[action];
    else next[action] = rule;
    commit(next);
  };

  const changeOwnerField = (value: string) => {
    // Sahip alanı kaldırılırsa 'owner' içeren kurallar geçersiz kalır — onları da temizle.
    const next = value
      ? access
      : Object.fromEntries(
          Object.entries(access).map(([k, r]) => [k, Array.isArray(r) ? r.filter((v) => v !== OWNER) : r]),
        );
    commit(next, {
      ownerField: value || null,
      // Görünürlük filtresindeki "sahibi" istisnası da sahip alanı olmadan geçersiz.
      readFilter: !value && entity.readFilter ? { ...entity.readFilter, bypassOwner: false } : entity.readFilter,
    });
  };

  const actions = ACTIONS.filter((a) => !entity.appendOnly || (a.key !== "update" && a.key !== "delete"));

  return (
    <div>
      <div className="group-label">{m.access.title}</div>
      <div className="field-row" style={{ marginBottom: 8 }}>
        <div className="field">
          <span className="field-label">{m.access.ownerField}</span>
          <select className="uselect" value={ownerField} onChange={(e) => changeOwnerField(e.target.value)}>
            <option value="">{m.access.none}</option>
            {guidProps.map((p) => (
              <option key={p} value={p}>{p}</option>
            ))}
          </select>
          <div className="hint" style={{ marginTop: 4 }}>
            {guidProps.length === 0
              ? m.access.ownerFieldNeedGuid
              : m.access.ownerFieldHint}
          </div>
        </div>
      </div>
      {actions.map(({ key, label }) => {
        const rule = access[key];
        const mode = modeOf(rule);
        const ownerAllowed = key !== "create";
        return (
          <div className="access-row" key={key}>
            <span className="access-action">{label}</span>
            <select
              className="uselect"
              value={mode}
              onChange={(e) => {
                const m = e.target.value as Mode;
                setRule(key, m === "default" ? undefined : m === "roles" ? ["Admin"] : m);
              }}
            >
              <option value="default">{m.access.defaultOption(defaultLabel)}</option>
              <option value="anonymous">{m.access.anyone}</option>
              <option value="authenticated">{m.access.signedIn}</option>
              <option value="roles">{m.access.rolesOrOwner}</option>
            </select>
            {mode === "roles" ? (
              <RoleChips
                roles={knownRoles}
                selected={rolesOf(rule)}
                onChange={(next) => setRule(key, next)}
                owner={
                  ownerAllowed
                    ? {
                        enabled: !!ownerField,
                        title: ownerField ? m.access.ownerChipTitle(ownerField) : m.access.pickOwnerFirst,
                      }
                    : undefined
                }
              />
            ) : (
              <span className="hint">
                {key === "create" && ownerField && (mode === "anonymous" || (mode === "default" && !auth.protect))
                  ? m.access.createNeedsAuth
                  : ""}
              </span>
            )}
          </div>
        );
      })}
      {(entity.counters ?? []).length > 0 && (
        <div className="hint" style={{ marginTop: 6 }}>{m.access.countersPublic}</div>
      )}
    </div>
  );
}
