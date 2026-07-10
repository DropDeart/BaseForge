import { useEffect, useState, type FormEvent } from "react";
import { FaGoogle, FaGithub, FaMicrosoft, FaFacebookF } from "react-icons/fa";
import { AuthLayout } from "../components/AuthLayout";
import { FormField } from "../components/FormField";
import { Button } from "../components/ui/button";
import { api } from "../api";

const PROVIDER_META: Record<string, { label: string; icon: typeof FaGoogle; bg: string; text: string }> = {
  Google: { label: "Google", icon: FaGoogle, bg: "bg-red-50", text: "text-red-600" },
  GitHub: { label: "GitHub", icon: FaGithub, bg: "bg-slate-100", text: "text-slate-800" },
  Microsoft: { label: "Microsoft", icon: FaMicrosoft, bg: "bg-blue-50", text: "text-blue-600" },
  Facebook: { label: "Facebook", icon: FaFacebookF, bg: "bg-indigo-50", text: "text-indigo-600" },
};

export function Login({ returnUrl, onSwitchToRegister, onLoggedIn }: {
  returnUrl: string | null;
  onSwitchToRegister: () => void;
  onLoggedIn: () => void;
}) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [providers, setProviders] = useState<string[]>([]);

  useEffect(() => {
    api.providers().then(setProviders).catch(() => setProviders([]));
  }, []);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    if (!email || !password) {
      setError("E-posta ve parola gerekli.");
      return;
    }
    setLoading(true);
    try {
      await api.login(email, password);
      if (returnUrl) {
        window.location.href = returnUrl;
      } else {
        onLoggedIn();
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Giriş başarısız.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthLayout title="Ortak Giriş" subtitle="Tek hesapla tüm BaseForge servislerine erişin. Yetkileriniz servis bazında yönetilir.">
      <h2 className="mb-1 text-xl font-bold text-slate-900">Giriş yap</h2>
      <p className="mb-6 text-sm text-slate-500">BaseForge Identity ile devam edin</p>

      {error && <div className="mb-4 rounded-lg bg-red-50 px-3 py-2.5 text-xs text-red-700">{error}</div>}

      <form onSubmit={submit} className="flex flex-col gap-4">
        <FormField
          label="E-posta"
          type="email"
          placeholder="ad.soyad@baseforge.local"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          autoComplete="username"
        />
        <FormField
          label="Parola"
          type="password"
          placeholder="••••••••"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          autoComplete="current-password"
        />
        <Button type="submit" disabled={loading} className="mt-1 w-full">
          {loading ? "Giriş yapılıyor…" : "Giriş yap"}
        </Button>
      </form>

      {providers.length > 0 && (
        <>
          <div className="my-5.5 flex items-center gap-2.5">
            <div className="h-px flex-1 bg-slate-100" />
            <span className="text-[11px] text-slate-400">veya</span>
            <div className="h-px flex-1 bg-slate-100" />
          </div>

          <div className="grid grid-cols-2 gap-2.5">
            {providers.map((key) => {
              const meta = PROVIDER_META[key];
              if (!meta) return null;
              const Icon = meta.icon;
              return (
                <a
                  key={key}
                  href={api.externalLoginUrl(key, returnUrl ?? "/")}
                  className="flex items-center gap-2 rounded-lg border border-slate-200 bg-white px-2.5 py-2.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
                >
                  <span className={`flex h-5 w-5 items-center justify-center rounded-md ${meta.bg} ${meta.text}`}>
                    <Icon size={11} />
                  </span>
                  {meta.label}
                </a>
              );
            })}
          </div>
        </>
      )}

      <p className="mt-6 text-center text-xs text-slate-500">
        Hesabın yok mu?{" "}
        <button type="button" onClick={onSwitchToRegister} className="cursor-pointer font-medium text-emerald-700 hover:text-emerald-800">
          Kayıt ol
        </button>
      </p>
    </AuthLayout>
  );
}
