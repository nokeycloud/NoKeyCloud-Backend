import { backendBaseUrl } from '@/shared/config';
import * as opaque from "@serenity-kit/opaque";

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
    const { clientLoginState, startLoginRequest } = opaque.client.startLogin({
        password,
    });

    const initRes = await fetch(`${backendBaseUrl}/api/Authenticate/login/init`, {
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
    const { finishLoginRequest, sessionKey } = loginResult;

    const verifyRes = await fetch(`${backendBaseUrl}/api/Authenticate/login/verify`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ sessionId, finishLoginRequest: finishLoginRequest })
    });

    if (!verifyRes.ok) throw new Error('Invalid credentials');

    const authData = (await verifyRes.json()) as LoginVerifyResponse;

    return authData;
};