using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Data.SqlClient;
using System.Text;
using LicitIA.Api.Configuration;
using LicitIA.Api.Contracts;
using LicitIA.Api.Data;
using LicitIA.Api.Models;
using LicitIA.Api.Security;
using LicitIA.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.WebHost.UseUrls("http://localhost:5153");

builder.Services.AddProblemDetails();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<GoogleAuthOptions>(builder.Configuration.GetSection("GoogleAuth"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddSingleton<AuthRepository>();
builder.Services.AddSingleton<OpportunityRepository>();
builder.Services.AddSingleton<EmailService>();


var app = builder.Build();

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = feature?.Error;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(
            title: "Error interno",
            detail: exception?.Message ?? "Ocurrio un error inesperado.")
            .ExecuteAsync(context);
    });
});

app.UseCors();


// Serve static frontend files - find the project root by looking for home.html
var contentRoot = builder.Environment.ContentRootPath;
var frontendPath = FindFrontendRoot(contentRoot);

if (!string.IsNullOrEmpty(frontendPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frontendPath),
        RequestPath = ""
    });

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new PhysicalFileProvider(frontendPath),
        DefaultFileNames = new List<string> { "home.html", "index.html" }
    });
}

app.MapGet("/", () => Results.Redirect("home.html"));

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "LicitIA API"
}));

app.MapGet("/api/opportunities", async (OpportunityRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        var opportunities = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(opportunities.Select(OpportunityResponse.FromModel));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "No se pudo consultar SQL Server.",
            detail: "Revisa la conexion y ejecuta el script de base de datos.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (Exception)
    {
        return Results.Problem(
            title: "No se pudo obtener las oportunidades.",
            detail: "Ocurrio un error inesperado al consultar la API.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/api/opportunities/{id:int}", async (int id, OpportunityRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        var opportunity = await repository.GetByIdAsync(id, cancellationToken);

        return opportunity is null
            ? Results.NotFound(new { message = "No se encontro la oportunidad solicitada." })
            : Results.Ok(OpportunityResponse.FromModel(opportunity));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "No se pudo consultar SQL Server.",
            detail: "Revisa la conexion y ejecuta el script de base de datos.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (Exception)
    {
        return Results.Problem(
            title: "No se pudo obtener el detalle.",
            detail: "Ocurrio un error inesperado al consultar la API.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/auth/register", async (
    RegisterRequest request,
    AuthRepository repository,
    IOptions<JwtOptions> jwtOptions,
    EmailService emailService,
    CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateRegisterRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    try
    {
        var existingUser = await repository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser is not null)
        {
            return Results.BadRequest(new { message = "Ya existe un usuario registrado con ese correo." });
        }

        var createdUser = await repository.CreateUserAsync(request, cancellationToken);
        var token = GenerateJwtToken(createdUser, jwtOptions.Value);

        // Send welcome email
        try
        {
            await emailService.SendWelcomeEmailAsync(createdUser.Email, createdUser.FullName, cancellationToken);
            Console.WriteLine($"[Register] Welcome email sent to: {createdUser.Email}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Register] Email failed for {createdUser.Email}: {ex.Message}");
        }

        return Results.Ok(new
        {
            message = "Registro completado correctamente. Revisa tu correo para el mensaje de bienvenida.",
            redirectUrl = "index.html",
            token,
            user = new
            {
                fullName = createdUser.FullName,
                email = createdUser.Email,
                role = createdUser.RoleName
            }
        });
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "No se pudo registrar el usuario.",
            detail: "Revisa la conexion a SQL Server y confirma que la base existe.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    AuthRepository repository,
    PasswordService passwordService,
    IOptions<JwtOptions> jwtOptions,
    CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateLoginRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    try
    {
        var user = await repository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !passwordService.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return Results.BadRequest(new { message = "Correo o contrasena incorrectos." });
        }

        var token = GenerateJwtToken(user, jwtOptions.Value, request.RememberMe);

        return Results.Ok(new
        {
            message = $"Bienvenido, {user.FullName}.",
            redirectUrl = "index.html",
            token,
            user = new
            {
                fullName = user.FullName,
                email = user.Email,
                role = user.RoleName
            }
        });
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "No se pudo iniciar sesion.",
            detail: "Revisa la conexion a SQL Server y confirma que la base existe.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// Forgot password endpoint
app.MapPost("/api/auth/forgot-password", async (
    ForgotPasswordRequest request,
    AuthRepository repository,
    EmailService emailService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Email))
    {
        return Results.BadRequest(new { message = "El correo es requerido." });
    }

    try
    {
        var user = await repository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null)
        {
            // Don't reveal if email exists, but return success message
            return Results.Ok(new { message = "Si el correo existe en nuestro sistema, recibirás un enlace de recuperación." });
        }

        // Generate reset token (6-digit code)
        var resetToken = new Random().Next(100000, 999999).ToString();

        // Save token to database
        await repository.SavePasswordResetTokenAsync(user.UserId, resetToken, cancellationToken);

        // Send email with reset token
        try
        {
            await emailService.SendPasswordResetEmailAsync(user.Email, resetToken, cancellationToken);
            Console.WriteLine($"[ForgotPassword] Reset email sent to: {user.Email}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ForgotPassword] Email failed for {user.Email}: {ex.Message}");
        }

        return Results.Ok(new { message = "Si el correo existe en nuestro sistema, recibirás un enlace de recuperación." });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ForgotPassword] Error: {ex.Message}");
        return Results.Problem(
            title: "No se pudo procesar la solicitud.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// Reset password endpoint
app.MapPost("/api/auth/reset-password", async (
    ResetPasswordRequest request,
    AuthRepository repository,
    PasswordService passwordService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
    {
        return Results.BadRequest(new { message = "El token y la nueva contraseña son requeridos." });
    }

    if (request.NewPassword.Length < 8)
    {
        return Results.BadRequest(new { message = "La contraseña debe tener al menos 8 caracteres." });
    }

    try
    {
        // Validate token and get user
        var user = await repository.GetUserByResetTokenAsync(request.Token, cancellationToken);
        if (user is null)
        {
            return Results.BadRequest(new { message = "Token inválido o expirado." });
        }

        // Hash new password
        var (passwordHash, passwordSalt) = passwordService.HashPassword(request.NewPassword);

        // Update password
        await repository.UpdatePasswordAsync(user.UserId, passwordHash, passwordSalt, cancellationToken);

        // Clear reset token
        await repository.ClearPasswordResetTokenAsync(user.UserId, cancellationToken);

        return Results.Ok(new { message = "Contraseña restablecida correctamente." });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ResetPassword] Error: {ex.Message}");
        return Results.Problem(
            title: "No se pudo restablecer la contraseña.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// Complete profile endpoint for new Google users
app.MapPost("/api/auth/complete-profile", async (
    CompleteProfileRequest request,
    IOptions<JwtOptions> jwtOptions,
    AuthRepository repository,
    CancellationToken cancellationToken) =>
{
    try
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.CompanyName))
        {
            return Results.BadRequest(new { message = "El nombre de empresa es requerido." });
        }

        if (string.IsNullOrWhiteSpace(request.Role))
        {
            return Results.BadRequest(new { message = "El rol es requerido." });
        }

        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(new { message = "Token y email son requeridos." });
        }

        // Validate JWT token manually
        var isValidToken = ValidateJwtToken(request.Token, request.Email, jwtOptions.Value);
        if (!isValidToken)
        {
            return Results.Unauthorized();
        }

        // Update user profile
        await repository.UpdateUserProfileAsync(request.Email, request.CompanyName, request.Role, request.Phone, cancellationToken);

        return Results.Ok(new { message = "Perfil actualizado correctamente." });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[CompleteProfile] Error: {ex.Message}");
        return Results.Problem(
            title: "No se pudo actualizar el perfil.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// Google OAuth redirect flow - Step 1: Redirect to Google
app.MapGet("/api/auth/google/login", (IOptions<GoogleAuthOptions> options) =>
{
    var clientId = options.Value.ClientId;
    var redirectUri = options.Value.RedirectUri;

    if (string.IsNullOrWhiteSpace(clientId) || clientId.Contains("YOUR_GOOGLE"))
    {
        return Results.BadRequest(new { message = "Google Client ID no configurado." });
    }

    var state = Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..16];

    var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth" +
        $"?client_id={Uri.EscapeDataString(clientId)}" +
        $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
        $"&response_type=code" +
        $"&scope={Uri.EscapeDataString("openid email profile")}" +
        $"&state={Uri.EscapeDataString(state)}" +
        $"&access_type=offline" +
        $"&prompt=consent";

    return Results.Redirect(authUrl);
});

// Google OAuth redirect flow - Step 2: Handle callback from Google
app.MapGet("/api/auth/google/callback", async (
    [Microsoft.AspNetCore.Mvc.FromQuery] string? code,
    [Microsoft.AspNetCore.Mvc.FromQuery] string? state,
    [Microsoft.AspNetCore.Mvc.FromQuery] string? error,
    IOptions<GoogleAuthOptions> authOptions,
    IOptions<JwtOptions> jwtOptions,
    IHttpClientFactory httpClientFactory,
    AuthRepository repository,
    EmailService emailService,
    CancellationToken cancellationToken) =>
{
    if (!string.IsNullOrWhiteSpace(error))
    {
        return Results.Redirect($"/registro.html?error={Uri.EscapeDataString(error)}");
    }

    if (string.IsNullOrWhiteSpace(code))
    {
        return Results.Redirect("/registro.html?error=no_code");
    }

    try
    {
        var httpClient = httpClientFactory.CreateClient();

        // Exchange code for access token
        var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = authOptions.Value.ClientId,
                ["client_secret"] = authOptions.Value.ClientSecret,
                ["redirect_uri"] = authOptions.Value.RedirectUri,
                ["grant_type"] = "authorization_code"
            }), cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"[GoogleAuth] Token exchange failed: {errorBody}");
            Console.WriteLine($"[GoogleAuth] ClientId: {authOptions.Value.ClientId?.Substring(0, 20)}...");
            Console.WriteLine($"[GoogleAuth] RedirectUri: {authOptions.Value.RedirectUri}");
            var errorSummary = Uri.EscapeDataString(errorBody.Length > 100 ? errorBody[..100] : errorBody);
            return Results.Redirect($"/registro.html?error=token_exchange&details={errorSummary}");
        }

        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken);
        if (tokenData?.AccessToken is null)
        {
            return Results.Redirect("/registro.html?error=no_access_token");
        }

        // Get user info from Google
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenData.AccessToken);
        var userInfoResponse = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo", cancellationToken);

        if (!userInfoResponse.IsSuccessStatusCode)
        {
            return Results.Redirect("/registro.html?error=user_info");
        }

        var googleUser = await userInfoResponse.Content.ReadFromJsonAsync<GoogleUserInfo>(cancellationToken);
        if (googleUser is null || string.IsNullOrWhiteSpace(googleUser.Email))
        {
            return Results.Redirect("/registro.html?error=no_email");
        }

        // Check if user exists
        var existingUser = await repository.GetByEmailAsync(googleUser.Email, cancellationToken);
        AppUser user;
        bool isNewUser = false;

        if (existingUser is not null)
        {
            user = existingUser;
        }
        else
        {
            var registerRequest = new RegisterRequest
            {
                FullName = googleUser.Name ?? $"{googleUser.GivenName} {googleUser.FamilyName}".Trim(),
                Email = googleUser.Email,
                CompanyName = "No especificada",
                Role = "Analista",
                Password = Guid.NewGuid().ToString("N")[..16]
            };

            user = await repository.CreateUserAsync(registerRequest, cancellationToken);
            isNewUser = true;

            try
            {
                Console.WriteLine($"[GoogleAuth] Sending welcome email to: {user.Email}");
                await emailService.SendWelcomeEmailAsync(user.Email, user.FullName, cancellationToken);
                Console.WriteLine($"[GoogleAuth] Welcome email sent successfully to: {user.Email}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GoogleAuth] Email failed for {user.Email}: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[GoogleAuth] Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        var jwtToken = GenerateJwtToken(user, jwtOptions.Value, false);

        // Redirect to frontend with token
        // New users go to complete profile page, existing users go to dashboard
        var redirectPage = isNewUser ? "completar-perfil.html" : "index.html";
        return Results.Redirect($"/{redirectPage}?token={Uri.EscapeDataString(jwtToken)}&name={Uri.EscapeDataString(user.FullName)}&email={Uri.EscapeDataString(user.Email)}&new={isNewUser}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[GoogleAuth] Exception: {ex.Message}");
        return Results.Redirect("/registro.html?error=exception");
    }
});

app.Run();

static Dictionary<string, string[]> ValidateRegisterRequest(RegisterRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.FullName))
    {
        errors["fullName"] = ["Ingresa el nombre completo."];
    }

    if (string.IsNullOrWhiteSpace(request.CompanyName))
    {
        errors["companyName"] = ["Ingresa la empresa."];
    }

    if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
    {
        errors["email"] = ["Ingresa un correo valido."];
    }

    if (string.IsNullOrWhiteSpace(request.Role))
    {
        errors["role"] = ["Selecciona un rol."];
    }

    if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
    {
        errors["password"] = ["La contrasena debe tener al menos 6 caracteres."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateLoginRequest(LoginRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Email))
    {
        errors["email"] = ["Ingresa tu correo."];
    }

    if (string.IsNullOrWhiteSpace(request.Password))
    {
        errors["password"] = ["Ingresa tu contrasena."];
    }

    return errors;
}

static string GenerateJwtToken(AppUser user, JwtOptions options, bool rememberMe = false)
{
    var secret = string.IsNullOrWhiteSpace(options.Key)
        ? "CHANGE_ME_REPLACE_WITH_STRONG_SECRET_1234567890"
        : options.Key;

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    // Use extended expiration if rememberMe is true
    var expires = rememberMe
        ? DateTime.UtcNow.AddDays(options.RememberMeExpirationDays > 0 ? options.RememberMeExpirationDays : 30)
        : DateTime.UtcNow.AddMinutes(options.ExpirationMinutes <= 0 ? 1440 : options.ExpirationMinutes);

    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
        new(JwtRegisteredClaimNames.Email, user.Email),
        new(JwtRegisteredClaimNames.UniqueName, user.FullName),
        new(ClaimTypes.Role, user.RoleName)
    };

    var token = new JwtSecurityToken(
        issuer: string.IsNullOrWhiteSpace(options.Issuer) ? "LicitIA" : options.Issuer,
        audience: string.IsNullOrWhiteSpace(options.Audience) ? "LicitIAUsers" : options.Audience,
        claims: claims,
        expires: expires,
        signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
}

static bool ValidateJwtToken(string token, string expectedEmail, JwtOptions options)
{
    try
    {
        var secret = string.IsNullOrWhiteSpace(options.Key)
            ? "CHANGE_ME_REPLACE_WITH_STRONG_SECRET_1234567890"
            : options.Key;

        Console.WriteLine($"[ValidateJwtToken] Validating token for email: {expectedEmail}");
        Console.WriteLine($"[ValidateJwtToken] Using secret (first 20 chars): {secret[..Math.Min(20, secret.Length)]}...");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = string.IsNullOrWhiteSpace(options.Issuer) ? "LicitIA" : options.Issuer,
            ValidAudience = string.IsNullOrWhiteSpace(options.Audience) ? "LicitIAUsers" : options.Audience,
            IssuerSigningKey = key
        };

        var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
        
        // Log all claims for debugging
        Console.WriteLine($"[ValidateJwtToken] Token received (first 50 chars): {token[..Math.Min(50, token.Length)]}...");
        Console.WriteLine($"[ValidateJwtToken] All claims:");
        foreach (var claim in principal.Claims)
        {
            Console.WriteLine($"  - {claim.Type}: {claim.Value}");
        }
        
        var emailClaim = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        var emailClaim2 = principal.FindFirst(ClaimTypes.Email)?.Value;

        Console.WriteLine($"[ValidateJwtToken] Email from JwtRegisteredClaimNames.Email: {emailClaim}");
        Console.WriteLine($"[ValidateJwtToken] Email from ClaimTypes.Email: {emailClaim2}");
        Console.WriteLine($"[ValidateJwtToken] Expected email: {expectedEmail}");
        
        var finalEmail = emailClaim ?? emailClaim2;
        Console.WriteLine($"[ValidateJwtToken] Match: {finalEmail == expectedEmail}");

        return finalEmail == expectedEmail;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ValidateJwtToken] Exception: {ex.GetType().Name}: {ex.Message}");
        return false;
    }
}

static string? FindFrontendRoot(string startPath)
{
    var current = startPath;
    // Walk up directory tree looking for home.html
    for (int i = 0; i < 6 && !string.IsNullOrEmpty(current); i++)
    {
        if (File.Exists(Path.Combine(current, "home.html")))
        {
            Console.WriteLine($"[StaticFiles] Serving frontend from: {current}");
            return current;
        }
        current = Path.GetDirectoryName(current);
    }

    // Fallback: try project directory (one level up from LicitIA.Api folder)
    var apiProjectDir = Path.GetDirectoryName(startPath);
    if (!string.IsNullOrEmpty(apiProjectDir))
    {
        var projectRoot = Path.GetDirectoryName(apiProjectDir);
        if (!string.IsNullOrEmpty(projectRoot) && File.Exists(Path.Combine(projectRoot, "home.html")))
        {
            Console.WriteLine($"[StaticFiles] Serving frontend from (fallback): {projectRoot}");
            return projectRoot;
        }
    }

    Console.WriteLine($"[StaticFiles] WARNING: Could not find home.html. Searched from: {startPath}");
    return null;
}
