namespace OpenClaw.Contracts.Auth.Requests;

public record ResetPasswordRequest(string Token, string NewPassword);
