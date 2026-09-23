using System;
using System.Collections.Generic;

namespace Proyecto_HobbyHub.Models;

public partial class Reporte
{
    public int IdReporte { get; set; }

    public int IdUsuarioReporta { get; set; }

    public int? IdPublicacion { get; set; }

    public int? IdComentario { get; set; }

    public int? IdUsuarioReportado { get; set; }

    public string Motivo { get; set; } = null!;

    public string Estado { get; set; } = null!;

    public DateTime FechaReporte { get; set; }

    public virtual Comentario? IdComentarioNavigation { get; set; }

    public virtual Publicacione? IdPublicacionNavigation { get; set; }

    public virtual Usuario IdUsuarioReportaNavigation { get; set; } = null!;

    public virtual Usuario? IdUsuarioReportadoNavigation { get; set; }
}
