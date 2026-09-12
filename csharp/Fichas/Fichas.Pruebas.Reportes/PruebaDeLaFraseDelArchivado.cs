using Fichas.Contratos.Consultas;
using Fichas.Reportes.Armado;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// El historico no puede contarse dos cosas distintas sobre lo mismo.
/// </summary>
/// <remarks>
/// <para>Nace de un defecto MEDIDO por QA sobre el paquete publicado el 2026-09-04: la
/// portada del historico decia de los archivados «Lo que hacen es salir de las listas de
/// trabajo del dia» y la seccion de la tabla, catorce lineas mas abajo, decia «Estos casos
/// NO salen de las listas de trabajo del dia». Las dos frases van en el MISMO PDF, y ese
/// PDF lo leen los jefes.</para>
///
/// <para><b>Ninguna de las dos era cierta</b>, y por eso este archivo no se limita a
/// igualarlas. Pero <b>la leccion de verdad la dio la segunda vez</b>: la frase que las
/// sustituyo tambien caduco —el dueno decidio el 2026-09-05 que un archivado sale del sistema
/// visible— y la prueba que la vigilaba <b>siguio en verde</b>, porque escribia a mano el
/// filtro que ella creia que usaban las pantallas en vez de llamarlas. Una prueba que se
/// compara consigo misma no vigila nada.</para>
///
/// <para>Lo que queda aqui es lo que este proyecto PUEDE afirmar sin referenciar la
/// aplicacion: que las dos frases del PDF son la misma, y que el historico no pierde ningun
/// archivado. Que el archivado sale de verdad de las cuatro listas se comprueba llamando a los
/// filtros de las pantallas desde
/// <c>Fichas.Pruebas.App/Reportes/PruebasDeLaFraseDelArchivadoContraLaApp.cs</c>.</para>
/// </remarks>
[TestClass]
public sealed class PruebaDeLaFraseDelArchivado
{
    /// <summary>La portada y la tabla dicen la MISMA frase, no dos parecidas.</summary>
    /// <remarks>
    /// Se compara la cadena entera y no unas palabras sueltas: dos frases que solo coinciden
    /// «en lo esencial» son exactamente el defecto que se esta cerrando.
    /// </remarks>
    [TestMethod]
    public void LaPortadaYLaTablaDicenLoMismoDeLosArchivados()
    {
        var reportes = Montar(out _);
        var documento = reportes.DocumentoDelHistorico($"{BaseDePrueba.Hoy} 10:00:00");
        var tabla = documento.Secciones.Single(s => s.Titulo == "Histórico — casos archivados");

        Assert.Contains(
            ArmadoDelHistorico.QueLePasaAUnArchivado,
            documento.Portada.Frase,
            "La portada del histórico tiene que llevar la frase única de qué le pasa a un archivado.");

        Assert.Contains(
            ArmadoDelHistorico.QueLePasaAUnArchivado,
            tabla.Notas,
            "Las notas de la tabla tienen que llevar esa MISMA frase, carácter a carácter. "
            + "Notas: " + string.Join(" | ", tabla.Notas));
    }

    /// <summary>
    /// De las listas de las que la frase dice que sale, sale de verdad.
    /// </summary>
    /// <remarks>
    /// <c>FiltroDeCasos.Todo</c> —con <c>IncluirArchivados</c> en falso, que es su valor por
    /// defecto— es literalmente el filtro con el que el selector de Correccion pide sus casos
    /// (<c>Fichas.App/Correccion/PaginaDeCorreccion.xaml.cs:82</c>). Es la medicion de QA
    /// —«18 de 18 casos» paso a «15 de 15» tras archivar tres— puesta donde no se pueda
    /// deshacer sin enterarse.
    /// </remarks>
    [TestMethod]
    public void UnCasoArchivadoNoSaleDondeLaFraseDiceQueNoSale()
    {
        var reportes = Montar(out var servicios);
        var entera = new Pagina(0, int.MaxValue);

        var conArchivados = servicios.Casos.Listar(new FiltroDeCasos(IncluirArchivados: true), entera).Elementos;
        var archivados = conArchivados.Where(c => c.Archivado).ToList();
        Assert.IsNotEmpty(archivados, "Sin ningún caso archivado en la base esto no comprueba nada.");

        var sinArchivados = servicios.Casos.Listar(FiltroDeCasos.Todo, entera).Elementos;

        Assert.HasCount(
            conArchivados.Count - archivados.Count,
            sinArchivados,
            $"Con archivados: {conArchivados.Count} · archivados: {archivados.Count} · "
            + $"sin archivados: {sinArchivados.Count}. El selector de Corrección pide con "
            + "«FiltroDeCasos.Todo», así que un archivado NO debe salir ahí.");

        // Y el histórico, que es este mismo informe, sigue contándolos: la frase promete las
        // dos cosas a la vez y las dos se comprueban en la misma pasada.
        var documento = reportes.DocumentoDelHistorico($"{BaseDePrueba.Hoy} 10:00:00");
        var tabla = documento.Secciones.Single(s => s.Titulo == "Histórico — casos archivados");
        Assert.HasCount(archivados.Count, tabla.Filas, "El histórico tiene que traerlos todos.");
    }

    /// <summary>
    /// Ni un archivado se pierde: el histórico los trae todos, que es lo único que aquí se puede afirmar.
    /// </summary>
    /// <remarks>
    /// <para>⚠️ <b>Esta prueba se llamaba <c>UnCasoArchivadoSigueSaliendoDondeLaFraseDiceQueSigue</c>
    /// y estaba PODRIDA por dentro.</b> Comprobaba que un archivado seguia saliendo en Revisar y
    /// en Asignar, y lo comprobaba escribiendo a mano el filtro que ella creia que usaban esas
    /// dos pantallas —su propio comentario lo confesaba: <i>«se comprueba con el mismo filtro
    /// escrito aquí, y no llamando a la app»</i>—. El 2026-09-05 el dueño decidio lo contrario
    /// (<i>«cuando yo archive, debe salir del sistema visible»</i>) y las dos pantallas pasaron a
    /// <c>IncluirArchivados: false</c>. <b>La prueba siguio en verde</b>, porque comparaba su
    /// copia consigo misma: <c>archivados.Count</c> contra
    /// <c>comoLosPideRevisar.Count(c =&gt; c.Archivado)</c>, que es la misma cuenta escrita dos
    /// veces. Con el filtro de la aplicacion cambiado y la frase del PDF ya falsa, no se puso
    /// roja ni una vez.</para>
    ///
    /// <para><b>Por que no se arregla llamando a la aplicacion desde aquí:</b>
    /// <c>Fichas.Pruebas.Reportes</c> referencia <c>Fichas.Contratos</c>,
    /// <c>Fichas.Reportes</c> y <c>Fichas.Datos.Falso</c>, y NO <c>Fichas.App</c> (medido en
    /// <c>Fichas.Pruebas.Reportes.csproj</c>). Asi que esta prueba se queda con lo único que
    /// desde aqui se puede afirmar sin inventarse el comportamiento de una pantalla: <b>que el
    /// histórico los trae todos</b>. Lo demas —que el archivado sale de verdad de las cuatro
    /// listas, llamando a los filtros de las pantallas— lo comprueba
    /// <c>Fichas.Pruebas.App/Reportes/PruebasDeLaFraseDelArchivadoContraLaApp.cs</c>, que sí
    /// referencia la aplicacion.</para>
    /// </remarks>
    [TestMethod]
    public void ElHistoricoTraeTodosLosArchivadosYNingunoSePierde()
    {
        var reportes = Montar(out var servicios);
        var entera = new Pagina(0, int.MaxValue);

        var archivados = servicios.Casos
            .Listar(new FiltroDeCasos(IncluirArchivados: true), entera).Elementos
            .Where(c => c.Archivado)
            .ToList();
        Assert.IsNotEmpty(archivados, "Sin ningún caso archivado en la base esto no comprueba nada.");

        var documento = reportes.DocumentoDelHistorico($"{BaseDePrueba.Hoy} 10:00:00");
        var tabla = documento.Secciones.Single(s => s.Titulo == "Histórico — casos archivados");

        Assert.HasCount(
            archivados.Count,
            tabla.Filas,
            $"En la base hay {archivados.Count} archivados y el histórico trae {tabla.Filas.Count}. "
            + "El histórico es el sitio donde el dueño dijo que se quedan: si aquí falta uno, se "
            + "perdió del todo.");
    }

    /// <summary>El motor sobre la base falsa con reloj fijo.</summary>
    /// <param name="servicios">Los servicios falsos, para mirar los puertos.</param>
    /// <param name="casos">Cuántos casos genera la base.</param>
    private static Fichas.Reportes.ReportesEnPdf Montar(out Fichas.Datos.Falso.ServiciosFalsos servicios, int casos = 300)
    {
        servicios = BaseDePrueba.Montar(casos);
        return new Fichas.Reportes.ReportesEnPdf(
            servicios.Casos, servicios.Personas, servicios.Companeros,
            servicios.Asignaciones, servicios.Procedencia, servicios.Reloj);
    }
}
