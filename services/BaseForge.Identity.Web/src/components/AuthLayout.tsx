import { type ReactNode } from "react";
import { Logo } from "./Logo";

export function AuthLayout({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle: string;
  children: ReactNode;
}) {
  return (
    <div className="flex min-h-screen w-full">
      <div className="hidden w-[42%] min-w-[380px] flex-col justify-between bg-gradient-to-br from-emerald-50 to-fuchsia-50 p-14 lg:flex">
        <Logo />
        <div>
          <h1 className="mb-3.5 text-4xl font-bold leading-tight text-emerald-950">{title}</h1>
          <p className="max-w-[340px] text-[15px] leading-relaxed text-emerald-800/80">{subtitle}</p>
        </div>
        <div />
      </div>

      <div className="flex flex-1 items-center justify-center p-5 sm:p-10">
        <div className="w-full max-w-[400px] rounded-2xl border border-slate-200 bg-white p-6 shadow-[0_1px_3px_rgba(0,0,0,0.06),0_12px_32px_rgba(0,0,0,0.06)] sm:p-10">
          {children}
        </div>
      </div>
    </div>
  );
}
