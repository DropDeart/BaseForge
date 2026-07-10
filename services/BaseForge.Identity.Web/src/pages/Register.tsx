import { useState, type FormEvent } from "react";
import { AuthLayout } from "../components/AuthLayout";
import { FormField } from "../components/FormField";
import { Button } from "../components/ui/button";
import { api } from "../api";

export function Register({ returnUrl, onSwitchToLogin, onRegistered }: {
  returnUrl: string | null;
  onSwitchToLogin: () => void;
  onRegistered: () => void;
}) {
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [password2, setPassword2] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    if (!fullName || !email || !password) {
      setError("Tüm alanları doldurun.");
      return;
    }
    if (password !== password2) {
      setError("Parolalar eşleşmiyor.");
      return;
    }
    setLoading(true);
    try {
      await api.register(fullName, email, password);
      if (returnUrl) {
        window.location.href = returnUrl;
      } else {
        onRegistered();
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Kayıt başarısız.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthLayout title="Aramıza katılın" subtitle="Bir hesap oluşturun, yöneticiniz size uygun servisler için yetki tanımlasın.">
      <h2 className="mb-1 text-xl font-bold text-slate-900">Kayıt ol</h2>
      <p className="mb-6 text-sm text-slate-500">Yeni bir BaseForge hesabı oluşturun</p>

      {error && <div className="mb-4 rounded-lg bg-red-50 px-3 py-2.5 text-xs text-red-700">{error}</div>}

      <form onSubmit={submit} className="flex flex-col gap-4">
        <FormField label="Ad Soyad" type="text" placeholder="Adınız Soyadınız" value={fullName} onChange={(e) => setFullName(e.target.value)} autoComplete="name" />
        <FormField label="E-posta" type="email" placeholder="ad.soyad@baseforge.local" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="username" />
        <FormField label="Parola" type="password" placeholder="••••••••" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" />
        <FormField label="Parola (tekrar)" type="password" placeholder="••••••••" value={password2} onChange={(e) => setPassword2(e.target.value)} autoComplete="new-password" />
        <Button type="submit" disabled={loading} className="mt-1 w-full">
          {loading ? "Oluşturuluyor…" : "Hesap oluştur"}
        </Button>
      </form>

      <p className="mt-6 text-center text-xs text-slate-500">
        Zaten hesabın var mı?{" "}
        <button type="button" onClick={onSwitchToLogin} className="cursor-pointer font-medium text-emerald-700 hover:text-emerald-800">
          Giriş yap
        </button>
      </p>
    </AuthLayout>
  );
}
