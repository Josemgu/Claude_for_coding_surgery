using ClosedXML.Excel;
using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// Qué pasa cuando el compañero estropea la columna «clave», ahora que nada va bloqueado.
/// </summary>
/// <remarks>
/// <para>Estas pruebas existen por lo único que el desbloqueo del 2026-09-07 puede romper. El
/// bloqueo de <c>numero_caso</c> y <c>mrn</c> protegía el par que reconciliaba; desde el
/// 2026-09-03 quien reconcilia es la columna <c>clave</c>, así que lo que hay que medir ya no
/// es «¿y si toca el MRN?» sino «¿y si toca la clave?».</para>
///
/// <para><b>Las tres averías son distintas y no se pueden confundir:</b></para>
/// <list type="number">
/// <item><b>Celda de la clave vacía</b> — la columna sigue ahí y ese renglón no dice de quién
/// es: <c>Motivos.ClaveBorrada</c>.</item>
/// <item><b>Celda de la clave con cualquier otra cosa</b> — <c>Motivos.ClaveRota</c>, y el
/// motivo lleva dentro lo que venía escrito.</item>
/// <item><b>La columna entera borrada</b> — y aquí lo medido desmintió lo que se esperaba: no
/// se cae al par suelto, se pierde la hoja ENTERA. El rótulo «clave» es también lo que marca
/// dónde empieza la tabla, así que sin él se leen las líneas de cabecera como personas. Desde
/// el 2026-09-07 eso se dice con un aviso propio.</item>
/// </list>
///
/// <para>⛔ En ninguna de las tres se empareja por nombre y en ninguna se descarta en silencio:
/// cada fila que no entra deja su renglón con su número de fila y su motivo escrito.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaClaveEstropeada
{
    /// <summary>La base en memoria de esta prueba, montada en <see cref="Preparar"/>.</summary>
    private BaseInventada _base = null!;
    /// <summary>La carpeta temporal donde se escribe el <c>.xlsx</c>; se borra en <see cref="Recoger"/>.</summary>
    private string _carpeta = null!;
    /// <summary>El número interno del compañero de prueba al que se le genera el paquete.</summary>
    private long _sandy;

    /// <summary>Monta la base en memoria y la carpeta temporal de esta prueba.</summary>
    [TestInitialize]
    public void Preparar()
    {
        _base = new BaseInventada();
        _carpeta = BaseInventada.CarpetaDePruebas();
        _sandy = _base.Companero("Agente de prueba uno");
    }

    /// <summary>Borra la carpeta temporal; ningún .xlsx se queda en el disco.</summary>
    [TestCleanup]
    public void Recoger()
    {
        try { Directory.Delete(_carpeta, recursive: true); }
        catch (IOException) { /* si Windows todavía tiene la manija, la limpia el sistema */ }
    }

    /// <summary>
    /// Una hoja rota a propósito: de tres filas, la buena entra y las dos rotas no, cada una
    /// con SU motivo.
    /// </summary>
    /// <remarks>
    /// Las tres van en el MISMO archivo a propósito: es lo que de verdad devuelve un compañero,
    /// y así se ve que una fila rota no arrastra a las de al lado.
    /// </remarks>
    [TestMethod]
    public void DeTresFilasConDosClavesEstropeadasEntraUnaYLasOtrasDicenPorQueNo()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Persona de prueba A", "055-1111-3851", 1);
        _base.Persona(caso, "Persona de prueba B", "055-1111-3852", 2);
        _base.Persona(caso, "Persona de prueba C", "055-1111-3853", 3);
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());

        Contestar(7, "Sí");
        Contestar(8, "Sí");
        Contestar(9, "Sí");
        EscribirEnLaClave(8, null);
        EscribirEnLaClave(9, "7000014");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);

        Assert.HasCount(1, vuelta.Marcas, "solo la fila con la clave intacta puede entrar");
        Assert.AreEqual("055-1111-3851", vuelta.Marcas[0].Mrn);
        Assert.HasCount(2, vuelta.Descartadas);

        var borrada = vuelta.Descartadas.Single(fila => fila.FilaExcel == 8);
        StringAssert.Contains(borrada.Motivo, "no trae nada en la columna «clave»");
        StringAssert.Contains(borrada.Motivo, "nunca se empareja por nombre");

        var rota = vuelta.Descartadas.Single(fila => fila.FilaExcel == 9);
        StringAssert.Contains(rota.Motivo, "no tiene la forma esperada");
        StringAssert.Contains(rota.Motivo, "7000014", "el motivo dice lo que venía escrito");
    }

    /// <summary>
    /// ⚠️ Lo que sostiene el desbloqueo: con la clave puesta, un MRN retecleado NO tira la fila.
    /// </summary>
    /// <remarks>
    /// Es la prueba del pase: el bloqueo protegía el par <c>numero_caso</c> + <c>mrn</c>, y con
    /// la clave delante ese par ya no decide. Si esta prueba se pusiera en rojo, quitar el
    /// bloqueo habría dejado de ser seguro y habría que devolvérselo al dueño.
    /// </remarks>
    [TestMethod]
    public void ConLaClavePuestaUnMrnRetecleadoAManoNoTiraLaFila()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Persona de prueba A", "055-1111-3851", 1);
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());

        Contestar(7, "Sí");
        Escribir(7, "mrn", "999-9999-9999");
        Escribir(7, "numero_caso", "OTRO9999");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);

        Assert.IsEmpty(vuelta.Descartadas, string.Join(" | ", vuelta.Descartadas.Select(f => f.Motivo)));
        Assert.HasCount(1, vuelta.Marcas, "la clave manda: lo tecleado encima del caso y del MRN no decide nada");
        Assert.AreEqual("055-1111-3851", vuelta.Marcas[0].Mrn, "el MRN que vuelve es el de la clave, no el retecleado");
    }

    /// <summary>
    /// ⚠️ Borrar la COLUMNA entera pierde la hoja completa, y ahora se dice con esas palabras.
    /// </summary>
    /// <remarks>
    /// <para><b>Esto es lo medido, no lo que se suponía.</b> El primer intento de esta prueba
    /// esperaba que la vuelta cayera al par <c>numero_caso</c> + <c>mrn</c> y que la fila
    /// entrara igual. No es lo que pasa: <c>LectorDeExcel.BuscarLaFilaDeTitulos</c> localiza la
    /// fila de títulos buscando el rótulo «clave», así que sin esa columna no encuentra la fila
    /// 6, cae a la 1 y lee las cinco líneas de cabecera como si fueran personas. De una hoja de
    /// UNA persona salen SEIS filas descartadas.</para>
    ///
    /// <para>Lo que la prueba fija es que <b>nada se descarta en silencio</b> y que la causa se
    /// dice con su nombre: sin el aviso, Miguel vería seis motivos que hablan de un número de
    /// caso que sí estaba escrito, y no habría forma de llegar a la causa.</para>
    /// </remarks>
    [TestMethod]
    public void BorrarLaColumnaDeLaClaveEnteraTiraLaHojaYElAvisoDiceQueFueEso()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Persona de prueba A", "055-1111-3851", 1);
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());

        Contestar(7, "Sí");
        BorrarLaColumnaDeLaClave();

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);

        Assert.IsEmpty(vuelta.Marcas, "sin la columna «clave» no se aplica ni una fila");
        Assert.IsNotEmpty(vuelta.Descartadas, "ninguna se pierde en silencio: cada una deja su renglón");
        Assert.IsTrue(
            vuelta.Descartadas.All(fila => !string.IsNullOrWhiteSpace(fila.Motivo)),
            "toda fila descartada lleva su motivo escrito");

        var aviso = vuelta.Avisos.SingleOrDefault(a => a.Linea.Contains("no trae la columna «clave»"));
        Assert.IsNotNull(aviso, "tiene que decirse por qué se perdió la hoja entera, no solo que se perdió");
        StringAssert.Contains(aviso.Detalle!, "vuelva a generarle el paquete");
    }

    /// <summary>
    /// Con la columna «clave» puesta no salta ese aviso: no se avisa de lo que no pasa.
    /// </summary>
    [TestMethod]
    public void ConLaColumnaDeLaClavePuestaNoSaltaEseAviso()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Persona de prueba A", "055-1111-3851", 1);
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(7, "Sí");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);

        Assert.IsFalse(
            vuelta.Avisos.Any(a => a.Linea.Contains("no trae la columna «clave»")),
            "la hoja la trae: avisar aquí sería ruido");
    }

    // ---- el montaje ---------------------------------------------------------

    /// <summary>La ruta del único <c>.xlsx</c> de la prueba, en su carpeta temporal.</summary>
    private string Ruta() => Path.Combine(_carpeta, "por_verificar.xlsx");

    /// <summary>Contesta las siete casillas de una fila, como haría el agente.</summary>
    /// <param name="filaExcel">La fila de Excel, base 1; la primera persona va en la 7.</param>
    /// <param name="respuesta">Lo que se escribe en las siete celdas.</param>
    private void Contestar(int filaExcel, string respuesta)
    {
        using var libro = new XLWorkbook(Ruta());
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        foreach (var paso in Pasos.Todos)
            hoja.Cell(filaExcel, Columnas.IndiceDe(paso.Nombre)).SetValue(respuesta);
        hoja.Cell(filaExcel, Columnas.IndiceDe(Pasos.ColumnaDeLaLlamada)).SetValue(respuesta);
        libro.Save();
    }

    /// <summary>Escribe —o vacía, con nulo— una celda cualquiera de una fila.</summary>
    /// <param name="filaExcel">La fila de Excel, base 1.</param>
    /// <param name="nombreDeColumna">El nombre de la columna en la base, no su título.</param>
    /// <param name="valor">El texto, o nulo para vaciar la celda.</param>
    private void Escribir(int filaExcel, string nombreDeColumna, string? valor)
    {
        using var libro = new XLWorkbook(Ruta());
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        var celda = hoja.Cell(filaExcel, Columnas.IndiceDe(nombreDeColumna));
        if (valor is null) celda.Clear(XLClearOptions.Contents); else celda.SetValue(valor);
        libro.Save();
    }

    /// <summary>Estropea la celda de la clave de una fila: vacía con nulo, rota con texto.</summary>
    /// <param name="filaExcel">La fila de Excel, base 1.</param>
    /// <param name="valor">Nulo para borrar la clave; cualquier texto sin la forma para romperla.</param>
    private void EscribirEnLaClave(int filaExcel, string? valor)
        => Escribir(filaExcel, Columnas.ColumnaDeLaClave, valor);

    /// <summary>
    /// Borra la COLUMNA entera de la clave, título incluido: es otra avería, no la misma.
    /// </summary>
    /// <remarks>
    /// Con la celda vacía la columna sigue existiendo y la vuelta sabe que falta un dato. Sin
    /// la columna, la vuelta ni siquiera sabe que existió, y por eso cae al par suelto.
    /// </remarks>
    private void BorrarLaColumnaDeLaClave()
    {
        using var libro = new XLWorkbook(Ruta());
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        hoja.Column(Columnas.IndiceDe(Columnas.ColumnaDeLaClave)).Delete();
        libro.Save();
    }
}
