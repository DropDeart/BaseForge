import { useState } from "react";
import { api } from "../api/client";
import type { WorkspaceEntry } from "../types";

/**
 * "Arayüz oluştur" butonu: workspace'te birden fazla servis varsa checkbox ile seçtirir
 * (aralarındaki externalRefs ilişkileri 'uidesign' tarafında çözümlenir — burada sadece hangi
 * servislerin dahil edileceği seçilir), sonra ayrı bir tool olan 'uidesign'ı (BaseForge.UiDesigner.Cli)
 * seçilen servislerle başlatıp dönen URL'i yeni sekmede açar.
 */
export function UiDesignLauncher({ workspace, defaultService }: { workspace: WorkspaceEntry[]; defaultService: string }) {
  const [open, setOpen] = useState(false);
  const [selected, setSelected] = useState<Set<string>>(() => new Set([defaultService]));
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  // identity'yi tasarım seçimine dahil etmiyoruz — kendi login/admin arayüzü zaten var.
  const candidates = workspace.filter((w) => !w.isIdentity);
  const multiple = candidates.length > 1;

  const toggle = (name: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(name)) next.delete(name);
      else next.add(name);
      return next;
    });
  };

  const launch = async () => {
    const services = multiple ? Array.from(selected) : [defaultService];
    if (services.length === 0) {
      setMessage("En az bir servis seçin.");
      return;
    }

    setBusy(true);
    setMessage(null);
    try {
      const res = await api.launchUiDesign(services);
      if (res.success && res.url) {
        window.open(res.url, "_blank", "noopener,noreferrer");
        setMessage(res.message);
        setOpen(false);
      } else {
        setMessage(res.message);
      }
    } catch (e) {
      setMessage(String(e));
    } finally {
      setBusy(false);
    }
  };

  if (!open) {
    return (
      <div className="run-row">
        <button className="btn" onClick={() => setOpen(true)}>Arayüz oluştur</button>
        {message && <span className="hint">{message}</span>}
      </div>
    );
  }

  return (
    <div className="result" style={{ marginTop: 12 }}>
      {multiple ? (
        <>
          <div className="group-label">Arayüze dahil edilecek servisler</div>
          {candidates.map((c) => (
            <label key={c.name} style={{ display: "flex", alignItems: "center", gap: 8, padding: "4px 0", fontSize: 13 }}>
              <input type="checkbox" checked={selected.has(c.name)} onChange={() => toggle(c.name)} />
              {c.name} <span className="hint">({c.entityCount ?? 0} entity)</span>
            </label>
          ))}
        </>
      ) : (
        <div className="hint">Yalnızca '{defaultService}' arayüze dahil edilecek.</div>
      )}
      <div className="run-row">
        <button className="btn btn-primary" disabled={busy} onClick={launch}>
          {busy ? "Başlatılıyor…" : "Başlat"}
        </button>
        <button className="btn" disabled={busy} onClick={() => setOpen(false)}>Vazgeç</button>
      </div>
      {message && <div className="hint" style={{ marginTop: 6 }}>{message}</div>}
    </div>
  );
}
