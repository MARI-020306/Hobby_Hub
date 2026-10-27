using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Proyecto_HobbyHub.Security;
public sealed class SafePersistenceExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        int status;string message;
        if(context.Exception is DbUpdateConcurrencyException){status=409;message="El recurso cambió mientras lo editabas. Recarga e inténtalo de nuevo.";}
        else if(context.Exception is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } }){status=409;message="La acción ya existe o entró en conflicto. Recarga la página.";}
        else if(context.Exception is DbException or DbUpdateException){status=503;message="No fue posible guardar o consultar la información. Inténtalo de nuevo.";}
        else return;
        if (context.HttpContext.RequestServices.GetService<AuditEventBudget>()?.Allow() != false)
        {
            var logger = context.HttpContext.RequestServices.GetService<ILogger<SafePersistenceExceptionFilter>>();
            if (logger is not null) PersistenceDiagnostics.Log(logger, context.Exception, context.HttpContext, status);
        }
        context.Result=new ContentResult{StatusCode=status,Content=message,ContentType="text/plain; charset=utf-8"};context.ExceptionHandled=true;
    }
}
