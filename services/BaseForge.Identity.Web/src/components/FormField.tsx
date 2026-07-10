import { forwardRef, type InputHTMLAttributes } from "react";
import { Label } from "./ui/label";
import { Input } from "./ui/input";

interface FormFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
}

export const FormField = forwardRef<HTMLInputElement, FormFieldProps>(function FormField(
  { label, id, name, className, ...props },
  ref,
) {
  const inputId = id ?? name;
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={inputId} className="text-xs font-medium text-slate-500">
        {label}
      </Label>
      <Input ref={ref} id={inputId} name={name} className={className} {...props} />
    </div>
  );
});
