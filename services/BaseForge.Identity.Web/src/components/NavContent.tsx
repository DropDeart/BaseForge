import { FiHome, FiUsers, FiLogOut } from "react-icons/fi";
import { Logo } from "./Logo";
import { Avatar } from "./Avatar";
import { Button } from "./ui/button";
import { Separator } from "./ui/separator";
import { cn } from "@/lib/utils";
import type { MeResponse } from "../types";
import type { Page } from "../App";

export function NavContent({
  me,
  page,
  onNavigate,
  onLogout,
}: {
  me: MeResponse;
  page: Page;
  onNavigate: (page: Page) => void;
  onLogout: () => void;
}) {
  const isAdmin = me.roles.includes("Admin");
  const items: { key: Page; label: string; icon: typeof FiHome }[] = [
    { key: "home", label: "Ana Sayfa", icon: FiHome },
    ...(isAdmin ? [{ key: "users" as Page, label: "Kullanıcılar", icon: FiUsers }] : []),
  ];

  return (
    <div className="flex h-full flex-col">
      <div className="px-5 py-5">
        <Logo size="sm" onClick={() => onNavigate("home")} />
      </div>

      <nav className="flex-1 space-y-1 px-3">
        {items.map((item) => (
          <button
            key={item.key}
            onClick={() => onNavigate(item.key)}
            className={cn(
              "flex w-full cursor-pointer items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors",
              page === item.key ? "bg-emerald-50 text-emerald-800" : "text-slate-600 hover:bg-slate-50",
            )}
          >
            <item.icon size={16} />
            {item.label}
          </button>
        ))}
      </nav>

      <Separator />

      <div className="flex flex-col gap-2 p-3">
        <button
          onClick={() => onNavigate("profile")}
          className={cn(
            "flex w-full cursor-pointer items-center gap-2.5 rounded-lg px-2 py-2 text-left transition-colors",
            page === "profile" ? "bg-emerald-50" : "hover:bg-slate-50",
          )}
        >
          <Avatar avatarUrl={me.avatarUrl} label={me.fullName ?? me.email} size="sm" />
          <div className="min-w-0">
            <div className="truncate text-xs font-medium text-slate-800">{me.fullName ?? me.email}</div>
            <div className={cn("text-[10px] font-semibold tracking-wide", isAdmin ? "text-emerald-700" : "text-slate-400")}>
              {isAdmin ? "YÖNETİCİ" : "KULLANICI"}
            </div>
          </div>
        </button>
        <Button variant="outline" onClick={onLogout} className="w-full justify-start">
          <FiLogOut size={13} />
          Çıkış yap
        </Button>
      </div>
    </div>
  );
}
