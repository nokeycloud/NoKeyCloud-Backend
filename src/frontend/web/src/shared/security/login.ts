import { backendBaseUrl, getTrustedOpaqueServerKey } from '@/shared/config';
import * as opaque from "@serenity-kit/opaque";
import { ServerIdentityMismatchError } from "./errors.ts";
import { customFetch } from '@/shared/api';
import { invalidateCsrfToken } from '@/shared/api/csrf';

export interface LoginInitResponse {
    loginResponse: string;
    sessionId: string;
}

export interface LoginVerifyResponse {
    rootFolderId: string;
    encryptedMasterKey: string;
    keySalt: string;
}

export const loginWithOpaque = async (username: string, password: string) => {
    invalidateCsrfToken();

    const { clientLoginState, startLoginRequest } = opaque.client.startLogin({
        password,
    });

    const initRes = await customFetch(`${backendBaseUrl}/api/Authenticate/login/init`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, email: username, startLoginRequest: startLoginRequest })
    });

    if (!initRes.ok) throw new Error('Invalid credentials');

    const {loginResponse, sessionId } = (await initRes.json()) as LoginInitResponse;

    const loginResult = opaque.client.finishLogin({
        clientLoginState,
        loginResponse,
        password,
    });
    if (!loginResult) {
        throw new Error("Login failed");
    }

    const trustedKey = getTrustedOpaqueServerKey();
    if (loginResult.serverStaticPublicKey !== trustedKey) {
        throw new ServerIdentityMismatchError();
    }

    const verifyRes = await customFetch(`${backendBaseUrl}/api/Authenticate/login/verify`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ sessionId, finishLoginRequest: loginResult.finishLoginRequest })
    });

    if (!verifyRes.ok) throw new Error('Invalid credentials');

    const authData = (await verifyRes.json()) as LoginVerifyResponse;

    return authData;
};