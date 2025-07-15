# Interventional Academy

Plataforma de aprendizaje por video construida con Blazor WebAssembly y .NET 8, desplegada en Azure.

## 🚀 Aplicación en Vivo

- **Frontend**: https://interventional-academy-frontend.azurewebsites.net
- **Backend API**: https://interventional-academy-api.azurewebsites.net  
- **Documentación API**: https://interventional-academy-api.azurewebsites.net/swagger

## 📦 Deploy Rápido

### Prerrequisitos
- Azure CLI instalado y autenticado
- .NET 8 SDK
- PowerShell

### Deploy Backend
```powershell
cd backend
.\deploy-backend.ps1
```

### Deploy Frontend  
```powershell
cd frontend
.\deploy-frontend.ps1
```

## ⚙️ Configuración Azure

### App Settings (Backend)
```
ConnectionStrings__DefaultConnection=Server=tcp:interventional-academy-sql.database.windows.net,1433;Initial Catalog=interventional-academy-db;User ID=iaadmin;Password=301064Cath;Encrypt=True;
ConnectionStrings__AzureStorage=DefaultEndpointsProtocol=https;AccountName=iaacademystorage2025;AccountKey=[KEY];EndpointSuffix=core.windows.net
AzureStorage__UseAzureStorage=true
Authentication__Jwt__Key=[JWT_SECRET_KEY]
Authentication__Google__ClientSecret=[GOOGLE_CLIENT_SECRET]
```

### App Settings (Frontend)
```
ApiBaseUrl=https://interventional-academy-api.azurewebsites.net
```

## 🏗️ Infraestructura

| Recurso | Nombre | Tipo |
|---------|--------|------|
| Grupo de Recursos | `rg-interventional-academy` | West Europe |
| SQL Server | `interventional-academy-sql` | Serverless |
| Base de Datos | `interventional-academy-db` | Auto-pause 60min |
| Cuenta de Storage | `iaacademystorage2025` | Standard LRS |
| App Backend | `interventional-academy-api` | F1 Free |
| App Frontend | `interventional-academy-frontend` | F1 Free |

## 🔧 Resolución de Problemas

### La App No Inicia (500.30)
```bash
# Revisar logs de la aplicación
az webapp log tail --name interventional-academy-api --resource-group rg-interventional-academy
```

### Problemas de Conexión Base de Datos
```bash
# Probar conexión
sqlcmd -S interventional-academy-sql.database.windows.net -U iaadmin -P 301064Cath -d interventional-academy-db -Q "SELECT 1"
```

### Problemas de Acceso Storage
```bash
# Verificar contenedores
az storage container list --account-name iaacademystorage2025
```

### Problemas CORS
Verificar que la URL del frontend esté en la lista blanca de configuración CORS del backend.

## 🛠️ Desarrollo Local

### Backend
```bash
cd backend
dotnet restore
dotnet run
```

### Frontend
```bash
cd frontend
dotnet restore
dotnet run
```

**Nota**: Actualizar `ApiBaseUrl` en frontend a `https://localhost:7113` para desarrollo local.

## 📊 Limitaciones Actuales

- **Tamaño de Archivo**: Hasta 5GB por video
- **Usuarios Concurrentes**: Limitado en tier gratuito
- **Storage**: ~$1-3/mes de costo

## 🔑 Credenciales Clave

- **SQL Admin**: `iaadmin` / `301064Cath`
- **Grupo de Recursos**: `rg-interventional-academy` 
- **Suscripción**: Azure subscription 1

## 📋 Funcionalidades

- ✅ Autenticación Google OAuth
- ✅ Subida de videos con recorte del lado cliente (FFmpeg.js)
- ✅ Generación automática de thumbnails
- ✅ Azure Blob Storage con tokens SAS
- ✅ Frontend responsivo Blazor WebAssembly
- ✅ API RESTful con Entity Framework

---

**Estado**: Listo para Producción ✅  
**Último Deploy**: 01/07/2025