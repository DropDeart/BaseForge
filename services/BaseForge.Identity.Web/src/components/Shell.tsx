import { type ReactNode } from "react";
import { FiMenu } from "react-icons/fi";
import { NavContent } from "./NavContent";
import { Logo } from "./Logo";
import { Button } from "./ui/button";
import { Sheet, SheetTrigger, SheetContent, SheetHeader, SheetTitle } from "./ui/sheet";
import type { MeResponse } from "../types";
import type { Page } from "../App";

export function Shell({
  me,
  page,
  onNavigate,
  onLogout,
  children,
}: {
  me: MeResponse;
  page: Page;
  onNavigate: (page: Page) => void;
  onLogout: () => void;
  children: ReactNode;
}) {
  return (
    <div className="flex min-h-screen bg-slate-50">
      <aside className="hidden w-64 shrink-0 border-r border-slate-200 bg-white md:block">
        <NavContent me={me} page={page} onNavigate={onNavigate} onLogout={onLogout} />
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <div className="flex items-center gap-3 border-b border-slate-200 bg-white px-4 py-3 md:hidden">
          <Sheet>
            <SheetTrigger render={<Button variant="ghost" size="icon" />}>
              <FiMenu size={18} />
            </SheetTrigger>
            <SheetContent side="left" className="w-72 p-0">
              <SheetHeader className="border-b border-slate-100 py-4">
                <SheetTitle>Menü</SheetTitle>
              </SheetHeader>
              <NavContent me={me} page={page} onNavigate={onNavigate} onLogout={onLogout} />
            </SheetContent>
          </Sheet>
          <Logo size="sm" onClick={() => onNavigate("home")} />
        </div>

        <main className="min-w-0 flex-1">{children}</main>
      </div>
    </div>
  );
}
