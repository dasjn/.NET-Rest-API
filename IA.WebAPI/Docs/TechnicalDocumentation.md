# Documentación Técnica - Interventional Academy Web API

## 1. Estructura del Proyecto

### 1.1 Árbol de Directorios

```
IA.WebAPI/
│
├── Controllers/           # Controladores de API
│   ├── AuthController.cs
│   └── VideosController.cs
│
├── Extensions/            # Métodos de extensión para configuración
│   ├── ApplicationBuilderExtensions.cs
│   ├── ConfigurationExtensions.cs
│   ├── LoggingExtensions.cs
│   └── ServiceCollectionExtensions.cs
│
├── Filters/               # Filtros personalizados de ASP.NET
│   ├── RequestLoggingFilter.cs
│   └── ValidationActionFilter.cs
│
├── Logging/               # Configuración de logging personalizado
│   └── DetailedLoggerProvider.cs
│
├── Middleware/            # Middleware personalizado
│   ├── ExceptionHandlingMiddleware.cs
│   └── SecurityHeadersMiddleware.cs
│
├── Migrations/            # Migraciones de Entity Framework
│   ├── 20250131092430_InitialCreate.cs
│   └── IAContextModelSnapshot.cs
│
├── Models/                # Modelos de datos
│   ├── Auth/
│   │   └── AuthModels.cs
│   ├── IAContext.cs
│   └── Video.cs
│
├── Options/               # Clases de configuración
│   ├── AuthOptions.cs
│   ├── GoogleAuthOptions.cs
│   └── JwtOptions.cs
│
├── Properties/            # Configuraciones de lanzamiento
│   └── launchSettings.json
│
├── Security/              # Componentes de seguridad
│   └── SecurityHeadersMiddleware.cs
│
├── Services/              # Servicios de negocio
│   ├── AuthService.cs
│   ├── FileStorageService.cs
│   ├── GoogleAuthService.cs
│   └── OAuthStateService.cs
│
├── Swagger/               # Configuración de Swagger
│   └── SwaggerFileOperationFilter.cs
│
├── Uploads/               # Directorio para archivos subidos
│
├── appsettings.Development.json   # Configuraciones para desarrollo
├── IA.WebAPI.csproj               # Archivo de proyecto
└── Program.cs                     # Punto de entrada de la aplicación
```

## 2. Gestión de Configuraciones

### 2.1 Configuración de Entorno

#### Archivo `appsettings.json`
Debido a consideraciones de seguridad, el archivo `appsettings.json` no se incluye en el repositorio. Se recomienda configurar las variables de entorno o un archivo de configuración local.

#### Variables de Entorno Recomendadas
```bash
# Configuraciones de Autenticación
Authentication__Jwt__Key=clave_secreta_jwt_muy_segura
Authentication__Jwt__Issuer=interventional_academy
Authentication__Jwt__Audience=frontend_application

# Configuraciones de Google OAuth
Authentication__Google__ClientId=tu_client_id_de_google
Authentication__Google__ClientSecret=tu_client_secret_de_google

# Cadena de Conexión a Base de Datos
ConnectionStrings__DefaultConnection=Server=localhost;Database=InterventionalAcademy;User Id=usuario;Password=contraseña;
```

### 2.2 Estrategia de Configuración
- Uso de `IConfiguration` para manejar configuraciones
- Soporte para múltiples entornos (desarrollo, producción)
- Separación de configuraciones sensibles

## 3. Componentes Principales

### 3.1 Controladores

#### AuthController
- Gestiona el flujo de autenticación con Google
- Endpoints:
  - `GET /api/auth/google-login`: Inicia autenticación con Google
  - `GET /api/auth/google-callback`: Procesa respuesta de Google
  - `GET /api/auth/user-info`: Obtiene información del usuario

#### VideosController
- Gestiona operaciones CRUD de videos
- Endpoints:
  - `GET /api/videos`: Listar videos
  - `GET /api/videos/{id}`: Obtener video específico
  - `POST /api/videos`: Subir nuevo video
  - `DELETE /api/videos/{id}`: Eliminar video

### 3.2 Servicios Clave

#### AuthService
- Generación de tokens JWT
- Validación de tokens
- Gestión de sesiones de usuario

#### GoogleAuthService
- Manejo de autenticación con Google OAuth
- Intercambio de códigos por tokens
- Obtención de información de usuario

#### FileStorageService
- Gestión segura de almacenamiento de archivos
- Validación de archivos
- Límites de tamaño
- Prevención de archivos maliciosos

## 4. Seguridad

### 4.1 Capas de Seguridad
- Autenticación OAuth con Google
- Tokens JWT
- Middleware de seguridad
- Validación de archivos
- Políticas de CORS
- Validación de estado OAuth

### 4.2 Ejemplo de Configuración de Seguridad
```csharp
services.AddAuthentication()
    .AddCookie()
    .AddJwtBearer(options => 
    {
        // Configuraciones de validación de token
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true
        };
    });
```

## 5. Desarrollo y Despliegue

### 5.1 Requisitos
- .NET 8.0 SDK
- SQL Server
- Visual Studio 2022 o VS Code

### 5.2 Pasos de Instalación
1. Clonar repositorio
2. Configurar variables de entorno
3. Restaurar paquetes NuGet
   ```bash
   dotnet restore
   ```
4. Aplicar migraciones
   ```bash
   dotnet ef database update
   ```
5. Ejecutar la aplicación
   ```bash
   dotnet run
   ```

## 6. Consideraciones Importantes

### 6.1 Mejores Prácticas
- Usar inyección de dependencias
- Validar todas las entradas
- Implementar logging comprehensivo
- Mantener secretos fuera del código fuente

### 6.2 Troubleshooting
- Verificar logs en `logs/`
- Validar configuraciones de OAuth
- Comprobar conexiones de base de datos
- Revisar permisos de archivos

## 7. Extensibilidad

### 7.1 Puntos de Extensión
- Añadir nuevos proveedores de autenticación
- Implementar servicios adicionales
- Extender modelos de datos
- Personalizar middleware de seguridad

## 8. Aspectos Adicionales no Cubiertos

### 8.1 Pruebas
Actualmente, el proyecto no incluye una estructura de pruebas. Se recomienda implementar:
- Pruebas unitarias para servicios
- Pruebas de integración
- Pruebas de controladores
- Cobertura de código

Estructura de pruebas sugerida:
```
tests/
├── IA.WebAPI.UnitTests/
│   ├── Services/
│   ├── Controllers/
│   └── Helpers/
└── IA.WebAPI.IntegrationTests/
    ├── Authentication/
    └── FileStorage/
```

### 8.2 CI/CD
Configuraciones pendientes:
- Pipeline de GitHub Actions o Azure DevOps
- Scripts de despliegue automatizado
- Validación de código
- Generación de artefactos

Ejemplo de workflow de GitHub Actions:
```yaml
name: CI/CD Pipeline
on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    - name: Setup .NET
      uses: actions/setup-dotnet@v2
      with:
        dotnet-version: 8.0.x
    - name: Restore dependencies
      run: dotnet restore
    - name: Build
      run: dotnet build --no-restore
    - name: Test
      run: dotnet test --no-build --verbosity normal
```

### 8.3 Mejoras de Seguridad Pendientes
- Implementar autenticación de dos factores
- Agregar políticas de contraseña
- Integrar escaneo de vulnerabilidades
- Configurar OWASP ZAP para pruebas de seguridad

### 8.4 Consideraciones de Rendimiento
- Implementar caché distribuida
- Configurar compresión de respuestas
- Optimizar consultas de base de datos
- Añadir monitoreo de rendimiento
- Configurar Application Insights

### 8.5 Documentación Técnica Pendiente
- Diagrama de arquitectura detallado
- Documentación de API con Swagger/OpenAPI
- Guía de desarrollo para nuevos miembros
- Registro de cambios (CHANGELOG.md)

### 8.6 Escalabilidad
- Preparar para contenerización con Docker
- Configurar balanceo de carga
- Implementar microservicios
- Diseñar para escalamiento horizontal

## 9. Roadmap de Desarrollo

### 9.1 Corto Plazo
- Implementar pruebas unitarias
- Mejorar documentación
- Configurar CI/CD
- Revisar configuraciones de seguridad

### 9.2 Medio Plazo
- Añadir autenticación de dos factores
- Implementar caché distribuida
- Optimizar rendimiento
- Integrar monitoreo avanzado

### 9.3 Largo Plazo
- Microservicios
- Arquitectura serverless
- Implementación de machine learning
- Integración avanzada de servicios

---

**Nota Final:**
La documentación es un recurso vivo. Mantenla actualizada con cada cambio significativo en la arquitectura o funcionalidad del sistema. El software evoluciona constantemente, y esta guía debe reflejar ese dinamismo.
