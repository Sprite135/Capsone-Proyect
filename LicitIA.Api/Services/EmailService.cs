using System.Net;
using System.Net.Mail;

namespace LicitIA.Api.Services;

public class EmailService
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUser;
    private readonly string _smtpPassword;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public EmailService(IConfiguration configuration)
    {
        _smtpHost = configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
        _smtpPort = int.TryParse(configuration["Email:SmtpPort"], out var port) ? port : 587;
        _smtpUser = configuration["Email:SmtpUser"] ?? string.Empty;
        _smtpPassword = configuration["Email:SmtpPassword"] ?? string.Empty;
        _fromEmail = configuration["Email:FromEmail"] ?? "noreply@licitia.com";
        _fromName = configuration["Email:FromName"] ?? "LicitIA";
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string userName, CancellationToken cancellationToken = default)
    {
        var subject = "Bienvenido a LicitIA - Tu cuenta ha sido creada";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #3b82f6, #1cc8b7); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f8fafc; padding: 30px; border-radius: 0 0 10px 10px; }}
        .button {{ display: inline-block; background: #3b82f6; color: white; padding: 12px 24px; text-decoration: none; border-radius: 25px; margin-top: 20px; }}
        .footer {{ text-align: center; margin-top: 20px; color: #64748b; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>¡Bienvenido a LicitIA!</h1>
        </div>
        <div class='content'>
            <h2>Hola {userName},</h2>
            <p>Tu cuenta ha sido creada exitosamente usando tu correo de Gmail.</p>
            <p>Ahora puedes comenzar a:</p>
            <ul>
                <li>Explorar oportunidades de licitaciones</li>
                <li>Configurar alertas personalizadas</li>
                <li>Recibir recomendaciones inteligentes</li>
            </ul>
            <a href='http://localhost:5153/index.html' class='button'>Ir al Dashboard</a>
        </div>
        <div class='footer'>
            <p>Este es un mensaje automático de LicitIA. No respondas a este correo.</p>
            <p>© 2024 Emdersoft S.A.C. - Todos los derechos reservados.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body, true, cancellationToken);
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default)
    {
        var subject = "Recuperación de contraseña - LicitIA";
        var resetUrl = $"http://localhost:5153/restablecer-contrasena.html?token={resetToken}";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #3b82f6, #1cc8b7); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f8fafc; padding: 30px; border-radius: 0 0 10px 10px; }}
        .button {{ display: inline-block; background: #3b82f6; color: white; padding: 12px 24px; text-decoration: none; border-radius: 25px; margin-top: 20px; }}
        .footer {{ text-align: center; margin-top: 20px; color: #64748b; font-size: 12px; }}
        .token-box {{ background: #f1f5f9; padding: 15px; border-radius: 8px; margin: 20px 0; text-align: center; font-family: monospace; font-size: 16px; letter-spacing: 2px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Recuperación de Contraseña</h1>
        </div>
        <div class='content'>
            <h2>Hola,</h2>
            <p>Recibimos una solicitud para restablecer tu contraseña en LicitIA.</p>
            <p>Tu token de recuperación es:</p>
            <div class='token-box'>{resetToken}</div>
            <p>O haz clic en el siguiente enlace para restablecer tu contraseña:</p>
            <a href='{resetUrl}' class='button'>Restablecer Contraseña</a>
            <p style='margin-top: 20px; color: #64748b; font-size: 14px;'>
                Este enlace expirará en 1 hora. Si no solicitaste este cambio, ignora este correo.
            </p>
        </div>
        <div class='footer'>
            <p>Este es un mensaje automático de LicitIA. No respondas a este correo.</p>
            <p>© 2024 Emdersoft S.A.C. - Todos los derechos reservados.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body, true, cancellationToken);
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_smtpUser) || string.IsNullOrWhiteSpace(_smtpPassword))
        {
            // Log warning but don't throw - email is optional
            Console.WriteLine($"[EmailService] Warning: SMTP credentials not configured. Email to {toEmail} would have been sent.");
            Console.WriteLine($"[EmailService] Subject: {subject}");
            return;
        }

        using var client = new SmtpClient(_smtpHost, _smtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_smtpUser, _smtpPassword)
        };

        var message = new MailMessage
        {
            From = new MailAddress(_fromEmail, _fromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };

        message.To.Add(toEmail);

        await client.SendMailAsync(message, cancellationToken);
    }
}
