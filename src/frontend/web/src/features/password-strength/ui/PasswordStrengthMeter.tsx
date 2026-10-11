import { cn } from "@/shared/lib";
import type { PasswordStrengthResult } from "@/shared/lib";

interface PasswordStrengthMeterProps {
    strength: PasswordStrengthResult | null;
    hideFeedback?: boolean;
    className?: string;
}

const SEGMENT_BG = [
    "bg-red-500",
    "bg-orange-500",
    "bg-yellow-500",
    "bg-lime-500",
    "bg-green-500",
] as const;

const TEXT_COLOR = [
    "text-red-500",
    "text-orange-500",
    "text-yellow-600",
    "text-lime-600",
    "text-green-600",
] as const;

export function PasswordStrengthMeter({
                                          strength,
                                          hideFeedback = false,
                                          className,
                                      }: PasswordStrengthMeterProps) {
    if (!strength) return null;

    const { score, label, warning, suggestions } = strength;
    const hasFeedback = !hideFeedback && (warning || suggestions.length > 0);

    return (
        <div className={cn("space-y-2", className)}>
            <div className="flex items-center gap-3">
                <div className="flex flex-1 gap-1" aria-hidden="true">
                    {[0, 1, 2, 3, 4].map((i) => (
                        <div
                            key={i}
                            className={cn(
                                "h-1.5 flex-1 rounded-full transition-colors duration-200",
                                i <= score ? SEGMENT_BG[score] : "bg-muted"
                            )}
                        />
                    ))}
                </div>
                <span
                    className={cn(
                        "text-xs font-medium min-w-20 text-right",
                        TEXT_COLOR[score]
                    )}
                >
                    {label}
                </span>
            </div>

            {hasFeedback && (
                <div className="text-xs space-y-1">
                    {warning && (
                        <p className="text-destructive font-medium">{warning}</p>
                    )}
                    {suggestions.length > 0 && (
                        <ul className="text-muted-foreground list-disc pl-4 space-y-0.5">
                            {suggestions.map((s, i) => (
                                <li key={i}>{s}</li>
                            ))}
                        </ul>
                    )}
                </div>
            )}
        </div>
    );
}