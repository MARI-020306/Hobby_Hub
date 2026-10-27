# Guía de exposición universitaria — HobbyHub

Duración sugerida: 10–12 minutos. Demostrar el sistema local con datos sintéticos y explicar los límites del prototipo; no presentar credenciales, tokens, archivos de configuración ni información personal real.

## Preparación

- Abrir http://127.0.0.1:5088, servidor separado del puerto 5062.
- Tener sesiones independientes para Usuario, Creador, Moderador y Administrador, usando perfiles de Edge o ventanas con contextos separados. No reutilizar la misma cookie para representar varios roles a la vez.
- Las cuentas privilegiadas y de Creador de pruebas están en archivos locales ignorados `.local/visual-privileged-accounts.json` y `.local/visual-browser-accounts.json`. Consultarlos fuera de la proyección, sin mostrar sus contraseñas. Registrar un Usuario sintético adicional si hace falta.
- Preparar una o dos imágenes JPG/PNG de paisajes o contenido sintético, sin personas identificables ni metadatos sensibles; cada archivo debe pesar menos de 5 MB. Las imágenes subidas permanecen fuera de Git.
- Usar un nombre de comunidad único para no confundir registros de pruebas anteriores. No borrar comunidades ajenas ni limpiar la base antes de la exposición.

## Recorrido principal

| Tiempo | Rol y acción | Qué demostrar |
|---|---|---|
| 0–1 min | Presentar el propósito y abrir Inicio/Comunidades. | Red social centrada en hobbies, MVC + EF Core + SQL Server; mismo menú y encabezado entre módulos. |
| 1–2 min | Registrar Usuario o iniciar con una cuenta sintética. | Registro validado, rol ordinario, autenticación real y cierre mediante POST protegido. |
| 2–4 min | Creador: crear comunidad y abrir Administrar. | Nombre, categoría, descripción, reglas y privacidad; control de miembros por propietario. |
| 4–5 min | Usuario: buscar comunidad y unirse. | Ingreso público; para una comunidad privada, solicitar ingreso y aprobarlo desde la sesión del propietario. |
| 5–6 min | Miembro: publicar texto y dos fotografías. | Galería real, autor/comunidad/fecha visibles y persistencia en SQL. |
| 6–7 min | Otro participante: comentar, responder, dar like y guardar. | Interacciones persistentes y contenido en Guardados/Actividad. |
| 7–8 min | Seguir una persona y enviar mensaje. | Seguidores, aprobación en perfil privado, conversación, lectura y notificaciones. |
| 8–9 min | Usuario: reportar publicación de otra cuenta. | Formulario con motivo y seguimiento en Mis reportes. |
| 9–10 min | Moderador: revisar evidencia y resolver. | Estados del reporte, decisión registrada; evitar suspender al presentador durante su propio recorrido. |
| 10–11 min | Administrador: filtrar cuenta sintética y cambiar rol. | Gestión real, cierre de sesión del usuario afectado y nuevo rol al volver a entrar; auditoría filtrable. |
| 11–12 min | Cerrar/reabrir sesión y mostrar móvil. | Persistencia, menú accesible con teclado/Escape y diseño responsive. |

## Demostraciones breves de seguridad

1. Desde Usuario, abrir `/Administracion`: se muestra acceso denegado, aunque se escriba la URL manualmente. No basta con ocultar botones.
2. Abrir una comunidad o perfil privados desde un visitante sin membresía/seguimiento aprobado: su contenido no aparece. Luego aprobar la solicitud y comprobar el acceso correspondiente.
3. Cambiar el rol de una cuenta sintética desde Administración: su sesión anterior deja de servir. Iniciar de nuevo y verificar el permiso actual.
4. Mostrar que el logout es un formulario POST y que no hay operaciones de esquema al navegar. El esquema se prepara con scripts independientes.
5. Si se desea explicar recuperación, indicar que el token es de un solo uso y dura 30 minutos; el buzón es local y cifrado. **No proyectar el token ni simular que se envía correo real.**

El bloqueo de login puede explicarse con las pruebas automatizadas. Si se demuestra en vivo, usar una cuenta nueva creada solo para ello: cinco fallos la bloquean durante 15 minutos. No bloquear las cuentas que se necesitan para continuar la exposición.

## Evidencia y cierre

Mostrar el resumen de pruebas del [informe final](Informe-final-entrega.md), no las credenciales o los informes técnicos sin revisar. Explicar que las pruebas utilizan una base SQL separada y que no se limpiaron datos previos.

Aclarar: desarrollo local académico; mensajes/notificaciones requieren navegación o recarga, no hay correo externo ni despliegue público. Para producción se necesitan mantenimiento de dependencias/runtime, HTTPS, permisos SQL mínimos, respaldos, retención/privacidad, pruebas de carga y revisión de accesibilidad.

## Si ocurre un problema

- Nombre de comunidad duplicado: utilizar otro nombre; no eliminar registros anteriores.
- Acceso denegado: revisar rol, propiedad, membresía o aprobación del seguidor; no quitar controles de autorización.
- Límite de intentos: esperar el tiempo indicado. No desactivar el limitador para la presentación.
- SQL no disponible: revisar únicamente la instancia dedicada y la configuración visual local. No conectar una base compartida como alternativa.
- Error inesperado: conservar el identificador de solicitud y consultar logs locales fuera de la proyección. No publicar trazas o configuraciones.
