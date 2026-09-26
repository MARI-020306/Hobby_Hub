namespace Proyecto_HobbyHub.ViewModels;

public class ComunidadesIndexViewModel
{
    public string? Busqueda { get; set; }

    public bool PuedeCrear { get; set; }

    public bool MostrarFormulario { get; set; }

    public ComunidadFormViewModel Formulario { get; set; } = new();

    public IReadOnlyList<ComunidadCardViewModel> Comunidades { get; set; } = Array.Empty<ComunidadCardViewModel>();
}

public class ComunidadCardViewModel
{
    public int IdComunidad { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public string Categoria { get; set; } = "General";

    public string? ImagenPortada { get; set; }

    public string Creador { get; set; } = string.Empty;

    public int TotalMiembros { get; set; }

    public bool EsCreadorActual { get; set; }

    public bool UsuarioYaUnido { get; set; }
}
