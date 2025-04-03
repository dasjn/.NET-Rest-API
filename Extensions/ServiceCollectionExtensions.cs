using IA.WebAPI.Filters;
using IA.WebAPI.Models;
using IA.WebAPI.Options;
using IA.WebAPI.Services;
using IA.WebAPI.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

namespace IA.WebAPI.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configurar controladores con filtro de validación
            services.AddControllers(options =>
            {
                options.Filters.Add<ValidationActionFilter>();
                options.Filters.Add<RequestLoggingFilter>();
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.PropertyNamingPolicy = null;
            });

            services.AddEndpointsApiExplorer();

            // Configuración para archivos grandes
            services.Configure<IISServerOptions>(options =>
            {
                options.MaxRequestBodySize = 5368709120; // 5GB
            });

            services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = 5368709120; // 5GB
            });

            // Registrar servicios personalizados
            services.AddCustomServices();

            // Configurar base de datos
            services.AddDatabaseContext(configuration);

            // Configurar CORS
            services.AddCorsPolicy(configuration);

            // Configurar opciones basadas en appsettings.json
            services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));

            // Configurar autenticación
            services.AddAppAuthentication(configuration);

            // Configurar roles
            services.AddAuthorizationBuilder()
                .AddPolicy("RequireAdminRole", policy =>
                    policy.RequireRole("Admin"))
                .AddPolicy("VerifiedUsers", policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c =>
                            (c.Type == "VerificationStatus" && c.Value == "Verified") ||
                            context.User.IsInRole("Admin"))))
                .AddPolicy("PremiumContent", policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c =>
                            (c.Type == "SubscriptionLevel" && c.Value == "Premium") ||
                            context.User.IsInRole("Admin"))));

            // Configurar Swagger
            services.AddAppSwagger();

            // Configurar HttpClient
            services.AddHttpClient("GoogleAuth", client =>
            {
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            return services;
        }

        private static IServiceCollection AddCustomServices(this IServiceCollection services)
        {
            // Servicios de caché y estado
            services.AddMemoryCache();

            // Servicios de autenticación
            services.AddScoped<IAuthService, AuthService>();
            services.AddSingleton<IOAuthStateService, OAuthStateService>();
            services.AddScoped<IGoogleAuthService, GoogleAuthService>();

            // Agregar servicio de almacenamiento de archivos
            services.AddScoped<IFileStorageService, FileStorageService>();

            return services;
        }

        private static IServiceCollection AddDatabaseContext(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<IAContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
            });

            return services;
        }

        private static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
        {
            var frontendUrl = configuration["Authentication:FrontendBaseUrl"] ?? "https://localhost:44337";
            var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?? new[] { frontendUrl };

            services.AddCors(options =>
            {
                // Política principal para el frontend
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.WithOrigins(frontendUrl)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials()
                          .WithExposedHeaders("Content-Disposition", "X-Pagination");
                });

                // Política más restrictiva para APIs públicas 
                options.AddPolicy("PublicApi", policy =>
                {
                    policy.WithOrigins(allowedOrigins)
                          .WithMethods("GET", "POST", "OPTIONS")
                          .WithHeaders("Content-Type", "Authorization")
                          .SetIsOriginAllowedToAllowWildcardSubdomains();
                });

                // Política para desarrollo local
                if (configuration.GetValue<bool>("IsLocalDevelopment"))
                {
                    options.AddPolicy("Development", policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    });
                }
            });

            return services;
        }

        private static IServiceCollection AddAppSwagger(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Interventional Academy API",
                    Version = "v1",
                    Description = "API para servicios de Interventional Academy"
                });

                // Agregar soporte para autenticación JWT en Swagger
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });

                // Incluir comentarios XML para documentación
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }

                c.OperationFilter<SwaggerFileOperationFilter>(); // Filtro para manejo de archivos
            });

            return services;
        }

        private static IServiceCollection AddAppAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            // Obtener y validar opciones de autenticación
            var authOptions = configuration.GetAuthOptions();

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = "ApplicationCookie";
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddCookie("ApplicationCookie", options =>
            {
                options.Cookie.HttpOnly = true;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(authOptions.Jwt.ExpiryInMinutes);
                options.SlidingExpiration = true;
                options.Cookie.SameSite = SameSiteMode.None;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.IsEssential = true;
                options.Cookie.Name = "IA.AuthCookie";
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = authOptions.Jwt.Issuer,
                    ValidAudience = authOptions.Jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.Jwt.Key)),
                    ClockSkew = TimeSpan.FromMinutes(2), // Reducir a 2 minutos para mayor seguridad
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,

                    // Validar el claim 'nbf' (not before time)
                    RequireAudience = true,
                    ValidateTokenReplay = true  // Prevenir replay attacks
                };

                // Configuración adicional de seguridad
                options.SaveToken = true;
                options.RequireHttpsMetadata = true;  // Requerir HTTPS

                // Eventos mejorados para auditoría
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogWarning(context.Exception, "JWT authentication failed: {Message}", context.Exception.Message);
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogInformation("JWT validated successfully for {Subject}",
                            context.Principal?.Identity?.Name ?? "unknown user");
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogInformation("JWT challenge issued to client");
                        return Task.CompletedTask;
                    }
                };
            });

            return services;
        }
    }
}