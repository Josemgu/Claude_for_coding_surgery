using System.Text;
using Fichas.Contratos.Modelos;
using Fichas.Reportes.Armado;
using Fichas.Reportes.Consultas;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// Vuelca el documento de un juego de datos fijo para poder compararlo con el del Python.
/// </summary>
/// <remarks>
/// El criterio del pase pide comparar el contenido con el que produce hoy el Python. La
/// comparacion se hace FUERA, con un guion que llama a los mismos modulos de <c>reportes/</c>
/// sobre las mismas filas y escribe el mismo formato; esta prueba solo pone el lado de C#.
///
/// El juego de datos esta escrito a mano y es minusculo A PROPOSITO: tiene que caber entero en
/// las dos cabezas para que una diferencia se pueda leer, y trae adrede los tres casos que
/// rompen cosas —un caso SIN numero, una persona sin ningun paso contestado, y alguien que no
/// pudo viajar cuyo caso no tiene fecha—.
///
/// ⚠️ El volcado va a la carpeta temporal, NUNCA al repositorio: no se commitea ningun archivo
/// generado, y estos datos son inventados pero el formato es el de un informe real.
///
/// ⚠️ <b>Desde el 2026-09-05 el espejo YA NO PUEDE salir identico, y es a proposito.</b> El
/// dueno pidio el informe del programa viejo (<c>DECISIONES.md</c>, 2026-09-04), asi que C#
/// dice «Verificadas» / «Sin verificar» / «Quiénes viajaron sin verificar» donde el Python
/// sigue diciendo «con la preparación completa», y el papel es Carta vertical donde el Python
/// lo tiene apaisado. Quien corra la comparacion tiene que esperar esas diferencias de ROTULO
/// y de PAPEL, y ninguna otra: si aparece una diferencia de CIFRA o de FILA, eso si es un
/// fallo. Esta prueba sigue valiendo porque lo que vigila es que el lado de C# se pueda
/// volcar entero y no cambie de forma sin que nadie lo vea.
/// </remarks>
[TestClass]
public class PruebaDeEspejoConElPython
{
    private const string Hoy = "2026-09-20";
    private const string GeneradoEn = "2026-09-20 10:00:00";

    /// <summary>Donde queda el volcado de C# para que el guion de Python lo compare.</summary>
    internal static string RutaDelVolcado
        => Path.Combine(Path.GetTempPath(), "fichas-espejo", "csharp.txt");

    [TestMethod]
    public void VuelcaElDocumentoDelJuegoFijoParaCompararloConElPython()
    {
        var documento = ArmadoDelDocumento.DelPeriodo(
            JuegoFijo(), Periodo.Leer("2026-09-01", "2026-09-30").Periodo!, GeneradoEn);

        var volcado = Volcar(documento);
        Directory.CreateDirectory(Path.GetDirectoryName(RutaDelVolcado)!);
        File.WriteAllText(RutaDelVolcado, volcado, new UTF8Encoding(false));

        // Las tres cosas que el juego fijo existe para probar, comprobadas aqui mismo para que
        // la prueba valga aunque nadie llegue a correr el guion de Python.
        StringAssert.Contains(volcado, "AVISO|1 persona anotada");
        StringAssert.Contains(volcado, Avisos.SinNumeroDeCaso);
        StringAssert.Contains(volcado, "CIFRA|1|viajaron sin verificar|Malo");
    }

    [TestMethod]
    public void DejaElPdfDelJuegoFijoParaQueLoAbraUnLectorDeVerdad()
    {
        // ⚠️ Esta prueba NO comprueba que el PDF abra: comprueba que se escribe. Que ABRE lo
        // dice un lector de PDF ajeno a este codigo, y por eso el archivo se deja puesto en vez
        // de borrarse. Una prueba que valida su propio formato con su propio codigo no valida
        // nada. El lector externo va en la entrega con su comando y su salida.
        var documento = ArmadoDelDocumento.DelPeriodo(
            JuegoFijo(), Periodo.Leer("2026-09-01", "2026-09-30").Periodo!, GeneradoEn);

        var ruta = Path.Combine(Path.GetDirectoryName(RutaDelVolcado)!, "reporte-del-juego-fijo.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllBytes(ruta, Fichas.Reportes.Formato.Maqueta.ConstruirPdf(documento));

        Assert.IsGreaterThan(1000, new FileInfo(ruta).Length);
        Console.WriteLine($"MEDIDO · PDF del juego fijo dejado en {ruta}");
    }

    /// <summary>El juego de datos fijo, escrito a mano y con los tres casos que rompen cosas.</summary>
    private static LecturaParaReportes JuegoFijo()
    {
        var casos = new List<Caso>
        {
            new()
            {
                Id = 1, NumeroCaso = "CASP2609", UnidadNombre = "Barahona", UnidadNumero = "7000011",
                TemploNombre = "Santo Domingo Dominican Republic", FechaViaje = "2026-09-05",
                EstadoRecomendacion = "no_completa", CreadoEn = "2026-08-01 09:00:00",
            },
            new()
            {
                // Sin numero de caso: el que rompia el informe entero.
                Id = 2, NumeroCaso = null, UnidadNombre = "La Vega", UnidadNumero = null,
                TemploNombre = null, FechaViaje = "2026-09-25",
                EstadoRecomendacion = null, CreadoEn = "2026-08-15 11:30:00",
            },
            new()
            {
                // Sin fecha de viaje Y sin numero: no cabe en ningun periodo, dispara el aviso, y
                // es justo el caso que hacia reventar el informe entero al ordenar los numeros.
                Id = 3, NumeroCaso = null, UnidadNombre = "Puerto Plata", UnidadNumero = "7000012",
                TemploNombre = "Caracas Venezuela", FechaViaje = null,
                EstadoRecomendacion = "completa", CreadoEn = "2026-07-20 08:00:00",
            },
        };

        var personas = new Dictionary<long, IReadOnlyList<Persona>>
        {
            [1] =
            [
                Con(1, 1, "Ana Anonimo", "055-1111-3853", 1, todosLosPasos: true, pudoViajar: true,
                    ordInvestidura: true),
                Con(2, 1, "Luis Anonimo", null, 2, todosLosPasos: null, pudoViajar: false,
                    ordRecibirPropias: true, motivo: "La recomendación venció la semana antes.")
                    with { PasoPreparacion = true, PasoInformacion = false },
            ],
            [2] = [Con(3, 2, "Rosa Peralta", "010-0001-0002", 1, todosLosPasos: null, pudoViajar: null,
                       ordTraductor: true)],
            [3] = [Con(4, 3, "Carmen Nuñez", null, 1, todosLosPasos: true, pudoViajar: false,
                       motivo: "Sin fecha de viaje anotada.")],
        };

        var verificacion = new List<CasoConSuVerificacion>
        {
            new(casos[0], 2, 4, 4, "2026-09-03 10:00:00"),
            new(casos[1], 1, 0, 0, null),
            new(casos[2], 1, 2, 1, "2026-09-10 08:00:00"),
        };

        return LecturaParaReportes.DeMemoria(
            casos,
            personas,
            new Dictionary<long, IReadOnlyList<string>> { [1] = ["Sandy"] },
            verificacion);
    }

    private static Persona Con(
        long id, long casoId, string? nombre, string? mrn, int fila,
        bool? todosLosPasos, bool? pudoViajar,
        bool? ordRecibirPropias = null, bool? ordTraductor = null, bool? ordInvestidura = null,
        string? motivo = null)
        => new()
        {
            Id = id, CasoId = casoId, Nombre = nombre, Mrn = mrn, FilaFormulario = fila,
            PudoViajar = pudoViajar, MotivoNoViajo = motivo,
            OrdRecibirPropias = ordRecibirPropias,
            OrdTraductor = ordTraductor,
            OrdInvestidura = ordInvestidura,
            PasoPreparacion = todosLosPasos,
            PasoInformacion = todosLosPasos,
            PasoCitaDelTemplo = todosLosPasos,
            PasoAccionesRequeridas = todosLosPasos,
            PasoEntrevistas = todosLosPasos,
            PasoListoParaElTemplo = todosLosPasos,
        };

    /// <summary>El documento en un formato de una linea por cosa, para poder diferenciarlo.</summary>
    /// <remarks>
    /// No es JSON a proposito: un <c>diff</c> de lineas dice QUE linea cambio, y un JSON
    /// reindentado dice que cambio el archivo entero.
    /// </remarks>
    internal static string Volcar(Documento documento)
    {
        var salida = new StringBuilder();
        salida.Append("TITULO|").Append(documento.Titulo).Append('\n');
        salida.Append("SUBTITULO|").Append(documento.Subtitulo).Append('\n');
        salida.Append("TITULAR|").Append(documento.Portada.Titular).Append('\n');
        salida.Append("FRASE|").Append(documento.Portada.Frase).Append('\n');

        foreach (var cifra in documento.Portada.Cifras)
        {
            salida.Append("CIFRA|").Append(cifra.Numero).Append('|')
                .Append(cifra.Rotulo).Append('|').Append(cifra.Tono).Append('\n');
        }
        foreach (var aviso in documento.Avisos) salida.Append("AVISO|").Append(aviso).Append('\n');

        foreach (var seccion in documento.Secciones)
        {
            salida.Append("SECCION|").Append(seccion.Titulo).Append('\n');
            foreach (var nota in seccion.Notas) salida.Append("NOTA|").Append(nota).Append('\n');
            salida.Append("COLUMNAS|").Append(string.Join('|', seccion.Columnas.Select(c => c.Nombre))).Append('\n');
            foreach (var fila in seccion.Filas)
            {
                salida.Append("FILA|").Append(string.Join('|', fila.Select(c => c ?? string.Empty))).Append('\n');
            }
            salida.Append("RESUMEN|").Append(seccion.Resumen ?? string.Empty).Append('\n');
        }
        return salida.ToString();
    }
}
