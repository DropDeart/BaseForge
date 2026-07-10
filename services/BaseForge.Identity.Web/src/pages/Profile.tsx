import { useRef, useState, type FormEvent } from "react";
import { FiCamera } from "react-icons/fi";
import { Avatar } from "../components/Avatar";
import { Button } from "../components/ui/button";
import { FormField } from "../components/FormField";
import { api } from "../api";
import type { MeResponse } from "../types";

const MAX_AVATAR_BYTES = 2 * 1024 * 1024;
const ALLOWED_TYPES = ["image/jpeg", "image/png", "image/webp", "image/gif"];

export function Profile({ me, onUpdated }: { me: MeResponse; onUpdated: (next: MeResponse) => void }) {
  const fileInput = useRef<HTMLInputElement>(null);
  const [avatarUrl, setAvatarUrl] = useState(me.avatarUrl);
  const [avatarError, setAvatarError] = useState("");
  const [uploading, setUploading] = useState(false);

  const [fullName, setFullName] = useState(me.fullName ?? "");
  const [nameSaving, setNameSaving] = useState(false);
  const [nameSaved, setNameSaved] = useState(false);
  const [nameError, setNameError] = useState("");

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [newPassword2, setNewPassword2] = useState("");
  const [passwordSaving, setPasswordSaving] = useState(false);
  const [passwordSaved, setPasswordSaved] = useState(false);
  const [passwordError, setPasswordError] = useState("");

  const pickAvatar = () => fileInput.current?.click();

  const onAvatarChange = async (file: File | undefined) => {
    if (!file) return;
    setAvatarError("");

    if (!ALLOWED_TYPES.includes(file.type)) {
      setAvatarError("Sadece JPEG, PNG, WEBP veya GIF yükleyebilirsiniz.");
      return;
    }
    if (file.size > MAX_AVATAR_BYTES) {
      setAvatarError("Dosya en fazla 2 MB olabilir.");
      return;
    }

    setUploading(true);
    try {
      const { avatarUrl: next } = await api.uploadAvatar(file);
      setAvatarUrl(next);
      onUpdated({ ...me, avatarUrl: next });
    } catch (err) {
      setAvatarError(err instanceof Error ? err.message : "Yükleme başarısız.");
    } finally {
      setUploading(false);
    }
  };

  const saveName = async (e: FormEvent) => {
    e.preventDefault();
    setNameError("");
    setNameSaved(false);
    setNameSaving(true);
    try {
      await api.updateProfile(fullName);
      onUpdated({ ...me, fullName: fullName || null });
      setNameSaved(true);
    } catch (err) {
      setNameError(err instanceof Error ? err.message : "Kaydedilemedi.");
    } finally {
      setNameSaving(false);
    }
  };

  const savePassword = async (e: FormEvent) => {
    e.preventDefault();
    setPasswordError("");
    setPasswordSaved(false);
    if (newPassword !== newPassword2) {
      setPasswordError("Yeni parolalar eşleşmiyor.");
      return;
    }
    setPasswordSaving(true);
    try {
      await api.changePassword(me.hasPassword ? currentPassword : null, newPassword);
      setCurrentPassword("");
      setNewPassword("");
      setNewPassword2("");
      setPasswordSaved(true);
      onUpdated({ ...me, hasPassword: true });
    } catch (err) {
      setPasswordError(err instanceof Error ? err.message : "Parola güncellenemedi.");
    } finally {
      setPasswordSaving(false);
    }
  };

  return (
    <div className="mx-auto max-w-xl px-8 py-10">
      <h1 className="mb-6 text-xl font-bold text-slate-900">Profilim</h1>

      <section className="mb-6 flex items-center gap-5 rounded-2xl border border-slate-200 bg-white p-6">
        <div className="relative">
          <Avatar avatarUrl={avatarUrl} label={me.fullName ?? me.email} size="lg" />
          <button
            type="button"
            onClick={pickAvatar}
            disabled={uploading}
            title="Fotoğraf değiştir"
            className="absolute -bottom-1 -right-1 flex h-7 w-7 cursor-pointer items-center justify-center rounded-full bg-emerald-600 text-white shadow hover:bg-emerald-700 disabled:opacity-50"
          >
            <FiCamera size={13} />
          </button>
          <input
            ref={fileInput}
            type="file"
            accept="image/jpeg,image/png,image/webp,image/gif"
            className="hidden"
            onChange={(e) => onAvatarChange(e.target.files?.[0])}
          />
        </div>
        <div>
          <div className="text-sm font-semibold text-slate-800">{me.fullName ?? me.email}</div>
          <div className="text-xs text-slate-500">{me.email}</div>
          {uploading && <div className="mt-1 text-xs text-slate-400">Yükleniyor…</div>}
          {avatarError && <div className="mt-1 text-xs text-red-600">{avatarError}</div>}
        </div>
      </section>

      <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-6">
        <h2 className="mb-4 text-sm font-semibold text-slate-800">Ad Soyad</h2>
        <form onSubmit={saveName} className="flex items-end gap-3">
          <div className="flex-1">
            <FormField label="Ad Soyad" value={fullName} onChange={(e) => setFullName(e.target.value)} placeholder="Adınız Soyadınız" />
          </div>
          <Button type="submit" disabled={nameSaving}>{nameSaving ? "Kaydediliyor…" : "Kaydet"}</Button>
        </form>
        {nameError && <div className="mt-2 text-xs text-red-600">{nameError}</div>}
        {nameSaved && <div className="mt-2 text-xs text-emerald-700">Kaydedildi.</div>}
      </section>

      <section className="rounded-2xl border border-slate-200 bg-white p-6">
        <h2 className="mb-1 text-sm font-semibold text-slate-800">{me.hasPassword ? "Parola değiştir" : "Parola belirle"}</h2>
        <p className="mb-4 text-xs text-slate-500">
          {me.hasPassword
            ? "Mevcut parolanı doğrulayıp yenisini belirle."
            : "Hesabın şu an sadece dış sağlayıcı ile girişe açık — parola belirlersen e-posta/parola ile de giriş yapabilirsin."}
        </p>
        <form onSubmit={savePassword} className="flex flex-col gap-3">
          {me.hasPassword && (
            <FormField
              label="Mevcut parola"
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              autoComplete="current-password"
            />
          )}
          <FormField label="Yeni parola" type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} autoComplete="new-password" />
          <FormField label="Yeni parola (tekrar)" type="password" value={newPassword2} onChange={(e) => setNewPassword2(e.target.value)} autoComplete="new-password" />
          <Button type="submit" disabled={passwordSaving} className="self-start">
            {passwordSaving ? "Kaydediliyor…" : me.hasPassword ? "Parolayı güncelle" : "Parola belirle"}
          </Button>
        </form>
        {passwordError && <div className="mt-2 text-xs text-red-600">{passwordError}</div>}
        {passwordSaved && <div className="mt-2 text-xs text-emerald-700">Parola güncellendi.</div>}
      </section>
    </div>
  );
}
