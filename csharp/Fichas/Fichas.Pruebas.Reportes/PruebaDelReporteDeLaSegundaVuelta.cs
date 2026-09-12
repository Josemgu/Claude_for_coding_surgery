using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;
using Fichas.Reportes;
using Fichas.Reportes.Armado;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// El reporte para quien recibe la segunda vuelta: que se intento, por que no salio, y que dijo el agente.
/// </summary>
/// <remarks>
/// <para><b>De donde sale el criterio.</b> De las palabras del dueno del 2026-09-05
/// (<c>DECISIONES.md</c>): <i>«yo creo otro paquete para los gerentes, para que ellos puedan
/// comunicarse con los líderes de estaca y distrito, de lo que los agentes no pudieron. […]
/// Así que debo crear reporte para ellos con los comentarios de los agentes»</i>. Y del
/// criterio C15-4: el mismo motor de <c>IReportes</c>, un metodo mas, y con los comentarios
/// dentro.</para>
///
/// <para><b>El camino del comentario esta entero, medido el 2026-09-05.</b> La hoja del
/// companero trae la casilla «Comentario» (<c>Fichas.Paquetes/Columnas.cs:156</c>), la vuelta
/// la guarda en <c>personas.nota_companero</c> (<c>Fichas.Paquetes/Paquetes.cs:599</c>) y este
/// reporte la lee de ahi. Estuvo cortado hasta la FASE C14 y por eso el aviso sigue haciendo
/// falta: <b>una hoja devuelta ANTES de que la casilla existiera vuelve sin comentario para
/// siempre</b>. Aqui se comprueban las dos ramas: con comentario sale entero, y sin ninguno el
/// reporte DICE por que en vez de dejar una columna en blanco, que se lee como «el agente no
/// dijo nada».</para>
/// </remarks>
[TestClass]
public class PruebaDelReporteDeLaSegundaVuelta
{
    /// <summary>El día fijo del reloj del escenario.</summary>
    private const string Hoy = "2026-09-05";
    /// <summary>La marca con la que se arma el reporte en todas las pruebas.</summary>
    private const string GeneradoEn = "2026-09-05 10:00:00";

    /// <summary>El reporte dice a que peldano va y cuantos documentos lleva.</summary>
    [TestMethod]
    public void ElReporteDiceAQuePeldanoVaYCuantosLleva()
    {
        var mundo = Escenario(conComentario: true);

        var documento = mundo.Reportes.DocumentoDeLaSegundaVuelta(2, mundo.Intentos, GeneradoEn);

        StringAssert.Contains(documento.Portada.Titular, "categoría 2");
        Assert.AreEqual(2, documento.Portada.Cifras[0].Numero);
    }

    /// <summary>
    /// Cada renglon dice quien lo intento, por que no salio, y lo que dijo ese agente.
    /// </summary>
    /// <remarks>Son las tres cosas que necesita quien va a llamar al lider de estaca.</remarks>
    [TestMethod]
    public void CadaRenglonLlevaQuienLoIntentoPorQueNoSalioYElComentario()
    {
        var mundo = Escenario(conComentario: true);

        var documento = mundo.Reportes.DocumentoDeLaSegundaVuelta(2, mundo.Intentos, GeneradoEn);
        var seccion = documento.Secciones.Single(s => s.Titulo == ArmadoDeLaSegundaVuelta.TituloDeLaSeccion);
        var celdas = seccion.Filas.SelectMany(fila => fila).Select(celda => celda ?? string.Empty).ToList();

        Assert.IsTrue(celdas.Any(c => c.Contains("Sandy", StringComparison.Ordinal)),
            "Falta quién lo intentó.");
        Assert.IsTrue(celdas.Any(c => c.Contains("no se pudo comunicar con el líder", StringComparison.Ordinal)),
            "Falta por qué no salió, con las palabras del dueño.");
        Assert.IsTrue(celdas.Any(c => c.Contains("Llamé tres veces", StringComparison.Ordinal)),
            $"Falta el comentario del agente. Lo que hay: {string.Join(" | ", celdas)}");
    }

    /// <summary>Un renglon por PERSONA, no por documento: al lider se le llama por alguien.</summary>
    [TestMethod]
    public void HayUnRenglonPorPersonaYNoPorDocumento()
    {
        var mundo = Escenario(conComentario: true);

        var documento = mundo.Reportes.DocumentoDeLaSegundaVuelta(2, mundo.Intentos, GeneradoEn);
        var seccion = documento.Secciones.Single(s => s.Titulo == ArmadoDeLaSegundaVuelta.TituloDeLaSeccion);

        // El escenario deja 2 documentos: uno con dos personas y otro con una.
        Assert.HasCount(3, seccion.Filas);
        Assert.AreEqual(3, documento.Portada.Cifras[1].Numero, "La portada tiene que contar las personas.");
    }

    /// <summary>Un comentario largo entra entero: no se recorta al guardarlo ni al leerlo.</summary>
    /// <remarks>Es el criterio C14-5, mirado desde el lado que lo lee.</remarks>
    [TestMethod]
    public void UnComentarioLargoSaleEnteroEnElReporte()
    {
        var largo = new string('a', 500);
        var mundo = Escenario(conComentario: true, comentario: largo);

        var documento = mundo.Reportes.DocumentoDeLaSegundaVuelta(2, mundo.Intentos, GeneradoEn);
        var seccion = documento.Secciones.Single(s => s.Titulo == ArmadoDeLaSegundaVuelta.TituloDeLaSeccion);
        var comentarios = seccion.Filas.Select(fila => fila[^1] ?? string.Empty).ToList();

        Assert.IsTrue(comentarios.Any(c => c.Length >= 500),
            $"El comentario salió recortado. El más largo mide {comentarios.Max(c => c.Length)}.");
    }

    /// <summary>
    /// Sin comentarios, el reporte DICE por que estan vacios en vez de dejar la columna en blanco.
    /// </summary>
    /// <remarks>
    /// Es la diferencia entre «el agente no dijo nada» y «esa hoja es de antes de que existiera
    /// la casilla». Las dos pasan, y quien reciba este PDF tiene que saber cuál puede ser o
    /// creerá que sus agentes no comentan.
    /// </remarks>
    [TestMethod]
    public void SinNingunComentarioElReporteAvisaDePorQue()
    {
        var mundo = Escenario(conComentario: false);

        var documento = mundo.Reportes.DocumentoDeLaSegundaVuelta(2, mundo.Intentos, GeneradoEn);

        Assert.IsTrue(
            documento.Avisos.Any(aviso => aviso.Contains("casilla «Comentario»", StringComparison.Ordinal)),
            $"Falta el aviso que explica por qué no hay comentarios. Avisos: {string.Join(" | ", documento.Avisos)}");
    }

    /// <summary>Con comentarios NO se avisa: el aviso sobraria y quitaria sitio.</summary>
    [TestMethod]
    public void ConComentariosNoSeAvisaDeNada()
    {
        var mundo = Escenario(conComentario: true);

        var documento = mundo.Reportes.DocumentoDeLaSegundaVuelta(2, mundo.Intentos, GeneradoEn);

        Assert.IsFalse(documento.Avisos.Any(aviso => aviso.Contains("casilla «Comentario»", StringComparison.Ordinal)));
    }

    /// <summary>Sin nada que subir no se escribe un PDF vacio: se dice y ya.</summary>
    [TestMethod]
    public void SinNadaQueSubirNoSeEscribeNingunPdf()
    {
        var mundo = Escenario(conComentario: true);
        var ruta = Path.Combine(Path.GetTempPath(), "fichas-segunda-vuelta", "no-deberia-existir.pdf");

        var resultado = mundo.Reportes.GenerarReporteDeLaSegundaVuelta(2, [], ruta);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.IsFalse(File.Exists(ruta));
    }

    /// <summary>Deja el PDF puesto para que lo abra un lector de verdad.</summary>
    [TestMethod]
    public void DejaElPdfDeLaSegundaVueltaPuesto()
    {
        var mundo = Escenario(conComentario: true);
        var carpeta = Path.Combine(Path.GetTempPath(), "fichas-segunda-vuelta");
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, "segunda-vuelta-categoria-2.pdf");

        var resultado = mundo.Reportes.GenerarReporteDeLaSegundaVuelta(2, mundo.Intentos, ruta);

        Assert.IsTrue(resultado.SeEscribio, string.Join(" · ", resultado.Avisos.Select(a => a.Linea)));
        Console.WriteLine($"MEDIDO · {ruta} · {new FileInfo(ruta).Length} bytes");
    }

    // ---- el escenario -------------------------------------------------------

    /// <summary>Lo que devuelve el escenario: el motor ya montado y los intentos que suben.</summary>
    /// <param name="Reportes">El motor sobre el almacén del escenario.</param>
    /// <param name="Intentos">Los dos documentos que suben, con quien los intentó.</param>
    private sealed record Mundo(ReportesEnPdf Reportes, IReadOnlyList<IntentoAnterior> Intentos);

    /// <summary>Dos documentos que suben: uno con dos personas dentro y otro con una.</summary>
    /// <param name="conComentario">Si las personas llevan comentario del agente o nulo.</param>
    /// <param name="comentario">El comentario que llevan cuando lo llevan.</param>
    private static Mundo Escenario(bool conComentario, string comentario = "Llamé tres veces al líder y no contestó.")
    {
        var servicios = new ServiciosFalsos(0, 3, new RelojFijo(Hoy));
        var almacen = servicios.Almacen;
        almacen.Companeros.Clear();

        var sandy = new Companero
        {
            Id = almacen.SiguienteId(), Nombre = "Sandy", Activo = true, CreadoEn = $"{Hoy} 08:00:00",
        };
        almacen.Companeros[sandy.Id] = sandy;

        var primero = Caso(almacen, "CASP2601", personas: 2, conComentario ? comentario : null);
        var segundo = Caso(almacen, "CASP2602", personas: 1, conComentario ? comentario : null);

        IReadOnlyList<IntentoAnterior> intentos =
        [
            new(primero, sandy.Nombre, 1, MotivoDeNoCompletar.NoSePudoComunicar),
            new(segundo, sandy.Nombre, 1, MotivoDeNoCompletar.ElLiderNoLoHizo),
        ];

        return new Mundo(
            new ReportesEnPdf(
                servicios.Casos, servicios.Personas, servicios.Companeros,
                servicios.Asignaciones, servicios.Procedencia, servicios.Reloj),
            intentos);
    }

    /// <summary>Un caso no completo con ese número y tantas personas, todas con el mismo comentario.</summary>
    /// <param name="almacen">El almacén falso donde se escribe.</param>
    /// <param name="numero">El número del caso.</param>
    /// <param name="personas">Cuántas personas lleva.</param>
    /// <param name="comentario">La nota del compañero de cada persona, o nula.</param>
    /// <returns>El id del caso creado.</returns>
    private static long Caso(AlmacenFalso almacen, string numero, int personas, string? comentario)
    {
        var casoId = almacen.SiguienteId();
        almacen.Casos[casoId] = new Caso
        {
            Id = casoId,
            NumeroCaso = numero,
            UnidadNombre = "Castries Branch",
            UnidadNumero = "700001",
            TemploNombre = "Santo Domingo Dominican Republic",
            FechaViaje = "2026-09-08",
            CreadoEn = $"{Hoy} 09:00:00",
            EstadoRecomendacion = "no_completa",
        };

        for (var cual = 1; cual <= personas; cual++)
        {
            var personaId = almacen.SiguienteId();
            almacen.Personas[personaId] = new Persona
            {
                Id = personaId,
                CasoId = casoId,
                Nombre = $"Persona {cual} de {numero}",
                Mrn = $"055-2000-{personaId:0000}",
                NotaCompanero = comentario,
            };
        }

        return casoId;
    }
}
