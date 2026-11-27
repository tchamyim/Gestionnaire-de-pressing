using System.Net;
using System.Net.Mail;
using System.Text;
using Pressings.Models;

namespace Pressings.Services;

public sealed class ClientCommunicationService(IConfiguration configuration, AccessRepository repository)
{
    public EmailSettingsInput GetEffectiveSettings()
    {
        var db = repository.GetEmailSettings();
        return new EmailSettingsInput
        {
            Host = !string.IsNullOrWhiteSpace(db.Host) ? db.Host : (configuration["Email:Smtp:Host"] ?? ""),
            Port = db.Port > 0 && !string.IsNullOrWhiteSpace(db.Host) ? db.Port : (configuration.GetValue<int?>("Email:Smtp:Port") ?? 587),
            EnableSsl = !string.IsNullOrWhiteSpace(db.Host) ? db.EnableSsl : configuration.GetValue("Email:Smtp:EnableSsl", true),
            User = !string.IsNullOrWhiteSpace(db.User) ? db.User : (configuration["Email:Smtp:User"] ?? ""),
            Password = !string.IsNullOrWhiteSpace(db.Password) ? db.Password : (configuration["Email:Smtp:Password"] ?? ""),
            FromAddress = !string.IsNullOrWhiteSpace(db.FromAddress) ? db.FromAddress : (configuration["Email:Smtp:FromAddress"] ?? ""),
            FromName = !string.IsNullOrWhiteSpace(db.FromName) ? db.FromName : (configuration["Email:Smtp:FromName"] ?? "Fresh & Care — Pressing"),
            AdditionalEmails = !string.IsNullOrWhiteSpace(db.AdditionalEmails) ? db.AdditionalEmails : (configuration["Email:AdditionalEmails"] ?? "")
        };
    }

    public bool EmailConfigured => IsConfigured(GetEffectiveSettings());

    public bool IsConfigured(EmailSettingsInput settings) => GetConfigurationError(settings) is null;

    public void SaveSettings(EmailSettingsInput input)
    {
        input.Host = input.Host?.Trim() ?? "";
        input.FromAddress = input.FromAddress?.Trim() ?? "";
        input.User = input.User?.Trim() ?? "";
        input.FromName = string.IsNullOrWhiteSpace(input.FromName) ? "Fresh & Care — Pressing" : input.FromName.Trim();
        input.AdditionalEmails = input.AdditionalEmails?.Trim() ?? "";

        var currentSettings = GetEffectiveSettings();
        if (string.IsNullOrWhiteSpace(input.Password) && !input.ClearPassword &&
            string.Equals(input.User, currentSettings.User, StringComparison.OrdinalIgnoreCase))
            input.Password = currentSettings.Password;

        if (GetConfigurationError(input) is string error)
            throw new InvalidOperationException(error);

        foreach (var address in input.AdditionalEmails.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!MailAddress.TryCreate(address, out _))
                throw new InvalidOperationException($"L’adresse de copie « {address} » n’est pas valide.");
        }

        repository.SaveEmailSettings(input);
    }

    public async Task SendEmailAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        var settings = GetEffectiveSettings();
        if (GetConfigurationError(settings) is string error)
            throw new InvalidOperationException(error);

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress.Trim(), settings.FromName, Encoding.UTF8),
            Subject = subject.Trim(),
            Body = body.Trim(),
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };
        foreach (var addr in recipient.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!MailAddress.TryCreate(addr, out var parsed))
                throw new InvalidOperationException($"L’adresse destinataire « {addr} » n’est pas valide.");
            if (!message.To.Any(existing => string.Equals(existing.Address, parsed.Address, StringComparison.OrdinalIgnoreCase)))
                message.To.Add(parsed);
        }
        if (message.To.Count == 0)
            throw new InvalidOperationException("Aucune adresse e-mail destinataire valide.");

        if (!string.IsNullOrWhiteSpace(settings.AdditionalEmails))
        {
            foreach (var bcc in settings.AdditionalEmails.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!MailAddress.TryCreate(bcc, out var parsedBcc))
                    throw new InvalidOperationException($"L’adresse de copie cachée « {bcc} » n’est pas valide.");
                if (!message.To.Any(to => string.Equals(to.Address, parsedBcc.Address, StringComparison.OrdinalIgnoreCase)) &&
                    !message.Bcc.Any(existing => string.Equals(existing.Address, parsedBcc.Address, StringComparison.OrdinalIgnoreCase)))
                    message.Bcc.Add(parsedBcc);
            }
        }

#pragma warning disable SYSLIB0014
        using var smtp = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            Timeout = 20000,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
#pragma warning restore SYSLIB0014
        if (!string.IsNullOrWhiteSpace(settings.User))
            smtp.Credentials = new NetworkCredential(settings.User, NormalizePassword(settings));
        try
        {
            await smtp.SendMailAsync(message, cancellationToken);
        }
        catch (SmtpException ex) when (
            string.Equals(settings.Host?.Trim(), "smtp.gmail.com", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(settings.User))
        {
            throw new InvalidOperationException(
                "Gmail a refusé l’authentification SMTP. Remplacez la clé enregistrée par une clé d’application Google active de 16 caractères.", ex);
        }
    }

    private static string? GetConfigurationError(EmailSettingsInput settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Host))
            return "Renseignez le serveur SMTP de votre fournisseur de messagerie.";
        if (settings.Port is < 1 or > 65535)
            return "Le port SMTP doit être compris entre 1 et 65535 (généralement 587).";
        if (!MailAddress.TryCreate(settings.FromAddress?.Trim(), out _))
            return "Renseignez une adresse e-mail d’envoi valide.";
        if (!string.IsNullOrWhiteSpace(settings.User) && string.IsNullOrWhiteSpace(settings.Password))
            return "Le mot de passe SMTP ou la clé d’application manque pour cet identifiant.";
        if (string.Equals(settings.Host?.Trim(), "smtp.gmail.com", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(settings.User) && NormalizePassword(settings).Length != 16)
            return "Gmail exige une clé d’application Google de 16 caractères (pas le mot de passe habituel). Remplacez le mot de passe SMTP enregistré.";
        return null;
    }

    private static string NormalizePassword(EmailSettingsInput settings) =>
        string.Equals(settings.Host?.Trim(), "smtp.gmail.com", StringComparison.OrdinalIgnoreCase)
            ? new string(settings.Password.Where(character => !char.IsWhiteSpace(character)).ToArray())
            : settings.Password;
}

public static class ClientCommunicationLinks
{
    public static string? WhatsApp(string? phone, string message)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal)) digits = digits[2..];
        if (digits.Length == 9) digits = "237" + digits;
        else if (digits.Length == 10 && digits.StartsWith('0')) digits = "237" + digits[1..];
        if (digits.Length is < 8 or > 15) return null;
        return $"https://wa.me/{digits}?text={Uri.EscapeDataString(message)}";
    }

    public static string Mailto(string? address, string subject, string body)
    {
        var recipient = Uri.EscapeDataString(address?.Trim() ?? "");
        return $"mailto:{recipient}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
    }

    public static string GmailCompose(string? address, string subject, string body) =>
        "https://mail.google.com/mail/?view=cm&fs=1" +
        $"&to={Uri.EscapeDataString(address?.Trim() ?? "")}" +
        $"&su={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
}
