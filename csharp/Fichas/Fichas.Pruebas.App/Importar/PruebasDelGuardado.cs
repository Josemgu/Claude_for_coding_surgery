using Fichas.App.Importar;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Lo que tiene que pasar al guardar lo leido de un PDF, criterio a criterio.
/// </summary>
/// <remarks>
/// Cada prueba nace de una frase del dueno o de una decision escrita, no del codigo:
/// «si hay documentos duplicados debe decirlo y no rechazarlo», «ninguna pagina se
/// rechaza», «el numero no se inventa». La referencia es <c>importacion/guardado.py</c>,
/// que es la especificacion que se porta.
/// </remarks>
[TestClass]
public sealed class PruebasDelGuardado : BaseDeImportacion
{
    /// <summary>Una hoja con una persona deja un caso y una persona, y nada mas.</summary>
    [TestMethod]
    public void UnaHojaConUnaPersonaDejaUnCasoYUnaPersona()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
            [Hoja("C:/pdfs/uno.pdf", 1, "CASP2609", [("Ana Perez", "055-1111-3853")])]);

        Assert.HasCount(1, salida);
        Assert.IsTrue(salida[0].Entro);
        Assert.AreEqual(1, salida[0].Personas);
        Assert.IsNull(salida[0].DuplicadoDe);
        Assert.IsFalse(salida[0].PendienteDeIdentificar);
        Assert.AreEqual(1L, Contar("casos"));
        Assert.AreEqual(1L, Contar("personas"));
    }

    /// <summary>
    /// Dos hojas del mismo documento con el mismo numero y sin contradecirse son UN caso.
    /// </summary>
    /// <remarks>
    /// Es la opcion A de DECISIONES.md (2026-09-02): sin esto, el grupo de Surinam
    /// entraba con 1 persona de 12. Y las filas se corren, para que el orden del papel
    /// se conserve al comparar contra el escaneo.
    /// </remarks>
    [TestMethod]
    public void DosHojasDelMismoDocumentoConElMismoNumeroSeUnenEnUnCaso()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
        [
            Hoja("C:/pdfs/grupo.pdf", 1, "SURB2609", [("Uno", "055-0000-0001"), ("Dos", "055-0000-0002")]),
            Hoja("C:/pdfs/grupo.pdf", 2, "SURB2609", [("Tres", "055-0000-0003")]),
        ]);

        Assert.HasCount(2, salida);
        Assert.AreEqual(salida[0].CasoId, salida[1].CasoId);
        Assert.AreEqual(1L, Contar("casos"));
        Assert.AreEqual(3L, Contar("personas"));

        var filas = Datos.Personas.DeCaso(salida[0].CasoId!.Value)
            .Select(persona => persona.FilaFormulario).ToArray();
        CollectionAssert.AreEqual(new int?[] { 1, 2, 3 }, filas);
    }

    /// <summary>
    /// Una hoja que lee OTRA fecha no se une: abre su propio caso y deja su renglon.
    /// </summary>
    /// <remarks>
    /// El fallo que lo obliga esta medido: QA junto los dos PDF del dueno y salio «1
    /// caso, 5 personas» con una persona bajo la fecha de viaje de otra familia.
    /// </remarks>
    [TestMethod]
    public void UnaHojaQueContradiceLaFechaAbreCasoAparte()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
        [
            Hoja("C:/pdfs/dos.pdf", 1, "BARC2608", [("Uno", "055-0000-0001")], "2026-08-25"),
            Hoja("C:/pdfs/dos.pdf", 2, "BARC2608", [("Dos", "055-0000-0002")], "2026-08-26"),
        ]);

        Assert.AreNotEqual(salida[0].CasoId, salida[1].CasoId);
        Assert.AreEqual(2L, Contar("casos"));
        Assert.IsTrue(salida[1].Entro, "la hoja que contradice ENTRA; solo que aparte.");
        Assert.Contains(
            MotivosDeIlegible.HojaAparte,
            RenglonesDe("C:/pdfs/dos.pdf"));
    }

    /// <summary>
    /// El mismo documento importado dos veces entra otra vez, MARCADO, y nada se pisa.
    /// </summary>
    /// <remarks>
    /// Palabras del dueno el 2026-09-03: «si hay documentos duplicados debe decirlo y no
    /// rechazarlo». Se comprueban las tres mitades: que entra, que queda marcado, y que
    /// el caso viejo no cambio ni una columna.
    /// </remarks>
    [TestMethod]
    public void ElMismoDocumentoDosVecesEntraMarcadoComoDuplicado()
    {
        var primera = Guardado.GuardarLasHojasDelDocumento(
            [Hoja("C:/pdfs/uno.pdf", 1, "CASP2609", [("Ana Perez", "055-1111-3853")])]);
        var antes = Datos.Casos.Obtener(primera[0].CasoId!.Value);

        var segunda = Guardado.GuardarLasHojasDelDocumento(
            [Hoja("C:/pdfs/uno.pdf", 1, "CASP2609", [("Ana Perez", "055-1111-3853")])]);

        Assert.IsTrue(segunda[0].Entro, "un duplicado NO se rechaza.");
        Assert.AreEqual(primera[0].CasoId, segunda[0].DuplicadoDe);
        Assert.AreEqual(2L, Contar("casos"));

        var despues = Datos.Casos.Obtener(primera[0].CasoId!.Value);
        Assert.AreEqual(antes, despues, "el caso que ya estaba NO se toca.");
        Assert.Contains(MotivosDeIlegible.EntroComoDuplicado, RenglonesDe("C:/pdfs/uno.pdf"));
    }

    /// <summary>
    /// Un archivo copiado a otra ruta, con los mismos MRN, tambien se reconoce.
    /// </summary>
    [TestMethod]
    public void ElMismoDocumentoDesdeOtraRutaSeReconocePorElMrn()
    {
        var primera = Guardado.GuardarLasHojasDelDocumento(
            [Hoja("C:/pdfs/uno.pdf", 1, "CASP2609", [("Ana", "055-1111-3853")])]);
        var segunda = Guardado.GuardarLasHojasDelDocumento(
            [Hoja("D:/copia/uno.pdf", 1, "CASP2609", [("Ana", "055-1111-3853")])]);

        Assert.AreEqual(primera[0].CasoId, segunda[0].DuplicadoDe);
    }

    /// <summary>
    /// Dos familias distintas con el mismo numero y sin MRN comun NO son duplicados.
    /// </summary>
    /// <remarks>
    /// Es el reverso de la prueba anterior y la mitad que la hace util: el numero de caso
    /// son cuatro letras mas el ano y el mes, asi que lo comparten familias distintas de
    /// la misma unidad. Sin este control positivo, un detector que marcara TODO como
    /// duplicado pasaria la prueba de arriba.
    /// </remarks>
    [TestMethod]
    public void DosFamiliasConElMismoNumeroYSinMrnComunNoSonDuplicados()
    {
        Guardado.GuardarLasHojasDelDocumento(
            [Hoja("C:/pdfs/uno.pdf", 1, "CASP2609", [("Ana", "055-1111-3853")])]);
        var segunda = Guardado.GuardarLasHojasDelDocumento(
            [Hoja("C:/pdfs/dos.pdf", 1, "CASP2609", [("Luis", "055-9999-1111")])]);

        Assert.IsNull(segunda[0].DuplicadoDe);
        Assert.AreEqual(2L, Contar("casos"));
    }

    /// <summary>
    /// Una hoja sin numero de caso entra igual, con todo lo demas, y queda pendiente.
    /// </summary>
    /// <remarks>
    /// El dueno lo midio con sus PDF: «Se guardaron 0 casos de 1 página, con 0 personas».
    /// El numero no se inventa (regla permanente 1): se teclea a mano despues.
    /// </remarks>
    [TestMethod]
    public void UnaHojaSinNumeroDeCasoEntraYQuedaPendienteDeIdentificar()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
            [Hoja("C:/pdfs/sin.pdf", 1, null, [("Ana", "055-1111-3853")])]);

        Assert.IsTrue(salida[0].Entro);
        Assert.IsTrue(salida[0].PendienteDeIdentificar);
        Assert.AreEqual(1L, Contar("personas"));
        Assert.IsNull(Datos.Casos.Obtener(salida[0].CasoId!.Value)!.NumeroCaso);
        Assert.Contains(MotivosDeIlegible.SinNumeroDeCaso, RenglonesDe("C:/pdfs/sin.pdf"));
    }

    /// <summary>
    /// Dos hojas SIN numero del mismo documento son dos casos, no uno.
    /// </summary>
    /// <remarks>
    /// «No se sabe el numero» no es un numero comun: unirlas dejaria dos familias
    /// revueltas en un caso, y ninguna funcion mueve una persona de caso despues.
    /// </remarks>
    [TestMethod]
    public void DosHojasSinNumeroDelMismoDocumentoNoSeUnen()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
        [
            Hoja("C:/pdfs/sin.pdf", 1, null, [("Ana", "055-0000-0001")]),
            Hoja("C:/pdfs/sin.pdf", 2, null, [("Luis", "055-0000-0002")]),
        ]);

        Assert.AreNotEqual(salida[0].CasoId, salida[1].CasoId);
        Assert.AreEqual(2L, Contar("casos"));
    }

    /// <summary>
    /// Un numero mal leido entra TAL CUAL: no se pierde el documento y no se corrige solo.
    /// </summary>
    /// <remarks>
    /// ⚠️ Esta prueba se escribio primero contra otra premisa —que el esquema rechazaba el
    /// numero mal formado— y FALLO. Medido despues sobre el motor: la migracion 12 de
    /// <c>Fichas.Datos</c> reconstruye <c>casos</c> «sin el UNIQUE y sin el CHECK de forma
    /// del numero», asi que sobre una base creada por el C# `CASP26O9` entra tal cual.
    ///
    /// <para>Lo que la prueba fija es lo que importa y no depende del CHECK: el documento
    /// NO se pierde, y el programa NO arregla el numero (regla permanente 1). Un `CASP26O9`
    /// convertido a `CASP2609` por la maquina seria un dato inventado.</para>
    ///
    /// <para>La otra mitad —que pasa si la base SI lleva ese CHECK, como la del dueno—
    /// la mide <see cref="PruebasDelNumeroQueLaBaseRechaza"/>.</para>
    /// </remarks>
    [TestMethod]
    public void UnNumeroMalLeidoEntraTalCualYNoSeCorrigeSolo()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
            [Hoja("C:/pdfs/malo.pdf", 1, "CASP26O9", [("Ana", "055-1111-3853")])]);

        Assert.IsTrue(salida[0].Entro, "el documento NO se pierde por un numero mal leido.");
        Assert.AreEqual(1L, Contar("casos"));
        Assert.AreEqual(1L, Contar("personas"));
        Assert.AreEqual("CASP26O9", Datos.Casos.Obtener(salida[0].CasoId!.Value)!.NumeroCaso,
            "se guarda lo que se leyo; el programa no lo arregla.");
    }

    /// <summary>Cada campo del caso deja su procedencia, y ninguna nace verificada.</summary>
    /// <remarks>
    /// Regla permanente 5: el sistema propone, Miguel firma. Siempre.
    /// <para>
    /// ⚠️ <b>Eran cuatro y desde el 2026-09-05 son cinco.</b> Esta prueba fijaba en verde
    /// que <c>templo_nombre</c> NO dejaba fila, que es justo el defecto: sin fila, ese campo
    /// no se puede dar por bueno —<c>Firmar</c> es un <c>UPDATE</c> y cambia cero filas—.
    /// Medido sobre los siete escaneos reales antes del arreglo: 0 de 7 casos con esa fila.
    /// La prueba que impide que las dos listas vuelvan a separarse esta en
    /// <see cref="PruebasDeLaProcedenciaDeTodosLosCampos"/>.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void CadaCampoDejaSuProcedenciaYNingunaNaceVerificada()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
            [Hoja("C:/pdfs/uno.pdf", 1, "CASP2609", [("Ana", "055-1111-3853")])]);

        var delCaso = Datos.Procedencia.DeRegistro(TablaDeProcedencia.Casos, salida[0].CasoId!.Value);
        Assert.HasCount(5, delCaso, "numero_caso, fecha_viaje, unidad_numero, unidad_nombre y templo_nombre.");
        Assert.IsFalse(delCaso.Any(fila => fila.Verificado));
        Assert.AreEqual("CASP2609", delCaso.Single(fila => fila.Campo == "numero_caso").ValorOcr);

        var persona = Datos.Personas.DeCaso(salida[0].CasoId!.Value)[0];
        var deLaPersona = Datos.Procedencia.DeRegistro(TablaDeProcedencia.Personas, persona.Id);
        Assert.HasCount(2, deLaPersona, "nombre y mrn.");
        Assert.AreEqual(0, Datos.Procedencia.ContarVerificados(TablaDeProcedencia.Personas, persona.Id));
    }

    /// <summary>Una hoja que no se pudo leer deja su renglon y no tira nada.</summary>
    [TestMethod]
    public void UnaHojaIlegibleDejaSuRenglonYLaTandaSigue()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
        [
            HojaIlegible("C:/pdfs/roto.pdf", "El archivo no se pudo abrir como PDF."),
        ]);

        Assert.IsFalse(salida[0].Entro);
        Assert.AreEqual(0L, Contar("casos"));
        Assert.Contains(MotivosDeIlegible.NoSePudoAbrir, RenglonesDe("C:/pdfs/roto.pdf"));
    }

    /// <summary>Un MRN repetido dentro del mismo caso avisa; no levanta ni tumba la hoja.</summary>
    [TestMethod]
    public void UnMrnRepetidoEnElMismoCasoAvisaYNoTumbaLaHoja()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
        [
            Hoja("C:/pdfs/uno.pdf", 1, "CASP2609",
                 [("Ana", "055-1111-3853"), ("Ana otra vez", "055-1111-3853")]),
        ]);

        Assert.IsTrue(salida[0].Entro);
        Assert.AreEqual(1, salida[0].Personas, "la segunda choca con UNIQUE (caso_id, mrn).");
        Assert.IsNotEmpty(salida[0].Avisos, "descartar una fila se dice, no se calla.");
    }

    /// <summary>Los motivos de los renglones que dejo un archivo.</summary>
    private string[] RenglonesDe(string rutaPdf)
        => Datos.Ilegibles
            .Listar(new Contratos.Consultas.FiltroDeIlegibles(RutaPdf: rutaPdf),
                    Contratos.Consultas.Pagina.Primera(50))
            .Elementos.Select(renglon => renglon.Motivo).ToArray();
}
