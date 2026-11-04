import type { ReactNode } from "react";
import {
  BUILT_IN_ROLES,
  type AuthSpec,
  type MessagingSpec,
  type MultiTenancySpec,
  type OtpSpec,
  type PasswordResetSpec,
} from "../types";
import { useT } from "../i18n";

const DEFAULT_TENANCY: MultiTenancySpec = { enabled: false, adminRole: "TenantAdmin", memberRoles: [], requireApproval: true, approverRoles: ["SuperAdmin"] };
const DEFAULT_OTP: OtpSpec = { enabled: false, codeLength: 6, lifetimeSeconds: 180, maxAttempts: 5, resendCooldownSeconds: 60, maxPerHour: 5, defaultCountryCode: "+90" };
const DEFAULT_MESSAGING: MessagingSpec = {
  emailProvider: "Log",
  smsProvider: "Log",
  smtp: { host: "", port: 587, username: "", fromAddress: "", fromName: "BaseForge" },
};
const DEFAULT_RESET: PasswordResetSpec = { enabled: false, resetUrl: "", tokenLifetimeHours: 2, cooldownSeconds: 60 };

function Toggle({ on, onClick, label }: { on: boolean; onClick: () => void; label: string }) {
  return (
    <div className="toggle-row" style={{ marginTop: 16 }}>
      <button className={`toggle ${on ? "on" : ""}`} onClick={onClick}>
        <span className="knob" />
      </button>
      <span className="group-label" style={{ margin: 0 }}>{label}</span>
    </div>
  );
}

/** Rol listesinden çoklu seçim (chip'ler). */
function RoleChips({ roles, selected, onChange }: { roles: string[]; selected: string[]; onChange: (next: string[]) => void }) {
  return (
    <div className="chips">
      {roles.map((r) => (
        <button
          key={r}
          type="button"
          className={`chip ${selected.includes(r) ? "on" : ""}`}
          onClick={() => onChange(selected.includes(r) ? selected.filter((x) => x !== r) : [...selected, r])}
        >
          {r}
        </button>
      ))}
    </div>
  );
}

function Field({ label, children, width }: { label: string; children: ReactNode; width?: number }) {
  return (
    <div className="field" style={width ? { maxWidth: width } : undefined}>
      <span className="field-label">{label}</span>
      {children}
    </div>
  );
}

/** auth.yaml 0.6.4 bölümleri: kayıtta seçilebilen roller, şirketler, OTP, e-posta, şifre sıfırlama, token süresi. */
export function IdentityFeaturesEditor({ auth, onChange }: { auth: AuthSpec; onChange: (auth: AuthSpec) => void }) {
  const m = useT().identityFeatures;
  const customRoles = auth.roles ?? [];
  const allRoles = [...new Set<string>([...BUILT_IN_ROLES, ...customRoles])];
  const registration = auth.registration ?? { enabled: false, defaultRole: "User", selfAssignableRoles: [] };
  const tenancy = { ...DEFAULT_TENANCY, ...auth.multiTenancy };
  const otp = { ...DEFAULT_OTP, ...auth.otp };
  const messaging = { ...DEFAULT_MESSAGING, ...auth.messaging, smtp: { ...DEFAULT_MESSAGING.smtp, ...auth.messaging?.smtp } };
  const reset = { ...DEFAULT_RESET, ...auth.passwordReset };
  const companyRoles = [tenancy.adminRole, ...tenancy.memberRoles];
  const globalRoles = allRoles.filter((r) => !companyRoles.includes(r));

  const setTenancy = (patch: Partial<MultiTenancySpec>) => onChange({ ...auth, multiTenancy: { ...tenancy, ...patch } });
  const setOtp = (patch: Partial<OtpSpec>) => onChange({ ...auth, otp: { ...otp, ...patch } });
  const setMessaging = (patch: Partial<MessagingSpec>) => onChange({ ...auth, messaging: { ...messaging, ...patch } });
  const setSmtp = (patch: Partial<MessagingSpec["smtp"]>) => setMessaging({ smtp: { ...messaging.smtp, ...patch } });
  const setReset = (patch: Partial<PasswordResetSpec>) => onChange({ ...auth, passwordReset: { ...reset, ...patch } });

  return (
    <div className="divider">
      <div className="group-label">{m.title}</div>

      {registration.enabled && (
        <Field label={m.selfAssignable}>
          <RoleChips
            roles={globalRoles.filter((r) => r !== "Admin")}
            selected={registration.selfAssignableRoles ?? []}
            onChange={(next) => onChange({ ...auth, registration: { ...registration, selfAssignableRoles: next } })}
          />
          <div className="hint">{m.selfAssignableHint}</div>
        </Field>
      )}

      <Toggle on={tenancy.enabled} onClick={() => setTenancy({ enabled: !tenancy.enabled })} label={m.tenancy} />
      {tenancy.enabled && (
        <>
          <div className="field-row" style={{ marginTop: 8 }}>
            <Field label={m.adminRole} width={220}>
              <select className="uselect" value={tenancy.adminRole} onChange={(e) => setTenancy({ adminRole: e.target.value })}>
                {customRoles.map((r) => (
                  <option key={r} value={r}>{r}</option>
                ))}
              </select>
            </Field>
            <Field label={m.requireApproval}>
              <button className={`toggle ${tenancy.requireApproval ? "on" : ""}`} onClick={() => setTenancy({ requireApproval: !tenancy.requireApproval })}>
                <span className="knob" />
              </button>
            </Field>
          </div>
          <Field label={m.memberRoles}>
            <RoleChips
              roles={customRoles.filter((r) => r !== tenancy.adminRole && !tenancy.approverRoles.includes(r))}
              selected={tenancy.memberRoles}
              onChange={(next) => setTenancy({ memberRoles: next })}
            />
          </Field>
          <Field label={m.approverRoles}>
            <RoleChips
              roles={customRoles.filter((r) => !companyRoles.includes(r))}
              selected={tenancy.approverRoles}
              onChange={(next) => setTenancy({ approverRoles: next })}
            />
          </Field>
          <div className="hint">{m.tenancyHint}</div>
        </>
      )}

      <Toggle on={otp.enabled} onClick={() => setOtp({ enabled: !otp.enabled })} label={m.otp} />
      {otp.enabled && (
        <div className="field-row" style={{ marginTop: 8 }}>
          <Field label={m.codeLength} width={120}>
            <input className="uinput" type="number" min={4} max={8} value={otp.codeLength} onChange={(e) => setOtp({ codeLength: Number(e.target.value) })} />
          </Field>
          <Field label={m.lifetimeSeconds} width={150}>
            <input className="uinput" type="number" min={30} value={otp.lifetimeSeconds} onChange={(e) => setOtp({ lifetimeSeconds: Number(e.target.value) })} />
          </Field>
          <Field label={m.countryCode} width={120}>
            <input className="uinput mono" value={otp.defaultCountryCode} onChange={(e) => setOtp({ defaultCountryCode: e.target.value.trim() })} />
          </Field>
        </div>
      )}
      {otp.enabled && <div className="hint">{m.otpHint}</div>}

      <Toggle on={reset.enabled} onClick={() => setReset({ enabled: !reset.enabled })} label={m.passwordReset} />
      {reset.enabled && (
        <div className="field-row" style={{ marginTop: 8 }}>
          <Field label={m.resetUrl}>
            <input className="uinput mono" placeholder="https://app.ornek.com/sifre-sifirla" value={reset.resetUrl} onChange={(e) => setReset({ resetUrl: e.target.value.trim() })} />
          </Field>
          <Field label={m.tokenLifetimeHours} width={140}>
            <input className="uinput" type="number" min={1} value={reset.tokenLifetimeHours} onChange={(e) => setReset({ tokenLifetimeHours: Number(e.target.value) })} />
          </Field>
        </div>
      )}

      <div className="field-row" style={{ marginTop: 16 }}>
        <Field label={m.emailProvider} width={160}>
          <select className="uselect" value={messaging.emailProvider} onChange={(e) => setMessaging({ emailProvider: e.target.value })}>
            <option value="Log">Log ({m.devOnly})</option>
            <option value="Smtp">SMTP</option>
          </select>
        </Field>
        <Field label={m.accessTokenMinutes} width={160}>
          <input
            className="uinput"
            type="number"
            min={5}
            max={1440}
            value={auth.accessTokenLifetimeMinutes ?? 60}
            onChange={(e) => onChange({ ...auth, accessTokenLifetimeMinutes: Number(e.target.value) })}
          />
        </Field>
      </div>
      {messaging.emailProvider === "Smtp" && (
        <>
          <div className="field-row">
            <Field label="Host">
              <input className="uinput mono" placeholder="smtp.resend.com" value={messaging.smtp.host} onChange={(e) => setSmtp({ host: e.target.value.trim() })} />
            </Field>
            <Field label="Port" width={100}>
              <input className="uinput" type="number" value={messaging.smtp.port} onChange={(e) => setSmtp({ port: Number(e.target.value) })} />
            </Field>
            <Field label={m.smtpUser}>
              <input className="uinput mono" value={messaging.smtp.username ?? ""} onChange={(e) => setSmtp({ username: e.target.value.trim() })} />
            </Field>
          </div>
          <div className="field-row">
            <Field label={m.fromAddress}>
              <input className="uinput mono" placeholder="no-reply@ornek.com" value={messaging.smtp.fromAddress} onChange={(e) => setSmtp({ fromAddress: e.target.value.trim() })} />
            </Field>
            <Field label={m.fromName}>
              <input className="uinput" value={messaging.smtp.fromName} onChange={(e) => setSmtp({ fromName: e.target.value })} />
            </Field>
          </div>
          <div className="hint">{m.smtpHint}</div>
        </>
      )}
    </div>
  );
}
