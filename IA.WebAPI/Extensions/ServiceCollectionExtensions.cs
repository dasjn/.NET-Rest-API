using IA.WebAPI.Filters;
using IA.WebAPI.Models;
using IA.WebAPI.Options;
using IA.WebAPI.Services;
using IA.WebAPI.Swagger;
using Microsoft.AspNetCore.Authentication.Cookies;
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
            services.Configure<AzureStorageOptions>(configuration.GetSection(AzureStorageOptions.SectionName));

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

            services.AddHttpClient("ImageProxy").ConfigurePrimaryHttpMessageHandler(() =>
            {
                return new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    MaxAutomaticRedirections = 5,
                    UseCookies = false
                };
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

            services.AddScoped<IThumbnailGeneratorService, ThumbnailGeneratorService>();

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

                // ELIMINADO: c.OperationFilter<SwaggerFileOperationFilter>();
                // Filtro de esquema personalizado para mejorar la documentación
                c.SchemaFilter<SwaggerSchemaFilter>();
            });

            return services;
        }

        public static IServiceCollection AddAppAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            // Obtener y validar opciones de autenticación
            var authOptions = configuration.GetAuthOptions();

            services.AddAuthentication(options =>
            {
                // Establecer JWT como esquema predeterminado
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
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

                // Establecer un manejador de eventos para debugging
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }
                };
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
                    ClockSkew = TimeSpan.FromMinutes(2),
                    RequireExpirationTime = true,
                    RequireSignedTokens = true
                };

                // Configuración de eventos para debugging
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        return Task.CompletedTask;
                    }
                };
            });

            return services;
        }
    }
}