import { backendBaseUrl } from "@/shared/config";

const CSRF_HEADER = "X-CSRF-TOKEN";
const SAFE_METHODS = new Set(["GET", "HEAD", "OPTIONS", "TRACE"]);

let token: string | null = null;
let inflight: Promise<string> | null = null;
let version = 0;

export function isSafeMethod(method?: string): boolean {
  return SAFE_METHODS.has((method ?? "GET").toUpperCase());
}

async function fetchToken(): Promise<string> {
  const requestVersion = ++version;

  const response = await fetch(`${backendBaseUrl}/api/Antiforgery/token`, {
    method: "GET",
    credentials: "include",
    cache: "no-store",
    headers: { Accept: "application/json" },
  });

  if (!response.ok) {
    throw new Error(`Failed to obtain CSRF token (HTTP ${response.status})`);
  }

  const data = (await response.json()) as { token?: string };

  if (!data.token) {
    throw new Error("CSRF token missing in response");
  }

  if (requestVersion === version) {
    token = data.token;
  }

  return data.token;
}

export function getCsrfToken(): Promise<string> {
  if (token) {
    return Promise.resolve(token);
  }

  if (!inflight) {
    inflight = fetchToken().finally(() => {
      inflight = null;
    });
  }

  return inflight;
}

export function invalidateCsrfToken(): void {
  token = null;
  inflight = null;
  version++;
}

export async function addCsrfHeader(
  options: RequestInit,
): Promise<RequestInit> {
  if (isSafeMethod(options.method)) {
    return options;
  }

  const csrfToken = await getCsrfToken();
  const headers = new Headers(options.headers);
  headers.set(CSRF_HEADER, csrfToken);

  return { ...options, headers };
}
