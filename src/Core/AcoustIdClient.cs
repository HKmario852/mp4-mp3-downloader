namespace Omni.Core;
// Application identifier, not an account/user submission credential.
public static class AcoustIdClient
{
    public static string Resolve(string? custom) => string.IsNullOrWhiteSpace(custom) ? "wdBJF1kUQS" : custom.Trim();
}
