declare global {
    interface Window {
        __NK_CONFIG__?: { OPAQUE_SERVER_PUBLIC_KEY?: string };
    }
}

export function getTrustedOpaqueServerKey(): string {
    const key = window.__NK_CONFIG__?.OPAQUE_SERVER_PUBLIC_KEY;

    if (!key) {
        const devKey = import.meta.env.VITE_OPAQUE_SERVER_PUBLIC_KEY as string | undefined;
        if (devKey) return devKey;

        throw new Error(
            "OPAQUE_SERVER_PUBLIC_KEY not configured. " +
            "set VITE_OPAQUE_SERVER_PUBLIC_KEY."
        );
    }

    return key;
}