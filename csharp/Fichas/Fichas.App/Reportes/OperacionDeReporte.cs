using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Reportes;

/// <summary>
/// Generar el PDF de los jefes y el del historico, y dejar dicho como quedo.
/// </summary>
/// <remarks>
/// <para>Es lo unico que hace la pantalla de Reportes cuando se pulsa el boton, y vive aqui
/// para poder probarlo SIN abrir ventana (ADR-0003 §8.1). No decide nada del contenido del
/// informe: eso es de <c>Fichas.Reportes</c>, detras de <see cref="IReportes"/>.</para>
///
/// <para>⚠️ <b>Despues de generar se MIRA el archivo.</b> Que la biblioteca conteste
/// «escrito» no es lo mismo que que el archivo este: con <c>--falso</c>, <c>ReportesFalsos</c>
/// contesta que genero y no toca el disco. Repetir su palabra dejaria al dueno buscando un
/// PDF que nunca existio; por eso la linea lleva el tamano en bytes, que solo se puede decir
/// habiendolo mirado.</para>
///
/// <para>⚠️ <b>Esta clase NO toca la franja de avisos.</b> Los avisos salen dentro del
/// <see cref="ResumenEnPantalla"/> y los deja la pantalla, que es quien esta en el hilo de la
/// ventana. El motivo, medido, esta escrito en <see cref="ResumenEnPantalla"/>.</para>
/// </remarks>
public sealed class OperacionDeReporte
{
    private readonly IReportes _reportes;

    /// <summary>Se ata al puerto de los reportes y a nada mas.</summary>
    public OperacionDeReporte(IReportes reportes) => _reportes = reportes;

    /// <summary>Genera el informe del periodo en esa ruta.</summary>
    public ResumenEnPantalla DelPeriodo(PeriodoDeLaPantalla periodo, string ruta)
        => Contar(
            _reportes.GenerarReporteDelPeriodo(periodo.Desde, periodo.Hasta, ruta),
            ruta,
            $"Reporte {periodo.EnTexto()}");

    /// <summary>Genera el historico completo en esa ruta.</summary>
    public ResumenEnPantalla Historico(string ruta)
        => Contar(_reportes.GenerarHistorico(ruta), ruta, "Histórico completo");

    /// <summary>
    /// Genera el informe de un agente en esa ruta: que hizo en el periodo y en su ultima semana.
    /// </summary>
    /// <remarks>
    /// <para>Lo pidio el dueno el 2026-09-05: <i>«es importante tener un informe por agente
    /// también: lo que hicieron los agentes en ese mes y lo que hicieron en esa semana, qué
    /// hicieron»</i>.</para>
    ///
    /// <para><b>Llama al metodo que YA existia en el contrato</b>,
    /// <c>GenerarReporteDeCompanero</c>, que estaba escrito y sin un solo boton que lo llamara.
    /// Lo que hacia era el informe de los jefes recortado a sus casos; lo que le faltaba —la
    /// seccion «Lo que hizo», con el mes y la semana— se le anadio dentro, en
    /// <c>Fichas.Reportes</c>. Desde aqui no cambia nada: es el mismo puerto.</para>
    /// </remarks>
    public ResumenEnPantalla DeUnAgente(Companero quien, PeriodoDeLaPantalla periodo, string ruta)
    {
        ArgumentNullException.ThrowIfNull(quien);

        return Contar(
            _reportes.GenerarReporteDeCompanero(quien.Id, periodo.Desde, periodo.Hasta, ruta),
            ruta,
            $"Informe de {quien.Nombre} {periodo.EnTexto()}");
    }

    /// <summary>Escribe la linea con el archivo ya mirado y recoge lo que hay que avisar.</summary>
    private static ResumenEnPantalla Contar(ResultadoDeEscritura resultado, string ruta, string deQue)
    {
        var avisos = new List<Aviso>(resultado.Avisos);
        var nombre = Path.GetFileName(ruta);
        var detalle = ResumenEnPantalla.DetalleDe(resultado.Avisos, $"Ruta completa: {ruta}");

        if (!resultado.SeEscribio)
        {
            var porQue = resultado.Avisos.Count > 0 ? resultado.Avisos[0].Linea : "no se dijo por qué.";
            return new ResumenEnPantalla(false, $"No se generó {deQue}: {porQue}", detalle, null, avisos);
        }

        var tamano = ResumenEnPantalla.TamanoDe(ruta);
        if (tamano is null)
        {
            var noEsta = Aviso.Problema(
                $"Se dijo que se escribió «{nombre}», pero el archivo no está.",
                string.Empty,
                $"Se buscó en «{ruta}» justo después de generarlo y no había nada que mirar. "
                + "Pasa siempre que el programa se abre con «--falso»: los datos son inventados y "
                + "no se escribe ningún archivo. Si NO se abrió así, avise: algo se lo llevó.");
            avisos.Add(noEsta);
            return new ResumenEnPantalla(false, noEsta.Linea, detalle, null, avisos);
        }

        return new ResumenEnPantalla(
            true,
            $"{deQue}: {nombre} · {ResumenEnPantalla.EnBytes(tamano.Value)}.",
            detalle,
            ruta,
            avisos);
    }
}
