namespace Proyecto_HobbyHub.Models;

public class Seguimiento
{
    public int IdSeguidor { get; set; }
    public int IdSeguido { get; set; }
    public bool Aceptado { get; set; }
    public DateTime FechaUtc { get; set; }
}
public class PublicacionGuardada
{
    public int IdUsuario { get; set; }
    public int IdPublicacion { get; set; }
    public DateTime FechaUtc { get; set; }
}
public class PublicacionImagen
{
    public Guid IdImagen { get; set; }
    public int IdPublicacion { get; set; }
    public string Archivo { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public int Orden { get; set; }
}
public class MensajePrivado
{
    public string Estado { get; set; } = "Visible";
    public long IdMensaje { get; set; }
    public int IdRemitente { get; set; }
    public int IdDestinatario { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaUtc { get; set; }
    public DateTime? LeidoUtc { get; set; }
}
public class BloqueoUsuario
{
    public int IdActor { get; set; }
    public int IdBloqueado { get; set; }
    public DateTime FechaUtc { get; set; }
}
public class Notificacion
{
    public long IdNotificacion { get; set; }
    public int IdUsuario { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int? IdRecurso { get; set; }
    public bool Leida { get; set; }
    public DateTime FechaUtc { get; set; }
}
public partial class Usuario
{
    public DateTime? SuspensionHastaUtc { get; set; }
    public string Biografia { get; set; } = string.Empty;
    public string Intereses { get; set; } = string.Empty;
    public bool Privado { get; set; }
    public bool PermitirMensajes { get; set; } = true;
    public bool RecibirNotificaciones { get; set; } = true;
}
public partial class Comunidade
{
    public bool Privada { get; set; }
    public string Reglas { get; set; } = string.Empty;
}
