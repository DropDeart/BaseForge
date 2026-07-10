import { Badge } from "./ui/badge";
import { cn } from "@/lib/utils";

const palette: Record<string, string> = {
  Admin: "bg-emerald-100 text-emerald-800",
  User: "bg-slate-100 text-slate-600",
};

export function RoleBadge({ role, onRemove }: { role: string; onRemove?: () => void }) {
  const colors = palette[role] ?? "bg-fuchsia-100 text-fuchsia-800";
  return (
    <Badge variant="secondary" className={cn("gap-1 rounded-full border-0 py-1 pl-2.5 pr-1.5", colors)}>
      {role}
      {onRemove && (
        <button
          type="button"
          onClick={onRemove}
          title="Rolü kaldır"
          className="cursor-pointer rounded-full leading-none opacity-60 hover:opacity-100"
        >
          ×
        </button>
      )}
    </Badge>
  );
}
