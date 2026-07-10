import { useEffect, useState, type FormEvent } from "react";
import { FiPlus, FiTrash2, FiCheckCircle, FiXCircle } from "react-icons/fi";
import { Button } from "../components/ui/button";
import { FormField } from "../components/FormField";
import { RoleBadge } from "../components/RoleBadge";
import { Avatar } from "../components/Avatar";
import { api } from "../api";
import type { AdminUserRow } from "../types";

export function AdminUsers() {
  const [users, setUsers] = useState<AdminUserRow[]>([]);
  const [roles, setRoles] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [addOpen, setAddOpen] = useState(false);
  const [newFullName, setNewFullName] = useState("");
  const [newEmail, setNewEmail] = useState("");

  const load = async () => {
    setLoading(true);
    try {
      const [u, r] = await Promise.all([api.listUsers(), api.roles()]);
      setUsers(u);
      setRoles(r);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Kullanıcılar yüklenemedi.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const addUser = async (e: FormEvent) => {
    e.preventDefault();
    if (!newEmail) return;
    const user = await api.addUser(newFullName, newEmail);
    setUsers((prev) => [...prev, user]);
    setAddOpen(false);
    setNewFullName("");
    setNewEmail("");
  };

  const deleteUser = async (id: string) => {
    await api.deleteUser(id);
    setUsers((prev) => prev.filter((u) => u.id !== id));
  };

  const addRole = async (id: string, role: string) => {
    if (!role) return;
    await api.addRole(id, role);
    setUsers((prev) => prev.map((u) => (u.id === id ? { ...u, roles: [...u.roles, role] } : u)));
  };

  const removeRole = async (id: string, role: string) => {
    await api.removeRole(id, role);
    setUsers((prev) => prev.map((u) => (u.id === id ? { ...u, roles: u.roles.filter((r) => r !== role) } : u)));
  };

  return (
    <div className="px-8 py-10 sm:px-12">
      <div className="mb-5 flex items-end justify-between">
        <div>
          <h1 className="mb-1 text-xl font-bold text-slate-900">Kullanıcılar</h1>
          <p className="text-sm text-slate-500">{users.length} kullanıcı</p>
        </div>
        <Button onClick={() => setAddOpen((v) => !v)}>
          <FiPlus size={14} /> Kullanıcı ekle
        </Button>
      </div>

      {error && <div className="mb-4 rounded-lg bg-red-50 px-3 py-2.5 text-xs text-red-700">{error}</div>}

      {addOpen && (
        <form
          onSubmit={addUser}
          className="mb-4 flex flex-wrap items-end gap-4 rounded-xl border border-emerald-200 bg-emerald-50 px-5 py-4"
        >
          <div className="min-w-[180px] flex-1">
            <FormField label="Ad Soyad" value={newFullName} onChange={(e) => setNewFullName(e.target.value)} placeholder="Adınız Soyadınız" />
          </div>
          <div className="min-w-[180px] flex-1">
            <FormField label="E-posta" type="email" value={newEmail} onChange={(e) => setNewEmail(e.target.value)} placeholder="ad.soyad@baseforge.local" />
          </div>
          <Button type="submit" variant="secondary">Ekle</Button>
          <Button type="button" variant="ghost" onClick={() => setAddOpen(false)}>Vazgeç</Button>
        </form>
      )}

      <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
        <div className="min-w-[720px]">
          <div className="grid grid-cols-[2fr_1.4fr_0.8fr_2fr_1.6fr_0.6fr] gap-3 border-b border-slate-200 px-5 py-3">
            {["E-posta", "Ad Soyad", "Onaylı", "Roller", "Rol ekle", ""].map((h) => (
              <div key={h} className="text-[10.5px] font-semibold uppercase tracking-wide text-slate-400">{h}</div>
            ))}
          </div>

          {loading ? (
            <div className="px-5 py-8 text-center text-sm text-slate-400">Yükleniyor…</div>
          ) : (
            users.map((u) => (
              <div key={u.id} className="grid grid-cols-[2fr_1.4fr_0.8fr_2fr_1.6fr_0.6fr] items-center gap-3 border-b border-slate-100 px-5 py-3.5">
                <div className="flex items-center gap-2.5 overflow-hidden">
                  <Avatar avatarUrl={u.avatarUrl} label={u.fullName ?? u.email} size="sm" className="shrink-0" />
                  <span className="truncate font-mono text-[13px] text-slate-800">{u.email}</span>
                </div>
                <div className="text-[13px] text-slate-700">{u.fullName ?? "—"}</div>
                <div className={`flex items-center gap-1 text-[13px] font-semibold ${u.emailConfirmed ? "text-emerald-700" : "text-red-600"}`}>
                  {u.emailConfirmed ? <FiCheckCircle size={13} /> : <FiXCircle size={13} />}
                </div>
                <div className="flex flex-wrap gap-1.5">
                  {u.roles.map((r) => (
                    <RoleBadge key={r} role={r} onRemove={() => removeRole(u.id, r)} />
                  ))}
                </div>
                <select
                  defaultValue=""
                  onChange={(e) => {
                    addRole(u.id, e.target.value);
                    e.target.value = "";
                  }}
                  className="cursor-pointer rounded-md border border-slate-200 bg-slate-50 px-2 py-1.5 text-xs text-slate-500 outline-none"
                >
                  <option value="" disabled>+ Rol ekle</option>
                  {roles.filter((r) => !u.roles.includes(r)).map((r) => (
                    <option key={r} value={r}>{r}</option>
                  ))}
                </select>
                <button
                  onClick={() => deleteUser(u.id)}
                  title="Kullanıcıyı sil"
                  className="flex cursor-pointer items-center justify-center rounded-md bg-red-50 p-1.5 text-red-600 hover:bg-red-100"
                >
                  <FiTrash2 size={13} />
                </button>
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  );
}
