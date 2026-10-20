export class ServerIdentityMismatchError extends Error {
    constructor() {
        super("Server identity could not be verified.");
        this.name = "ServerIdentityMismatchError";
    }
}