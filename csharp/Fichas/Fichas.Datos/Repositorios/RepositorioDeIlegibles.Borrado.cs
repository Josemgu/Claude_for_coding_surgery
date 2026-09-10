using System.Globalization;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Mantenimiento;
using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Repositorios;

/// <summary>
/// La mitad de <see cref="RepositorioDeIlegibles"/> que BORRA los renglones de los PDF que
/// no se pudieron leer y no dejaron ningun documento detras.
/// </summary>
/// <remarks>
/// <para>Va en su propio archivo por el mismo reparto que <c>GuardadoDeHojas.Filas.cs</c> y
/// <c>PaginaDeImportar.Carpetas.cs</c>: la otra mitad guarda y consulta, y esto es lo unico
/// que quita filas. Juntos pasaban de 400 lineas.</para>
///
/// <para>⛔ Y hay un motivo mejor que el tamano para que este aparte: es el unico sitio de
/// este repositorio que borra, y borrar es la excepcion declarada del programa
/// (DECISIONES.md 2026-09-04, punto 9). Que se vea de un vistazo cual es el archivo que
/// quita filas vale mas que la comodidad de tenerlo todo junto.</para>
///
/// <para>Del dueno, el 2026-09-07 y dicho dos veces el mismo dia: <i>«no se contempla
/// eliminar PDF o documentos que no tienen informacion; debe poder eliminarlo»</i>. Un PDF
/// que no se pudo leer en absoluto deja un renglon con <c>caso_id</c> NULO, no tiene
/// documento al que agarrarse, y hasta hoy ningun borrado del programa lo alcanzaba.</para>
/// </remarks>
public sealed partial class RepositorioDeIlegibles
{
    /// <inheritdoc />
    public PlanDeBorrado PlanearBorradoDeRenglonesSinCaso(IReadOnlyCollection<long> renglonIds)
    {
        ArgumentNullException.ThrowIfNull(renglonIds);

        var marcados = renglonIds.Distinct().ToList();
        var sinDocumento = LosQueNoTienenDocumento(marcados);
        var conDocumento = marcados.Count - sinDocumento.Count;

        if (sinDocumento.Count == 0)
        {
            // Sin nada que borrar no se copia la base: una copia de 40 MiB por una
            // pulsacion que no borra nada es un archivo que aparece sin motivo.
            return PlanDeBorrado.NoSePuede(
                AlcanceDelBorrado.Documentos,
                null,
                conDocumento == 0
                    ? Aviso.Informa("No hay ningún renglón marcado que borrar.")
                    : AvisoDeLosQueTienenDocumento(conDocumento));
        }

        var copia = RespaldoAntesDeBorrar.Hacer(Conexion);
        if (!copia.HayCopia)
        {
            return PlanDeBorrado.NoSePuede(AlcanceDelBorrado.Documentos, null, copia.Fallo!);
        }

        return new PlanDeBorrado
        {
            Alcance = AlcanceDelBorrado.Documentos,
            Ids = sinDocumento,
            Conteos = [ConteoDeLosRenglones(sinDocumento.Count)],
            RutaDeLaCopia = copia.Ruta,
            SePuedeBorrar = true,
            Avisos = conDocumento == 0 ? [] : [AvisoDeLosQueTienenDocumento(conDocumento)],
        };
    }

    /// <inheritdoc />
    public ResultadoDeBorrado BorrarRenglonesSinCaso(PlanDeBorrado plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        // ⛔ Un plan de documentos ejecutado por aqui borraria RENGLONES cuyos ids
        // coinciden con los de unos casos. Los dos planes son del mismo tipo, asi que el
        // cruce compila y solo esta comprobacion lo para.
        if (!EsUnPlanDeRenglones(plan))
        {
            return ResultadoDeBorrado.NoSeBorro(
                plan.RutaDeLaCopia,
                Aviso.Problema(
                    "No se borró nada: ese plan no es de los PDF que no se pudieron leer.",
                    string.Empty,
                    "Un plan de documentos no se ejecuta por aquí. Vuelva a intentarlo desde su botón."));
        }

        if (!plan.SePuedeBorrar)
        {
            return ResultadoDeBorrado.NoSeBorro(
                plan.RutaDeLaCopia,
                Aviso.Problema(
                    "No se borró nada: este borrado no tenía permiso.",
                    string.Empty,
                    "El plan llegó marcado como «no se puede borrar». Vuelva a intentarlo desde el botón."));
        }

        // ⛔ La regla que no se negocia: sin copia previa NO se borra. Ni con permiso.
        if (plan.RutaDeLaCopia is null)
        {
            return ResultadoDeBorrado.NoSeBorro(
                null,
                Aviso.Problema(
                    "No se borró nada: no había copia previa de la base.",
                    string.Empty,
                    "Nada se borra sin haber copiado antes. Libere espacio en el disco y vuelva a intentarlo."));
        }

        // Se vuelve a mirar: entre la pregunta y el «si» un renglon pudo ganar documento,
        // y entonces ya no es huerfano y no se ofrece suelto.
        var siguenSinDocumento = LosQueNoTienenDocumento(plan.Ids);
        if (siguenSinDocumento.Count == 0)
        {
            return ResultadoDeBorrado.NoSeBorro(
                plan.RutaDeLaCopia,
                Aviso.Advierte(
                    "No se borró nada: esos renglones ya tienen un documento detrás.",
                    string.Empty,
                    "Algo cambió desde que se preguntó. Se van a ir con su documento cuando "
                    + "se borre el documento."));
        }

        return BorrarEsosRenglones(siguenSinDocumento, plan.RutaDeLaCopia);
    }

    /// <summary>Borra los renglones, todo o nada, y devuelve lo que de verdad cayo.</summary>
    private ResultadoDeBorrado BorrarEsosRenglones(IReadOnlyList<long> ids, string rutaDeLaCopia)
    {
        try
        {
            using var trato = Conexion.BeginTransaction();
            using var orden = Conexion.CreateCommand();
            orden.Transaction = trato;

            // La condicion repite «caso_id IS NULL» aunque ya se comprobo: entre la
            // comprobacion y el DELETE va una vuelta mas, y el filtro que borra tiene que
            // ser el mismo que conto.
            orden.CommandText =
                "DELETE FROM documentos_ilegibles WHERE id = $id AND caso_id IS NULL";
            var parametro = orden.CreateParameter();
            parametro.ParameterName = "$id";
            orden.Parameters.Add(parametro);

            var cuantas = 0;
            foreach (var id in ids)
            {
                parametro.Value = id;
                cuantas += orden.ExecuteNonQuery();
            }

            trato.Commit();

            return new ResultadoDeBorrado
            {
                SeBorro = cuantas > 0,
                Borradas = [ConteoDeLosRenglones(cuantas)],
                RutaDeLaCopia = rutaDeLaCopia,
            };
        }
        catch (SqliteException fallo)
        {
            return ResultadoDeBorrado.NoSeBorro(
                rutaDeLaCopia,
                Aviso.Problema(
                    "No se pudo borrar: la base lo rechazó y se dejó todo como estaba.",
                    string.Empty,
                    $"Detalle del motor: {fallo.Message}. La copia previa está en "
                    + $"«{rutaDeLaCopia}» y no se ha tocado."));
        }
    }

    /// <summary>
    /// Cuales de esos ids son renglones que NO tienen documento detras.
    /// </summary>
    /// <remarks>
    /// Se pregunta id a id, con un parametro, en vez de armar un <c>IN (...)</c>: ningun
    /// valor se concatena jamas en una instruccion (ARQUITECTURA §1.7) ni siquiera siendo
    /// numeros, y asi tampoco se choca contra el limite de 999 parametros de SQLite. Y NO
    /// se usa la tabla temporal <c>ids_a_borrar</c> de <c>RepositorioDeMantenimiento</c> a
    /// proposito: es una sola tabla por conexion, y los dos borrados comparten conexion.
    /// </remarks>
    private List<long> LosQueNoTienenDocumento(IEnumerable<long> ids)
    {
        using var orden = Conexion.CreateCommand();
        orden.CommandText =
            "SELECT COUNT(*) FROM documentos_ilegibles WHERE id = $id AND caso_id IS NULL";
        var parametro = orden.CreateParameter();
        parametro.ParameterName = "$id";
        orden.Parameters.Add(parametro);

        var sinDocumento = new List<long>();
        foreach (var id in ids)
        {
            parametro.Value = id;
            if (Convert.ToInt32(orden.ExecuteScalar(), CultureInfo.InvariantCulture) > 0)
            {
                sinDocumento.Add(id);
            }
        }

        return sinDocumento;
    }

    /// <summary>Si el plan es de estos renglones y de nada mas.</summary>
    private static bool EsUnPlanDeRenglones(PlanDeBorrado plan)
        => plan.Conteos.Count == 1
           && string.Equals(plan.Conteos[0].Tabla, IIlegibles.TablaDeLosRenglones, StringComparison.Ordinal);

    /// <summary>
    /// Como se dicen estos renglones en la pregunta y en el acuse.
    /// </summary>
    /// <remarks>
    /// Dice «renglones» y no «PDF» a proposito: lo que cae es la anotacion de que un
    /// archivo no se pudo leer, y el archivo se queda donde esta. Una frase que dijera
    /// «borrar 3 PDF» haria creer al dueno que le quitaron tres escaneos.
    /// </remarks>
    private static ConteoDeTabla ConteoDeLosRenglones(int cuantas) => new(
        IIlegibles.TablaDeLosRenglones,
        cuantas,
        "renglón de un PDF que no se pudo leer",
        "renglones de PDF que no se pudieron leer");

    private static Aviso AvisoDeLosQueTienenDocumento(int cuantos) => Aviso.Advierte(
        cuantos == 1
            ? "Un renglón marcado no se borra: tiene un documento detrás."
            : $"{cuantos.ToString(CultureInfo.InvariantCulture)} renglones marcados no se borran: tienen un documento detrás.",
        string.Empty,
        "Ese renglón es el motivo por el que su documento está a medias, y se va con él "
        + "cuando se borre el documento.");

}
