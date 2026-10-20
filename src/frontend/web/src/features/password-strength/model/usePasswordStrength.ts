import { useEffect, useMemo, useState } from "react";
import {
    evaluatePasswordStrength,
    type PasswordStrengthResult,
} from "@/shared/lib";

export function usePasswordStrength(
    password: string,
    debounceMs = 150
): PasswordStrengthResult | null {
    const [debounced, setDebounced] = useState(password);

    useEffect(() => {
        const timer = setTimeout(() => setDebounced(password), debounceMs);
        return () => clearTimeout(timer);
    }, [password, debounceMs]);

    return useMemo(() => {
        if (!debounced) return null;
        try {
            return evaluatePasswordStrength(debounced);
        } catch (err) {
            console.error("Password strength evaluation failed", err);
            return null;
        }
    }, [debounced]);
}