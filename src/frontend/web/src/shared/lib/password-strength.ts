import { ZxcvbnFactory } from "@zxcvbn-ts/core";
import * as zxcvbnCommonPackage from "@zxcvbn-ts/language-common";
import * as zxcvbnEnPackage from "@zxcvbn-ts/language-en";

const zxcvbn = new ZxcvbnFactory({
    translations: zxcvbnEnPackage.translations,
    graphs: zxcvbnCommonPackage.adjacencyGraphs,
    dictionary: {
        ...zxcvbnCommonPackage.dictionary,
        ...zxcvbnEnPackage.dictionary,
    },
});

export type PasswordScore = 0 | 1 | 2 | 3 | 4;

export interface PasswordStrengthResult {
    score: PasswordScore;
    label: string;
    warning: string;
    suggestions: string[];
}

const SCORE_LABELS: Record<PasswordScore, string> = {
    0: "Very Weak",
    1: "Weak",
    2: "Fair",
    3: "Good",
    4: "Strong",
};

export function evaluatePasswordStrength(password: string): PasswordStrengthResult {
    const result = zxcvbn.check(password);

    const score = result.score as PasswordScore;

    return {
        score,
        label: SCORE_LABELS[score],
        warning: result.feedback?.warning ?? "",
        suggestions: result.feedback?.suggestions ?? [],
    };
}

export function isPasswordAcceptable(strength: PasswordStrengthResult | null): boolean {
    return strength !== null && strength.score >= 3;
}