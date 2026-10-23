using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Bootstrap;
using WhatsAppBot.Api.Infrastructure.Database;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Infrastructure.Security;
using WhatsAppBot.Api.Business.Services;
using WhatsAppBot.Api.Infrastructure;

namespace WhatsAppBot.Api.Infrastructure.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionOptions = configuration.GetSection("ConnectionStrings").Get<DatabaseOptions>() ?? new();
        var connectionString = connectionOptions.WhatsAppBot;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{DatabaseOptions.ConnectionStringName} must be configured.");
        }
        if (!environment.IsDevelopment() &&
            connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SQL Server LocalDB is supported only in the Development environment.");
        }

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection("ConnectionStrings"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.WhatsAppBot),
                $"ConnectionStrings:{DatabaseOptions.ConnectionStringName} is required.")
            .ValidateOnStart();
        services.AddSingleton<ISqlConnectionFactory>(new SqlConnectionFactory(connectionString));
        services.AddOptions<MetaOptions>()
            .Bind(configuration.GetSection(MetaOptions.SectionName))
            .Validate(options => options.RequestTimeoutSeconds is > 0 and <= 120,
                "Meta:RequestTimeoutSeconds must be between 1 and 120.")
            .Validate(options => options.WebhookMaxBodyBytes is >= 1024 and <= 1048576,
                "Meta:WebhookMaxBodyBytes must be between 1024 and 1048576.")
            .Validate(options => options.WorkerBatchSize is >= 1 and <= 100,
                "Meta:WorkerBatchSize must be between 1 and 100.")
            .Validate(options => options.MaxPhoneNumberPages is >= 1 and <= 100,
                "Meta:MaxPhoneNumberPages must be between 1 and 100.")
            .ValidateOnStart();
        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .Validate(options => options.RequestTimeoutSeconds is > 0 and <= 600,
                "OpenAI:RequestTimeoutSeconds must be between 1 and 600.")
            .Validate(options => options.MaxInputCharacters is >= 1 and <= 1_000_000,
                "OpenAI:MaxInputCharacters must be between 1 and 1000000.")
            .Validate(options => options.MaxOutputTokens is >= 1 and <= 100_000,
                "OpenAI:MaxOutputTokens must be between 1 and 100000.")
            .Validate(options => options.MaxAudioBytes is >= 1_024 and <= 100_000_000,
                "OpenAI:MaxAudioBytes must be between 1024 and 100000000.")
            .Validate(options => options.MaxImageBytes is >= 1_024 and <= 100_000_000,
                "OpenAI:MaxImageBytes must be between 1024 and 100000000.")
            .ValidateOnStart();
        services.AddOptions<MediaStorageOptions>()
            .Bind(configuration.GetSection(MediaStorageOptions.SectionName))
            .Validate(options => options.RequestTimeoutSeconds is > 0 and <= 600,
                "MediaStorage:RequestTimeoutSeconds must be between 1 and 600.")
            .Validate(options => options.MaxFileBytes is >= 1_024 and <= 1_000_000_000,
                "MediaStorage:MaxFileBytes must be between 1024 and 1000000000.")
            .Validate(options => options.RetentionDays is >= 1 and <= 3650,
                "MediaStorage:RetentionDays must be between 1 and 3650.")
            .ValidateOnStart();
        services.AddOptions<FfmpegOptions>()
            .Bind(configuration.GetSection(FfmpegOptions.SectionName))
            .Validate(options => options.TimeoutSeconds is > 0 and <= 1800,
                "FFmpeg:TimeoutSeconds must be between 1 and 1800.")
            .Validate(options => options.MaxInputBytes is >= 1_024 and <= 1_000_000_000,
                "FFmpeg:MaxInputBytes must be between 1024 and 1000000000.")
            .Validate(options => options.MaxOutputBytes is >= 1_024 and <= 2_000_000_000,
                "FFmpeg:MaxOutputBytes must be between 1024 and 2000000000.")
            .Validate(options => options.MaxDurationSeconds is > 0 and <= 7200,
                "FFmpeg:MaxDurationSeconds must be between 1 and 7200.")
            .ValidateOnStart();
        services.AddOptions<ApiOptions>().Bind(configuration.GetSection(ApiOptions.SectionName))
            .Validate(options => options.RequestTimeoutSeconds is > 0 and <= 120,
                "Api:RequestTimeoutSeconds must be between 1 and 120.")
            .Validate(options => options.MaxRequestBodyBytes is >= 1024 and <= 104857600,
                "Api:MaxRequestBodyBytes must be between 1024 and 104857600.")
            .Validate(options => options.CompletionFailureStateTimeoutSeconds is > 0 and <= 60,
                "Api:CompletionFailureStateTimeoutSeconds must be between 1 and 60.")
            .ValidateOnStart();
        services.AddOptions<SqlOptions>()
            .Bind(configuration.GetSection(SqlOptions.SectionName))
            .Validate(options => options.CommandTimeoutSeconds is > 0 and <= 600,
                "Sql:CommandTimeoutSeconds must be between 1 and 600.")
            .ValidateOnStart();
        services.AddOptions<AuthenticationOptions>()
            .Bind(configuration.GetSection(AuthenticationOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Authentication:Issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Authentication:Audience is required.")
            .Validate(options => options.AccessTokenMinutes is > 0 and <= 60,
                "Authentication:AccessTokenMinutes must be between 1 and 60.")
            .Validate(options => Encoding.UTF8.GetByteCount(options.SigningKey) >= 32,
                "Authentication:SigningKey must be provided through a secure configuration source and contain at least 32 bytes.")
            .ValidateOnStart();

        var auth = configuration.GetSection(AuthenticationOptions.SectionName).Get<AuthenticationOptions>() ?? new();
        if (Encoding.UTF8.GetByteCount(auth.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Authentication:SigningKey must be configured in User Secrets or the deployment secret store.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = auth.Issuer,
                    ValidateAudience = true,
                    ValidAudience = auth.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        services.AddAuthorization();
        services.AddSingleton<IAccessTokenIssuer, AccessTokenIssuer>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IWhatsAppMessagingPolicy, WhatsAppMessagingPolicy>();
        services.AddSingleton<UserSecretsSecretStore>();
        services.AddSingleton<ISecretStore>(provider => provider.GetRequiredService<UserSecretsSecretStore>());
        services.AddSingleton<ISecretReferenceResolver>(provider =>
            provider.GetRequiredService<UserSecretsSecretStore>());
        services.AddScoped<BootstrapCommand>();
        services.AddScoped<ITenantContext, TenantContext>();
        return services;
    }
}
