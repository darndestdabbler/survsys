namespace Survsys.Contracts;

/// <summary>
/// A user name and password to sign in with: what <c>POST /api/sign-in</c>
/// takes. It answers with a bearer token for every later request.
/// </summary>
/// <param name="UserName">The user's user name.</param>
/// <param name="Password">The user's password.</param>
public record SignInRequest(string UserName, string Password);
