using Fichas.App.Correccion;
using Fichas.App.Importar;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Todo campo que la pantalla de correccion ensena sale de la importacion CON su fila.
/// </summary>
/// <remarks>
/// <para>El defecto que cierra, medido el 2026-09-05 con la ventana abierta sobre los siete
/// escaneos reales del dueno, importados en una carpeta de datos propia:</para>
/// <code>
/// numero_caso      con fila: 7/7
/// unidad_numero    con fila: 7/7
/// unidad_nombre    con fila: 7/7
/// fecha_viaje      con fila: 7/7
/// templo_nombre    con fila: 0/7    &lt;-- este
/// </code>
/// <para><c>IProcedencia.Firmar</c> de verdad es un <c>UPDATE</c>: sin fila cambia cero filas
/// y contesta «No se encontro la fila que se queria cambiar», que ademas no dice de que campo
/// habla. O sea que el unico campo que el programa NO habia leido era tambien el unico que no
/// se podia dar por bueno.</para>
/// <para>⛔ Y la fila nace SIN firma, como todas: <c>verificado</c> se queda en cero (regla
/// permanente 5). Eso se comprueba aqui tambien, porque es justo lo que un arreglo apurado
/// podria romper.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaProcedenciaDeTodosLosCampos : BaseDeImportacion
{
    /// <summary>
    /// Dado un documento leido, cuando se guarda, entonces los CINCO campos del caso
    /// tienen su fila de procedencia y ninguno nace firmado.
    /// </summary>
    [TestMethod]
    public void LosCincoCamposDelCasoDejanSuFilaDeProcedencia()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
            [Hoja("C:/pdfs/uno.pdf", 1, "CASP2609", [("Ana Prueba", "055-1111-3853")])]);

        var casoId = salida[0].CasoId!.Value;
        var filas = Datos.Procedencia.DeRegistro(TablaDeProcedencia.Casos, casoId);

        CollectionAssert.AreEquivalent(
            new[]
            {
                CamposDeLaHoja.CampoNumeroCaso, CamposDeLaHoja.CampoFechaViaje,
                CamposDeLaHoja.CampoUnidadNumero, CamposDeLaHoja.CampoUnidadNombre,
                CamposDeLaHoja.CampoTemploNombre,
            },
            filas.Select(fila => fila.Campo).ToArray());

        Assert.IsEmpty(filas.Where(fila => fila.Verificado), "Ninguna fila nace firmada.");
    }

    /// <summary>
    /// El templo deja su fila con el valor que se leyo, no con un hueco.
    /// </summary>
    /// <remarks>
    /// La fila sola no basta: si naciera vacia, la pantalla diria «sin lectura guardada»
    /// sobre un campo que SI se leyo, y el documento saldria pidiendo comprobar algo que
    /// ya estaba bien.
    /// </remarks>
    [TestMethod]
    public void LaFilaDelTemploGuardaLoQueSeLeyo()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
            [Hoja("C:/pdfs/uno.pdf", 1, "CASP2609", [("Ana Prueba", "055-1111-3853")],
                  temploNombre: "Panama City, Panama")]);

        var templo = Datos.Procedencia
            .DeRegistro(TablaDeProcedencia.Casos, salida[0].CasoId!.Value)
            .Single(fila => fila.Campo == CamposDeLaHoja.CampoTemploNombre);

        Assert.AreEqual(OrigenDeCampo.Ocr, templo.Origen);
        Assert.AreEqual("Panama City, Panama", templo.ValorOcr);
        Assert.IsFalse(templo.AusenteEnElPapel, "Un campo que SI se leyo no esta ausente del papel.");
    }

    /// <summary>
    /// Los campos que la pantalla de correccion dibuja y los que la importacion deja con
    /// fila son la MISMA lista. Ni uno de mas ni uno de menos.
    /// </summary>
    /// <remarks>
    /// Es la prueba que impide que esto vuelva a pasar: el defecto no fue un campo olvidado,
    /// fue que dos listas de campos vivian en dos archivos y nadie las comparaba. Si manana
    /// alguien anade un sexto campo a la pantalla y no a la importacion, esto se pone rojo el
    /// mismo dia y no dentro de un mes con el dueno delante.
    /// </remarks>
    [TestMethod]
    public void LaPantallaYLaImportacionHablanDeLosMismosCampos()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
            [Hoja("C:/pdfs/uno.pdf", 1, "CASP2609", [("Ana Prueba", "055-1111-3853")])]);

        var conFila = Datos.Procedencia
            .DeRegistro(TablaDeProcedencia.Casos, salida[0].CasoId!.Value)
            .Select(fila => fila.Campo)
            .OrderBy(campo => campo, StringComparer.Ordinal)
            .ToArray();

        var enPantalla = ModeloDeCorreccion.CamposDelCasoQueSeDibujan
            .OrderBy(campo => campo, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(enPantalla, conFila);
    }

    /// <summary>Los dos campos de cada persona tambien dejan su fila, y sin firma.</summary>
    [TestMethod]
    public void LosDosCamposDeLaPersonaDejanSuFilaDeProcedencia()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
            [Hoja("C:/pdfs/uno.pdf", 1, "CASP2609", [("Ana Prueba", "055-1111-3853")])]);

        var persona = Datos.Personas.DeCaso(salida[0].CasoId!.Value).Single();
        var filas = Datos.Procedencia.DeRegistro(TablaDeProcedencia.Personas, persona.Id);

        CollectionAssert.AreEquivalent(
            new[] { CamposDeLaHoja.CampoNombre, CamposDeLaHoja.CampoMrn },
            filas.Select(fila => fila.Campo).ToArray());
        Assert.IsEmpty(filas.Where(fila => fila.Verificado));
    }
}
