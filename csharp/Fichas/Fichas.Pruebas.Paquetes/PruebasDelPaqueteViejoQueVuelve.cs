using ClosedXML.Excel;
using Fichas.Contratos.Modelos;
using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// Un paquete generado ANTES del 2026-09-06 —con sus 18 columnas— tiene que seguir volviendo.
/// </summary>
/// <remarks>
/// El dueno quito «Fecha de solicitud» y «Estaca o distrito» el 2026-09-06 (DECISIONES.md).
/// El dia del cambio hay paquetes de 18 columnas en manos de companeros, y ese trabajo ya esta
/// hecho: si la vuelta dejara de leerlos, se perderia entero y sin avisar.
/// <para>
/// ⚠️ La hoja de estas pruebas se arma A MANO, con los 18 rotulos escritos aqui letra por
/// letra, y NO con <see cref="Columnas"/>. Es lo que hace que la prueba sirva: una hoja
/// construida con el codigo de hoy se encogeria sola a 16 columnas el dia que alguien cambie
/// la lista, y entonces esta prueba pasaria sin haber probado nada. Los 18 rotulos son los que
/// se midieron sobre un `.xlsx` generado antes del cambio.
/// </para>
/// </remarks>
[TestClass]
public class PruebasDelPaqueteViejoQueVuelve
{
    /// <summary>Los 18 rotulos de la hoja vieja, en su orden, copiados de un archivo real.</summary>
    private static readonly string[] LosDieciochoDeAntes =
    [
        "Caso", "Fecha de solicitud", "Fecha de viaje", "Barrio o rama",
        "Estaca o distrito", "Hermano(a) que viaja", "Cédula de miembro", "A qué va",
        "1. Preparación", "2. Información", "3. Cita del templo",
        "4. Acciones requeridas", "5. Entrevistas", "6. Listo para el templo",
        "¿Llamó al líder?", "¿Por qué no se completó?", "Comentario", "clave",
    ];

    /// <summary>La base en memoria de esta prueba, montada en <see cref="Preparar"/>.</summary>
    private BaseInventada _base = null!;
    /// <summary>La carpeta temporal donde se escriben los <c>.xlsx</c>; se borra en <see cref="Recoger"/>.</summary>
    private string _carpeta = null!;
    /// <summary>El número interno del compañero de prueba al que se le genera el paquete.</summary>
    private long _sandy;

    /// <summary>Monta la base en memoria, la carpeta temporal y el compañero de cada prueba.</summary>
    [TestInitialize]
    public void Preparar()
    {
        _base = new BaseInventada();
        _carpeta = BaseInventada.CarpetaDePruebas();
        _sandy = _base.Companero("Sandy");
    }

    /// <summary>Borra la carpeta temporal; ningún <c>.xlsx</c> se queda en el disco.</summary>
    [TestCleanup]
    public void Recoger()
    {
        try { Directory.Delete(_carpeta, recursive: true); }
        catch (IOException) { /* si Windows todavia tiene la manija, la carpeta temporal la limpia el sistema */ }
    }

    /// <summary>La ruta de un archivo dentro de la carpeta temporal de la prueba.</summary>
    /// <param name="nombre">El nombre del archivo; por defecto el del paquete viejo.</param>
    private string Ruta(string nombre = "paquete_viejo.xlsx") => Path.Combine(_carpeta, nombre);

    /// <summary>
    /// Escribe una hoja «Por verificar» con los 18 rotulos de antes y una fila por persona.
    /// </summary>
    /// <remarks>
    /// Reproduce la forma del archivo viejo en lo que la vuelta mira: el nombre de la pestana,
    /// las cinco lineas de cabecera encima, los rotulos en la fila 6 y la clave a la vista en la
    /// ultima columna. Lo demas —colores, anchos, menus— no lo lee nadie al volver.
    /// </remarks>
    /// <param name="ruta">Dónde dejar el <c>.xlsx</c>.</param>
    /// <param name="gente">Una entrada por persona: caso, MRN, id del caso y nombre, en el orden de las filas.</param>
    private void EscribirHojaDeDieciocho(string ruta, IReadOnlyList<(string Caso, string Mrn, long CasoId, string Nombre)> gente)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet(Columnas.NombreDeLaHoja);
        hoja.Cell(1, 1).Value = "Preparación para las ordenanzas";
        hoja.Cell(4, 1).Value = "Agente: Sandy";

        for (var columna = 1; columna <= LosDieciochoDeAntes.Length; columna++)
            hoja.Cell(Columnas.FilaDeLaCabecera, columna).Value = LosDieciochoDeAntes[columna - 1];

        for (var indice = 0; indice < gente.Count; indice++)
        {
            var (caso, mrn, casoId, nombre) = gente[indice];
            var fila = Columnas.PrimeraFilaDeDatos + indice;
            hoja.Cell(fila, 1).SetValue(caso);
            // Las dos columnas que el dueno quito iban con «no consta», que es como salian.
            hoja.Cell(fila, 2).SetValue(Columnas.SinDato);
            hoja.Cell(fila, 3).SetValue("2026-10-15");
            hoja.Cell(fila, 4).SetValue("Cuatricentenaria (7000014)");
            hoja.Cell(fila, 5).SetValue(Columnas.SinDato);
            hoja.Cell(fila, 6).SetValue(nombre);
            hoja.Cell(fila, 7).SetValue(mrn);
            hoja.Cell(fila, 8).SetValue("Investidura");
            hoja.Cell(fila, 18).SetValue(Columnas.ArmarLaClave(caso, mrn, casoId));
        }
        libro.SaveAs(ruta);
    }

    /// <summary>Rellena las siete de si o no de una fila, en las posiciones de la hoja vieja.</summary>
    /// <param name="ruta">El <c>.xlsx</c> a modificar.</param>
    /// <param name="filaExcel">La fila de Excel, base 1.</param>
    /// <param name="respuesta">Lo que se escribe en los seis pasos, columnas 9 a 14.</param>
    /// <param name="llamo">Lo que se escribe en «¿Llamó al líder?», columna 15.</param>
    private static void ContestarEnLaHojaVieja(string ruta, int filaExcel, string respuesta, string llamo)
    {
        using var libro = new XLWorkbook(ruta);
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        for (var columna = 9; columna <= 14; columna++)
            hoja.Cell(filaExcel, columna).SetValue(respuesta);
        hoja.Cell(filaExcel, 15).SetValue(llamo);
        libro.Save();
    }

    /// <summary>
    /// Dado un paquete de 18 columnas que ya esta en manos de un companero, cuando lo devuelve
    /// relleno, entonces se reconcilia entero y no se descarta ni una fila.
    /// </summary>
    [TestMethod]
    public void UnPaqueteDeDieciochoColumnasSigueVolviendoEntero()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena Rosa Muestra", "055-1111-385A", 1);
        _base.Persona(caso, "Julia Luz Inventada", "055-1111-3853", 2);
        EscribirHojaDeDieciocho(Ruta(),
        [
            ("BALC2609", "055-1111-385A", caso, "Elena Rosa Muestra"),
            ("BALC2609", "055-1111-3853", caso, "Julia Luz Inventada"),
        ]);
        ContestarEnLaHojaVieja(Ruta(), 7, "Sí", "No");
        ContestarEnLaHojaVieja(Ruta(), 8, "Sí", "No");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);

        Assert.IsEmpty(vuelta.Descartadas, string.Join(" | ", vuelta.Descartadas.Select(d => d.Motivo)));
        Assert.HasCount(2, vuelta.Marcas, "las dos filas del paquete viejo tienen que casar");
        CollectionAssert.AreEquivalent(
            new[] { "055-1111-385A", "055-1111-3853" },
            vuelta.Marcas.Select(m => m.Mrn).ToArray(),
            "el trabajo del companero no se pierde porque la hoja traiga dos columnas de más");
        Assert.IsTrue(vuelta.Marcas.All(m => m.PasoPreparacion == true && m.PasoListoParaElTemplo == true));
        Assert.IsTrue(vuelta.Marcas.All(m => m.LlamoAlLider == false));
    }

    /// <summary>
    /// Las dos columnas que sobran NO se leen como otra cosa: son ruido y se ignoran.
    /// </summary>
    /// <remarks>
    /// El peligro concreto de quitar columnas de en medio es que la vuelta las lea por posicion:
    /// entonces «no consta» de la columna 5 caeria dentro de «Hermano(a) que viaja» y todo se
    /// correria un puesto. La vuelta las lee por ROTULO, y esta prueba lo fija: el nombre que
    /// vuelve es el nombre, no el «no consta» de la estaca.
    /// </remarks>
    [TestMethod]
    public void LasDosColumnasQueSobranNoSeCorrenSobreLasDeAlLado()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena Rosa Muestra", "055-1111-385A", 1);
        EscribirHojaDeDieciocho(Ruta(), [("BALC2609", "055-1111-385A", caso, "Elena Rosa Muestra")]);
        ContestarEnLaHojaVieja(Ruta(), 7, "Sí", "No");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);

        Assert.HasCount(1, vuelta.Marcas);
        Assert.AreEqual("BALC2609", vuelta.Marcas[0].NumeroCaso, "el caso sigue siendo el caso");
        Assert.AreEqual("055-1111-385A", vuelta.Marcas[0].Mrn, "la cédula sigue siendo la cédula");
    }

    /// <summary>
    /// Una hoja vieja que ademas trae el motivo escrito lo devuelve: la columna del motivo
    /// existe en las dos hojas y no la mueve el cambio del 2026-09-06.
    /// </summary>
    [TestMethod]
    public void ElMotivoEscritoEnUnaHojaViejaSigueLlegando()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena Rosa Muestra", "055-1111-385A", 1);
        EscribirHojaDeDieciocho(Ruta(), [("BALC2609", "055-1111-385A", caso, "Elena Rosa Muestra")]);
        using (var libro = new XLWorkbook(Ruta()))
        {
            var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
            hoja.Cell(7, 16).SetValue(MotivosDeLaHoja.Opciones[1]);
            hoja.Cell(7, 17).SetValue("Llamé tres veces y no contestó.");
            libro.Save();
        }

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);

        Assert.HasCount(1, vuelta.Marcas);
        Assert.AreEqual(MotivoDeNoCompletar.ElLiderNoLoHizo, vuelta.Marcas[0].Motivo);
        Assert.AreEqual("Llamé tres veces y no contestó.", vuelta.Marcas[0].NotaCompanero);
    }
}
