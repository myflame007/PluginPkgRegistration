using Microsoft.Identity.Client;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace Dataverse.PluginRegistration;

/// <summary>
/// Handles MSAL-based interactive authentication with a custom browser success page.
/// Uses PublicClientApplication directly so we can control the post-login HTML.
/// </summary>
public static class DataverseAuth
{
    private const string SuccessHtml = """
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8" />
            <title>Login erfolgreich</title>
            <style>
                body {
                    font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
                    display: flex;
                    justify-content: center;
                    align-items: center;
                    min-height: 100vh;
                    margin: 0;
                    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                    color: #fff;
                }
                .card {
                    background: rgba(255,255,255,0.15);
                    backdrop-filter: blur(10px);
                    border-radius: 16px;
                    padding: 48px;
                    text-align: center;
                    max-width: 480px;
                    box-shadow: 0 8px 32px rgba(0,0,0,0.2);
                }
                .icon { font-size: 64px; margin-bottom: 16px; }
                h1 { margin: 0 0 8px 0; font-size: 24px; font-weight: 600; }
                p { margin: 8px 0; opacity: 0.9; font-size: 15px; line-height: 1.5; }
                .hint { margin-top: 24px; opacity: 0.7; font-size: 13px; }
                a { color: #ffd700; text-decoration: none; }
                a:hover { text-decoration: underline; }
                .coffee { margin-top: 20px; font-size: 14px; }
            </style>
        </head>
        <body>
            <div class="card">
                <div class="icon">✅</div>
                <h1>Authentifizierung erfolgreich!</h1>
                <p>Du bist jetzt verbunden.<br/>Das Plugin-Deployment läuft im Terminal weiter.</p>
                <p class="hint">Du kannst diesen Tab jetzt schließen.</p>
                <p class="coffee">☕ <a href="https://buymeacoffee.com/community.dataverse" target="_blank">Buy me a coffee</a></p>
            </div>
        </body>
        </html>
        """;

    private const string ErrorHtml = """
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8" />
            <title>Login fehlgeschlagen</title>
            <style>
                body {
                    font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
                    display: flex;
                    justify-content: center;
                    align-items: center;
                    min-height: 100vh;
                    margin: 0;
                    background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
                    color: #fff;
                }
                .card {
                    background: rgba(255,255,255,0.15);
                    backdrop-filter: blur(10px);
                    border-radius: 16px;
                    padding: 48px;
                    text-align: center;
                    max-width: 480px;
                    box-shadow: 0 8px 32px rgba(0,0,0,0.2);
                }
                .icon { font-size: 64px; margin-bottom: 16px; }
                h1 { margin: 0 0 8px 0; font-size: 24px; font-weight: 600; }
                p { margin: 8px 0; opacity: 0.9; font-size: 15px; }
            </style>
        </head>
        <body>
            <div class="card">
                <div class="icon">❌</div>
                <h1>Authentifizierung fehlgeschlagen</h1>
                <p>Bitte starte den Befehl erneut im Terminal.</p>
            </div>
        </body>
        </html>
        """;

    /// <summary>
    /// Connects to Dataverse using interactive MSAL auth with a custom browser page.
    /// Falls back to connection string auth if a full connection string is provided.
    /// </summary>
    public static async Task<ServiceClient> ConnectAsync(
        EnvironmentConfig envConfig, CancellationToken ct = default)
    {
        // If a raw connection string is configured, use it directly (no custom UI possible)
        if (!string.IsNullOrWhiteSpace(envConfig.ConnectionString))
            return new ServiceClient(envConfig.ConnectionString);

        var appId = envConfig.AppId ?? throw new InvalidOperationException("AppId is required.");
        var url = envConfig.Url ?? throw new InvalidOperationException("Url is required.");

        var app = PublicClientApplicationBuilder
            .Create(appId)
            .WithAuthority(AzureCloudInstance.AzurePublic, "organizations")
            .WithRedirectUri("http://localhost")
            .Build();

        var scopes = new[] { $"{url}/.default" };

        // Always interactive login with custom HTML
        var authResult = await app.AcquireTokenInteractive(scopes)
            .WithSystemWebViewOptions(new SystemWebViewOptions
            {
                HtmlMessageSuccess = SuccessHtml,
                HtmlMessageError = ErrorHtml
            })
            .WithPrompt(envConfig.LoginPrompt?.Equals("Always", StringComparison.OrdinalIgnoreCase) == true
                ? Prompt.ForceLogin
                : Prompt.SelectAccount)
            .ExecuteAsync(ct)
            .ConfigureAwait(false);

        // Connect ServiceClient with the acquired token
        var client = new ServiceClient(
            instanceUrl: new Uri(url),
            tokenProviderFunction: async _ => authResult.AccessToken,
            useUniqueInstance: true);

        if (!client.IsReady)
            throw new InvalidOperationException(
                $"ServiceClient not ready: {client.LastError}");

        return client;
    }
}
