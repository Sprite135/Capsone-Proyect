# LicitIA.Api

Backend en ASP.NET Core para `login`, `registro` y oportunidades, con scraping de OECE/SEACE. Trabaja con SQL Server.

## Requisitos previos

1. **.NET SDK 8.0** - Descargar desde https://dotnet.microsoft.com/download
2. **SQL Server** - Descargar SQL Server Express (gratis) desde https://www.microsoft.com/sql-server/sql-server-downloads
3. **SQL Server Management Studio (SSMS)** - Para ejecutar scripts de base de datos

## Instalación

### 1. Clonar el repositorio

```bash
git clone https://github.com/Sprite135/Capsone-Proyect.git
cd Capsone-Proyect
```

### 2. Crear la base de datos

Abre SQL Server Management Studio y ejecuta en orden:

1. **Schema principal:** `database/LicitIAAuthDb.sql`
2. **Migración de SeaceIndex:** `database/migration_add_seace_index.sql`
3. **Migración de perfil avanzado:** `database/migration_add_advanced_profile.sql`

Esto creará la base de datos `LicitIAAuthDb` con todas las tablas necesarias.

### 3. Configurar la conexión

Revisa el archivo `appsettings.json`. La conexión por defecto es:

```json
"Database": {
  "ConnectionString": "Server=localhost;Database=LicitIAAuthDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
}
```

Si tu instancia de SQL Server usa otro nombre (por ejemplo `localhost\\SQLEXPRESS`), ajusta el `Server` en el connection string.

### 4. Ejecutar la API

Desde la carpeta raíz del proyecto:

```powershell
cd LicitIA.Api
dotnet restore
dotnet run
```

La API quedará disponible en `http://localhost:5153`.

### 5. Importar oportunidades desde OECE

Una vez la API esté corriendo, ejecuta:

```bash
Invoke-WebRequest -Uri "http://localhost:5153/api/oece/refresh?maxResults=45" -Method POST
```

Esto descargará 45 oportunidades desde la API de OECE y las guardará en la base de datos.

## Endpoints disponibles

### Autenticación
- `POST /api/auth/register` - Registrar usuario
- `POST /api/auth/login` - Iniciar sesión
- `POST /api/auth/logout` - Cerrar sesión

### Oportunidades
- `GET /api/opportunities` - Listar todas las oportunidades
- `GET /api/opportunities/{id}` - Obtener una oportunidad por ID
- `POST /api/opportunities/{id}/favorite` - Marcar como favorito
- `DELETE /api/opportunities/{id}/favorite` - Eliminar favorito

### Scraping OECE/SEACE
- `POST /api/oece/refresh?maxResults=45` - Importar oportunidades desde OECE
- `GET /api/oece/download` - Descargar datos de OECE
- `POST /api/oece/start-auto-update` - Iniciar actualización automática

### Perfil de usuario
- `GET /api/profile` - Obtener perfil del usuario
- `POST /api/profile` - Crear/actualizar perfil
- `PUT /api/profile/{id}` - Actualizar perfil por ID

### Otros
- `GET /api/health` - Verificar estado de la API

## Frontend

El frontend se sirve automáticamente desde la carpeta raíz del proyecto. Abre `http://localhost:5153` en tu navegador para ver:

- `index.html` - Página principal
- `oportunidades.html` - Lista de oportunidades con filtros
- `seguimiento.html` - Tabla de seguimiento con paginación
