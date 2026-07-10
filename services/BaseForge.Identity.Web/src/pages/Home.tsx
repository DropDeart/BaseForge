import { useEffect, useState } from "react";
import { FiShield, FiUser, FiServer, FiLock, FiGrid } from "react-icons/fi";
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from "../components/ui/card";
import { Badge } from "../components/ui/badge";
import { api } from "../api";
import type { ServiceRegistryEntry } from "../types";

export function Home() {
  const [services, setServices] = useState<ServiceRegistryEntry[] | "loading">("loading");

  useEffect(() => {
    api.services().then(setServices);
  }, []);

  const connected = services === "loading" ? [] : services.filter((s) => !s.isIdentity);

  return (
    <div className="mx-auto max-w-4xl px-6 py-10 sm:px-10">
      <h1 className="mb-1 text-xl font-bold text-slate-900">BaseForge Identity</h1>
      <p className="mb-8 max-w-2xl text-sm leading-relaxed text-slate-500">
        Bu, BaseForge mikroservisleri için <strong>ortak giriş</strong> (merkezi kimlik doğrulama) sistemidir.
        Burada oluşturduğun tek hesap, aşağıda listelenen tüm bağlı servislere aynı oturumla erişim sağlar —
        her servisi ayrı ayrı hesap açmana gerek kalmaz. Yetkiler roller üzerinden yönetilir; hangi servislere
        erişebileceğin sağdaki menüden değil, her servisin kendi yetkilendirme kurallarından belirlenir.
      </p>

      <div className="mb-10 flex flex-col items-center gap-3 rounded-2xl border border-slate-200 bg-white p-8 sm:flex-row sm:justify-center sm:gap-6">
        <DiagramNode icon={FiUser} label="Kullanıcı" />
        <DiagramArrow label="giriş yapar" />
        <DiagramNode icon={FiShield} label="BaseForge Identity" highlight />
        <DiagramArrow label="JWT ile doğrular" />
        <DiagramNode icon={FiServer} label={connected.length > 0 ? `${connected.length} servis` : "servisler"} />
      </div>

      <div className="mb-3 flex items-center gap-2">
        <FiGrid className="text-slate-400" size={15} />
        <h2 className="text-sm font-semibold text-slate-800">Bağlı servisler</h2>
      </div>

      {services === "loading" ? (
        <p className="text-sm text-slate-400">Yükleniyor…</p>
      ) : connected.length === 0 ? (
        <Card>
          <CardContent className="py-6 text-sm text-slate-500">
            Henüz bağlı bir servis yok. Tasarımcıda bir servis üretirken "Merkez Identity'ye JWT ile bağla"
            seçeneğini işaretlersen, o servis burada otomatik olarak listelenir.
          </CardContent>
        </Card>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {connected.map((s) => (
            <Card key={s.name}>
              <CardHeader>
                <CardTitle className="capitalize">{s.name}</CardTitle>
                <CardDescription>
                  {s.entityCount !== null ? `${s.entityCount} entity` : "servis"}
                  {s.restPort ? ` · :${s.restPort}` : ""}
                </CardDescription>
              </CardHeader>
              <CardContent className="flex flex-wrap gap-1.5">
                {s.protected && (
                  <Badge variant="secondary" className="gap-1 bg-emerald-100 text-emerald-800">
                    <FiLock size={10} /> JWT korumalı
                  </Badge>
                )}
                {s.audience && <Badge variant="outline">aud: {s.audience}</Badge>}
                {s.grpcPort && <Badge variant="outline">gRPC :{s.grpcPort}</Badge>}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}

function DiagramNode({ icon: Icon, label, highlight }: { icon: typeof FiUser; label: string; highlight?: boolean }) {
  return (
    <div className="flex flex-col items-center gap-2">
      <div
        className={
          highlight
            ? "flex h-14 w-14 items-center justify-center rounded-2xl bg-emerald-600 text-white shadow-sm"
            : "flex h-14 w-14 items-center justify-center rounded-2xl bg-slate-100 text-slate-500"
        }
      >
        <Icon size={22} />
      </div>
      <span className="text-xs font-medium text-slate-600">{label}</span>
    </div>
  );
}

function DiagramArrow({ label }: { label: string }) {
  return (
    <div className="flex flex-col items-center gap-1 px-1 text-slate-300">
      <span className="text-[10px] whitespace-nowrap text-slate-400">{label}</span>
      <span className="rotate-90 text-lg leading-none sm:rotate-0">→</span>
    </div>
  );
}
