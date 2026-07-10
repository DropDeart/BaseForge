import { Avatar as AvatarRoot, AvatarImage, AvatarFallback } from "./ui/avatar";
import { cn } from "@/lib/utils";

export function Avatar({
  avatarUrl,
  label,
  size = "default",
  className,
}: {
  avatarUrl: string | null;
  label: string;
  size?: "sm" | "default" | "lg";
  className?: string;
}) {
  return (
    <AvatarRoot size={size} className={cn(size === "lg" && "size-20", className)}>
      {avatarUrl && <AvatarImage src={avatarUrl} alt={label} />}
      <AvatarFallback className="bg-emerald-100 font-bold text-emerald-800">
        {label[0]?.toUpperCase() ?? "?"}
      </AvatarFallback>
    </AvatarRoot>
  );
}
