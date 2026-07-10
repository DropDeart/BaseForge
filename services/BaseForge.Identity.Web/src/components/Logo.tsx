import clsx from "clsx";

export function Logo({ size = "md", onClick }: { size?: "sm" | "md"; onClick?: () => void }) {
  const dims = size === "sm" ? "h-8 w-8 text-xs" : "h-9.5 w-9.5 text-sm";
  const content = (
    <>
      <div className={clsx("flex items-center justify-center rounded-[10px] bg-emerald-600 font-bold text-white", dims)}>
        BF
      </div>
      <span className="text-base font-semibold text-slate-800">BaseForge</span>
    </>
  );

  if (onClick) {
    return (
      <button type="button" onClick={onClick} className="flex cursor-pointer items-center gap-3">
        {content}
      </button>
    );
  }

  return <div className="flex items-center gap-3">{content}</div>;
}
