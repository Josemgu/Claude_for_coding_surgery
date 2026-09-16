using Fichas.Reportes.Modelo;

namespace Fichas.Reportes.Formato;

/// <summary>
/// En que fila cae cada pieza de la hoja unica, calculado una vez a partir de cuantas filas hay.
/// </summary>
/// <remarks>
/// Lo fijo es del mockup: cabecera en 1-2, tarjetas en 4-6, titulos de las tablas en la 8,
/// rotulos en la 9 y primera fila de datos en la 10. Lo que se mueve depende de cuantas
/// unidades y agentes haya: el total de cada tabla, la leyenda, el grafico y donde empieza
/// la lista de pendientes, que va debajo de lo mas largo de las dos columnas.
/// </remarks>
/// <param name="FilaDelTotalDeUnidades">La fila «Total» de la tabla por unidad.</param>
/// <param name="FilaDelTotalDeAgentes">La fila «Total» de la tabla por agente.</param>
/// <param name="FilaDeLaLeyenda">La leyenda del semaforo, dos filas bajo el total de unidades.</param>
/// <param name="FilaDelTituloDelGrafico">«Viajaron por mes», dos filas bajo el total de agentes.</param>
/// <param name="FilaDelTituloDePendientes">«Pendientes», dos filas bajo lo mas largo.</param>
/// <param name="UltimaFila">La ultima fila escrita; cierra el area de impresion.</param>
internal sealed record DisposicionDelResumen(
    int FilaDelTotalDeUnidades,
    int FilaDelTotalDeAgentes,
    int FilaDeLaLeyenda,
    int FilaDelTituloDelGrafico,
    int FilaDelTituloDePendientes,
    int UltimaFila)
{
    /// <summary>La fila de los titulos «Por unidad» y «Por agente».</summary>
    internal const int FilaDeLosTitulos = 8;

    /// <summary>La fila de los rotulos de las dos tablas.</summary>
    internal const int FilaDeLosRotulos = 9;

    /// <summary>La primera fila de datos de las dos tablas.</summary>
    internal const int PrimeraFilaDeDatos = 10;

    /// <summary>Cuantas filas ocupa el grafico, como en el mockup (J19:N30).</summary>
    internal const int FilasDelGrafico = 12;

    /// <summary>La primera fila sobre la que flota el grafico.</summary>
    internal int PrimeraFilaDelGrafico => FilaDelTituloDelGrafico + 1;

    /// <summary>La ultima fila sobre la que flota el grafico.</summary>
    internal int UltimaFilaDelGrafico => FilaDelTituloDelGrafico + FilasDelGrafico;

    /// <summary>La fila de rotulos de la lista de pendientes.</summary>
    internal int FilaDeLosRotulosDePendientes => FilaDelTituloDePendientes + 1;

    /// <summary>La primera persona de la lista de pendientes.</summary>
    internal int PrimeraFilaDePendientes => FilaDelTituloDePendientes + 2;

    /// <summary>Calcula la disposicion para ese resumen.</summary>
    /// <param name="resumen">El resumen, del que se cuentan unidades, agentes y pendientes.</param>
    internal static DisposicionDelResumen De(ResumenDelPeriodo resumen)
    {
        var totalDeUnidades = PrimeraFilaDeDatos + resumen.PorUnidad.Count;
        var totalDeAgentes = PrimeraFilaDeDatos + resumen.PorAgente.Count;
        var leyenda = totalDeUnidades + 2;
        var tituloDelGrafico = totalDeAgentes + 2;
        var tituloDePendientes = Math.Max(leyenda, tituloDelGrafico + FilasDelGrafico) + 2;

        return new DisposicionDelResumen(
            totalDeUnidades,
            totalDeAgentes,
            leyenda,
            tituloDelGrafico,
            tituloDePendientes,
            tituloDePendientes + 1 + Math.Max(1, resumen.Pendientes.Count));
    }
}
