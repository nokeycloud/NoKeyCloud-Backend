import { backendBaseUrl } from "@/shared/config";
import { addCsrfHeader, invalidateCsrfToken, isSafeMethod } from "./csrf";
import { setGuest } from "@/entities/session";

async function send(url: string, options: RequestInit): Promise<Response> {
  const credentials = options.credentials ?? "include";
  return fetch(url, { ...options, credentials });
}

async function sendWithCsrf(
  url: string,
  options: RequestInit,
): Promise<Response> {
  return send(url, await addCsrfHeader(options));
}

async function sendWithCsrfRetry(
  url: string,
  options: RequestInit,
): Promise<Response> {
  let response = await sendWithCsrf(url, options);

  if (response.status === 403 && !isSafeMethod(options.method)) {
    invalidateCsrfToken();
    response = await sendWithCsrf(url, options);
  }

  return response;
}

export async function customFetch(
  url: string,
  options: RequestInit = {},
): Promise<Response> {
  const requestOptions: RequestInit = { ...options };
  const isAuthCall =
    /\/api\/Authenticate\/(login|refresh|logout|register)/i.test(url);

  let response = await sendWithCsrfRetry(url, requestOptions);

  if (response.status === 401 && !isAuthCall) {
    const refreshResponse = await sendWithCsrfRetry(
      `${backendBaseUrl}/api/Authenticate/refresh`,
      {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: "{}",
      },
    );

    if (refreshResponse.ok) {
      response = await sendWithCsrfRetry(url, requestOptions);
    } else {
      invalidateCsrfToken();
      setGuest();
    }
  }

  return response;
}
