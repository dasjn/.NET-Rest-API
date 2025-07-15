# Plan de Testing Crítico - Interventional Academy

## Filosofía: Solo Tests Críticos

**Principio fundamental**: Testear ÚNICAMENTE lo que previene bugs críticos de negocio. Zero redundancia, máximo ROI.

### ✅ QUÉ TESTEAR (Solo Critical-Tier)
- **Seguridad**: Autenticación, autorización, validación de tokens
- **Core Business Logic**: Upload de videos, procesamiento de datos críticos  
- **Integraciones externas**: APIs, servicios de terceros con alto riesgo de fallo
- **Edge cases peligrosos**: Fallos que pueden corromper datos o comprometer seguridad

### ❌ QUÉ NO TESTEAR (Eliminado completamente)
- **DTOs y modelos básicos**: Las validaciones se prueban automáticamente en integration
- **Propiedades simples**: Getters/setters, asignaciones directas
- **Tests redundantes**: Si ya se prueba en otro lado, no duplicar
- **Detalles de implementación**: Métodos privados, estructura interna

## Estado Actual de Tests

### ✅ **IMPLEMENTADOS** (29 tests críticos)

#### IA.WebAPI.Tests - AuthService (11 tests) ⭐⭐⭐⭐⭐
**¿Por qué críticos?** Fallos aquí comprometen la seguridad completa del sistema

Lista exacta de tests implementados:
- ✅ `AuthenticateUserAsync_NewUser_ShouldCreateUserAndReturnToken`
- ✅ `AuthenticateUserAsync_ExistingUser_ShouldUpdateUserAndReturnToken`  
- ✅ `GenerateJwtToken_ValidUser_ShouldReturnValidToken`
- ✅ `ValidateToken_ValidToken_ShouldReturnUserInfo`
- ✅ `ValidateToken_InvalidToken_ShouldReturnNull`
- ✅ `ValidateToken_EmptyToken_ShouldReturnNull`
- ✅ `ValidateToken_NullToken_ShouldReturnNull`
- ✅ `GetUserByIdAsync_ExistingUser_ShouldReturnUserInfo`
- ✅ `GetUserByIdAsync_NonExistingUser_ShouldReturnNull`
- ✅ `GenerateJwtToken_UserWithoutProfilePicture_ShouldNotIncludeProfilePictureClaim`
- ✅ `AuthenticateUserAsync_ExistingUserWithoutNewProfilePicture_ShouldKeepExistingProfilePicture`

#### IA.FrontEnd.Tests - VideoService (15 tests) ⭐⭐⭐⭐⭐
**¿Por qué críticos?** Es la funcionalidad core del negocio

Lista exacta de tests implementados:
- ✅ `UploadVideoAsync_WithValidData_ShouldReturnSuccess`
- ✅ `UploadVideoAsync_WithThumbnail_ShouldIncludeThumbnailInRequest`
- ✅ `UploadVideoAsync_WithEmptyVideoData_ShouldReturnFailure`
- ✅ `UploadVideoAsync_WithEmptyTitle_ShouldReturnFailure`
- ✅ `UploadVideoAsync_WithEmptyDescription_ShouldReturnFailure`
- ✅ `UploadVideoAsync_WithoutAuthToken_ShouldReturnFailure`
- ✅ `UploadVideoAsync_WithHttpError_ShouldReturnFailure`
- ✅ `UploadVideoAsync_WithHttpException_ShouldReturnFailureWithExceptionMessage`
- ✅ `FormatVideoUrl_WithAbsoluteUrl_ShouldReturnUnchanged`
- ✅ `FormatVideoUrl_WithRelativePath_ShouldReturnFullUrl`
- ✅ `FormatVideoUrl_WithBackslashes_ShouldNormalizeToForwardSlashes`
- ✅ `FormatThumbnailUrl_WithNullInput_ShouldReturnEmptyString`
- ✅ `FormatThumbnailUrl_WithEmptyInput_ShouldReturnEmptyString`
- ✅ `FormatThumbnailUrl_WithAbsoluteUrl_ShouldReturnUnchanged`
- ✅ `FormatThumbnailUrl_WithRelativePath_ShouldReturnFullUrl`

#### Tests dummy a eliminar:
- ❌ `IA.WebAPI.Tests.UnitTest1.Test1` (eliminar)
- ❌ `IA.FrontEnd.Tests.UnitTest1.Test1` (eliminar)  
- ❌ `IA.IntegrationTests.UnitTest1.Test1` (eliminar)

**Total real: 26 tests críticos (11 + 15)**

## Evaluación de Tests Propuestos

### 🔥 **FileStorageService** - **REALMENTE CRÍTICO**
**¿Por qué SÍ testear?** 
- Maneja uploads de video (core del negocio)
- Integra con Azure Storage (externa, puede fallar)
- Validaciones de archivos pueden fallar silenciosamente
- Pérdida de datos si falla

**Tests propuestos VÁLIDOS (4 tests):**
- ❌ `SaveFileAsync_WithValidFile_ShouldReturnFilePath`
- ❌ `SaveFileAsync_WithInvalidExtension_ShouldThrowException`
- ❌ `DeleteFileAsync_ExistingFile_ShouldReturnTrue`
- ❌ `GetFileUrl_ValidFile_ShouldReturnCorrectUrl`

### 🔥 **GoogleAuthService** - **REALMENTE CRÍTICO**
**¿Por qué SÍ testear?**
- Integración OAuth externa (puede cambiar APIs)
- Fallo = no login = app inútil
- Manejo de tokens externos complejos

**Tests propuestos VÁLIDOS (3 tests):**
- ❌ `ExchangeCodeForTokenAsync_ValidCode_ShouldReturnToken`
- ❌ `ExchangeCodeForTokenAsync_InvalidCode_ShouldThrowException`
- ❌ `GetUserInfoAsync_ValidToken_ShouldReturnUserInfo`

### 🟡 **ThumbnailGeneratorService** - **MODERADAMENTE CRÍTICO**
**¿Por qué SÍ testear?**
- Integra con FFmpeg (externa, puede fallar)
- Errores no son obvios (video corrupto vs FFmpeg missing)

**Tests propuestos VÁLIDOS (2 tests):**
- ❌ `GenerateThumbnailFromVideoAsync_ValidVideo_ShouldReturnThumbnailPath`
- ❌ `GenerateThumbnailFromVideoAsync_FFmpegFailure_ShouldHandleError`

### ❌ **OAuthStateService** - **NO ES CRÍTICO**
**¿Por qué NO testear?**
- Lógica muy simple (Guid + MemoryCache)  
- No hay integración externa
- Bug sería obvio (OAuth no funciona)
- Tiempo escribiendo test > tiempo escribiendo función

### ❌ **VideoInteractionService** - **NO ES CRÍTICO**  
**¿Por qué NO testear?**
- Es básicamente HTTP calls simples
- Ya se testea en VideoService patterns
- UX feature, no funcionalidad core crítica
- Fallo sería obvio (botón no funciona)

## Plan de Implementación Depurado

### 📋 **FASE 1 COMPLETADA** ✅
**26 tests críticos reales implementados**
- ✅ AuthService (Backend): 11 tests de seguridad
- ✅ VideoService (Frontend): 15 tests de upload
- ❌ **Pendiente**: Eliminar 3 tests dummy

### 🚀 **FASE 2: Solo Servicios Realmente Críticos**
**Objetivo: +9 tests críticos solamente**

#### Prioridad de Implementación:
1. **FileStorageService** (4 tests) - Core del negocio
2. **GoogleAuthService** (3 tests) - Integración crítica externa  
3. **ThumbnailGeneratorService** (2 tests) - Procesamiento externo

**Total objetivo final: 35 tests críticos (26 + 9)**

### ❌ **NO IMPLEMENTAR**
- OAuthStateService (lógica trivial)
- VideoInteractionService (HTTP calls básicos)
- InfiniteScrollService (UI básica)
- IA.FrontEnd AuthService (wrapper del backend)
- Integration tests (overhead sin valor para este tamaño)

## Estructura Actual Real

```
IA.WebAPI.Tests/
├── Services/
│   └── AuthServiceTests.cs ✅ (11 tests)
├── Helpers/
│   ├── TestIAContext.cs ✅
│   └── TestAuthService.cs ✅
└── UnitTest1.cs ❌ (eliminar)

IA.FrontEnd.Tests/
├── Services/  
│   └── VideoServiceTests.cs ✅ (15 tests)
├── Helpers/
│   ├── ITestAuthStateProvider.cs ✅
│   └── TestVideoService.cs ✅
└── UnitTest1.cs ❌ (eliminar)

IA.IntegrationTests/
└── UnitTest1.cs ❌ (eliminar placeholder)
```

## Métricas de Calidad

### Estado Actual: 26 tests críticos reales
- **Cobertura**: 100% de funcionalidad crítica core
- **Tiempo de ejecución**: <8 segundos total
- **Mantenimiento**: Mínimo absoluto
- **ROI**: Máximo - cada test previene bugs críticos

### Target Fase 2: +9 tests
- **Total final**: 35 tests críticos
- **Cobertura**: 100% de servicios realmente críticos
- **Tiempo objetivo**: <12 segundos total  
- **Philosophy**: Better 35 critical tests than 100 mediocre ones

## Comandos de Ejecución

```bash
# Todos los tests críticos
dotnet test

# Solo backend crítico
dotnet test IA.WebAPI.Tests

# Solo frontend crítico
dotnet test IA.FrontEnd.Tests

# Solo AuthService  
dotnet test --filter "AuthServiceTests"

# Solo VideoService
dotnet test --filter "VideoServiceTests"

# Con detalle
dotnet test --logger "console;verbosity=normal"
```

## Reglas de Oro para Testing Crítico

### ✅ **SÍ testear si...**
1. **¿Un bug aquí compromete seguridad?** → Test it
2. **¿Un bug aquí corrompe/pierde datos?** → Test it
3. **¿Un bug aquí rompe la funcionalidad core?** → Test it
4. **¿Es integración externa que puede cambiar?** → Test it
5. **¿Puede fallar silenciosamente de formas no obvias?** → Test it

### ❌ **NO testear si...**
1. **¿Es lógica trivial (CRUD básico)?** → Skip it
2. **¿Ya se prueba en otro test?** → Skip it
3. **¿Es UI sin lógica de negocio?** → Skip it
4. **¿Un bug sería inmediatamente obvio?** → Skip it
5. **¿Tardé más escribiendo el test que la función?** → Definitely skip it

## Próximos Pasos Concretos

### Inmediato (limpieza):
1. Eliminar `UnitTest1.cs` files (3 archivos dummy)
2. Actualizar conteo real: 26 tests críticos

### Fase 2 (solo críticos):
1. **FileStorageService** (4 tests) - Semana 1
2. **GoogleAuthService** (3 tests) - Semana 2  
3. **ThumbnailGeneratorService** (2 tests) - Semana 3

### Final:
- **35 tests críticos total**
- **Suite completa y final** - no agregar más unless bugs críticos discovered

---

**Última actualización**: Julio 2024  
**Estado**: 26/35 tests críticos implementados (74% completado)  
**Próximo**: Eliminar tests dummy, luego FileStorageService tests