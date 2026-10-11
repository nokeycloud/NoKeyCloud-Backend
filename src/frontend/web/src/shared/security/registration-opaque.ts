import * as opaque from "@serenity-kit/opaque";
import {backendBaseUrl} from "@/shared/config";

export interface RegistrationOpaque {
    userIdentifier: string;
    registrationRecord: string;
}

export interface RegistrationError {
    error: {
        errors: {
            body: string[];
        };
    };
}

export async function registrationOpaque(password: string): Promise<RegistrationOpaque | RegistrationError> {
    const userIdentifier = crypto.randomUUID();

    const { clientRegistrationState, registrationRequest } = opaque.client.startRegistration({ password });

    const firstResponse = await fetch(`${backendBaseUrl}/api/Authenticate/register/init`, {
        method: "POST",
        headers: {"Content-Type": "application/json"},
        body: JSON.stringify({
            userIdentifier,
            registrationRequest
        })
    });

    if (!firstResponse.ok) {
        return { error: { errors: { body: ["Registration init failed."] } } };
    }

    const { registrationResponse } = (await firstResponse.json()) as { registrationResponse: string };

    const { registrationRecord } = opaque.client.finishRegistration({
        clientRegistrationState,
        registrationResponse,
        password,
    });

    return {
        userIdentifier: userIdentifier,
        registrationRecord: registrationRecord
    }
}