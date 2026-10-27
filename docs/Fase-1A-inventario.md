# Inventario técnico — HobbyHub Fase 1A

## Alcance y estado

- Rama de trabajo: `rama/Fernando`; `origin/rama/mari` integrada en el inventario inicial.
- Solución ASP.NET Core MVC, destino `net8.0`, EF Core SQL Server y BCrypt existentes.
- PDF de referencia: 15 páginas válidas; no se modifica.
- Un proyecto web y un proyecto de pruebas offline añadido en esta subfase.
- No se ha consultado ni modificado la base compartida. El esquema real sigue pendiente
  de contraste mediante un inventario de metadatos autorizado.

## Mapa funcional conservado

| Flujo | Entrada | Controlador | Datos |
|---|---|---|---|
| Acceso/registro | `Views/Auth`, `LoginViewModel`, `RegistroViewModel` | `AuthController` | Usuarios, Roles |
| Feed y likes/comentarios | `Views/Home/Index`, ViewModels Social | `HomeController` | Publicaciones, Comunidades, Usuarios, UsuarioImagenes, PublicacionLikes, Comentarios |
| Perfil/edición | `Views/Perfil`, ViewModels Perfil | `PerfilController` | Usuarios, Roles, UsuarioImagenes y relaciones sociales |
| Listado/gestión de comunidades | `Views/Comunidades/Index`, `ComunidadesIndexViewModel`, `ComunidadFormViewModel` | `ComunidadesController` | Comunidades, Usuarios, Roles, MiembrosComunidad |
| Publicaciones y reportes | `Views/Comunidades/Detalle`, `ComunidadDetalleViewModel` y formularios | `ComunidadesController` | Publicaciones, MiembrosComunidad, Reportes |

No se cambian formularios, rutas, reglas de propiedad, hashes ni persistencia CRUD.
Los tres controladores dejan de preparar esquema durante las solicitudes. Una base
incompleta necesita preparación manual antes de usar esos flujos; no habrá reparación
automática al abrir pantallas.

## Contrato actual de persistencia (derivado de EF, no del servidor)

| Tabla | Clave | Restricciones y relaciones relevantes |
|---|---|---|
| Roles | IdRol | Nombre único, varchar(30) |
| Usuarios | IdUsuario | Correo único, RolId obligatorio; Nombre 100, Correo 150, Password 255, Celular 64, Dirección 255 |
| Comunidades | IdComunidad | Nombre único 100, descripción 500, categoría 50, portada 500; creador obligatorio |
| MiembrosComunidad | IdComunidad + IdUsuario | Relaciones con comunidad y usuario; cascadas existentes conservadas |
| Publicaciones | IdPublicacion | Autor y comunidad obligatorios, contenido 2000, imagen 500, estado 20 |
| Comentarios | IdComentario | Autor y publicación obligatorios, contenido 1000, estado 20 |
| PublicacionLikes | IdLike | Par publicación/usuario único |
| UsuarioImagenes | IdImagen | Usuario obligatorio, URL 500, tipo 20 y principal bit |
| Reportes | IdReporte | Denunciante obligatorio; publicación/comentario/usuario reportado opcionales; motivo 255 |

Fechas `datetime` con valores predeterminados `GETDATE()`. Los textos actuales usan
varchar, no nvarchar. Se conserva este contrato; Unicode, privacidad y entidades nuevas
se diseñarán en fases siguientes. Teléfono, correo y dirección siguen como hashes.

## Cambios de seguridad iniciales

1. Eliminados `EnsureSocialSchemaAsync`, `EnsureUsuarioImagenesAsync`,
   `EnsureCommunitySchemaAsync` y todas sus invocaciones.
2. SQL desplazado a scripts explícitos y protegidos por nombre de base de desarrollo.
3. Desarrollo/pruebas usan configuración separada, validación local y ausencia de fallback
   a la conexión compartida. Se limita el tiempo de conexión a cinco segundos.
4. Errores de configuración no incluyen cadenas de conexión ni excepciones del parser.
5. Las pruebas interceptan apertura de conexiones antes de que EF contacte SQL Server.

## Entorno observado

SDK .NET 10.0.401 y runtime ASP.NET Core 8.0.31 disponibles. LocalDB/sqlcmd instalados.
Se detectaron servicios SQL Server activos, pero no se conectó a ellos. La instancia
dedicada HobbyHubDevelopment no quedó verificada como disponible. Instrucciones para
crearla en `Proyecto_HobbyHub/Database/README.md`.

## Límites y siguientes pasos

Fase 1B seguirá con revocación/revalidación de sesiones, rate limiting, logout POST,
validaciones de autenticación y claves persistentes protegidas. El cierre GET, las claves
efímeras y el resto de hallazgos del diagnóstico no se presentan como resueltos aquí.
No se alteran las funciones de eliminación existentes; no se ejecutó ninguna acción de
borrado. La política de conservación de evidencia corresponde a fases posteriores.

El modelo/script se comprueba offline, pero SQL, índices y comportamiento real deberán
validarse en la instancia aislada. No se declara el proyecto listo para producción.

## Verificación ejecutada al cerrar la subfase

- `dotnet restore Proyecto_HobbyHub.sln --verbosity minimal`: correcto.
- `dotnet build Proyecto_HobbyHub.sln --no-restore --verbosity minimal`: correcto,
  cero errores y cero advertencias.
- `dotnet test Proyecto_HobbyHub.sln --no-build --no-restore --verbosity minimal`:
  36 pruebas correctas, cero fallidas y cero omitidas.
- Pruebas de integración MVC: login Razor, antiforgery, rutas protegidas y formularios
  inválidos autenticados de publicación, comentario y edición de perfil.
- Pruebas de aislamiento: sin fallback compartido, destinos rechazados, errores sin
  credenciales y bloqueo explícito de apertura EF antes de cualquier conexión SQL.
- Pruebas de regresión: BCrypt, normalización del correo, validación de registro y
  publicaciones, ausencia de SQL de esquema en código web y cobertura de las nueve
  tablas/columnas por los scripts revisables. No prueban ejecución T-SQL en servidor.
- La fábrica MVC de pruebas utiliza configuración SQL sintética, interceptor de conexión,
  claves solo en memoria y ningún proveedor del registro de eventos de Windows.
- `git diff --check`: sin errores de espacios. Rama conservada como `rama/Fernando`.
- Configuración compartida existente, modelos, vistas, recursos visuales y PDF conservados.
  No hay commits, push, migraciones ni scripts ejecutados sobre una base de datos.
