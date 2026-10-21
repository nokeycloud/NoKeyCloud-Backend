import {
    generateSalt,
    generateAesKey,
    deriveKey,
    exportKey,
    encryptBytes
} from "./crypto";
import {bytesToBase64} from "@/shared/lib";

export interface RegistrationPayload {
    keySaltBase64: string;
    encryptedMasterKeyBase64: string;
    recoveryEncryptedMasterKeyBase64: string;
    recoveryPasswordPlaintext: string;
    rootFolderEncryptedKeyBase64: string;
}

export async function prepareRegistration(password: string): Promise<RegistrationPayload> {

    const masterKey = await generateAesKey();
    const exportedMasterKey = await exportKey(masterKey);

    const keySalt = generateSalt();
    const kek = await deriveKey(password, keySalt);

    const encryptedMasterKey = await encryptBytes(kek, exportedMasterKey);

    const recoveryBytes = crypto.getRandomValues(new Uint8Array(32));
    const recoveryPasswordPlaintext = bytesToBase64(recoveryBytes);

    const recoveryKek = await deriveKey(recoveryPasswordPlaintext, keySalt);
    const recoveryEncryptedMasterKey = await encryptBytes(recoveryKek, exportedMasterKey);

    const rootFolderKey = await generateAesKey();
    const exportedRootKey = await exportKey(rootFolderKey);
    const rootFolderEncryptedKey = await encryptBytes(masterKey, exportedRootKey);

    return {
        keySaltBase64: bytesToBase64(keySalt),
        encryptedMasterKeyBase64: bytesToBase64(encryptedMasterKey),
        recoveryEncryptedMasterKeyBase64: bytesToBase64(recoveryEncryptedMasterKey),
        rootFolderEncryptedKeyBase64: bytesToBase64(rootFolderEncryptedKey),
        recoveryPasswordPlaintext
    };
}