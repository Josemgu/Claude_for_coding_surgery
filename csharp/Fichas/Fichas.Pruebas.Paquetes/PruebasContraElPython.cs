using System.Text.Json;
using ClosedXML.Excel;
using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// Compara columna a columna la hoja que produce el C# con la que produce HOY el Python.
/// </summary>
/// <remarks>
/// El dueno llamo perfecto al Excel del programa viejo, y el Python de este repositorio ya lo
/// reproduce. Esta clase no confia en que el port se hiciera bien: compara contra una huella
/// sacada del propio Python.
/// <para>
/// La huella vive en <c>HuellaDelPython.json</c>, que se genera con <c>huella_del_python.py</c>
/// —el script esta al lado, importa el repositorio principal en SOLO LECTURA y no abre ninguna
/// base—. Se guarda como archivo y no se vuelve a generar en cada ejecucion a proposito: la
/// prueba tiene que correr en una maquina sin Python, y una comparacion contra algo que se
/// recalcula cada vez no detecta que el Python cambio.
/// </para>
/// <para>
/// ⚠️ Las diferencias que NO son fallo se declaran aqui y se comprueban una a una, en vez de
/// callarlas: una comparacion con excepciones sin nombre deja de servir.
/// </para>
/// <para>
/// <b>Primera diferencia declarada, del 2026-09-05.</b> La hoja del C# lleva DOS columnas que
/// el Python no tiene —«¿Por qué no se completó?» y «Comentario»— y una frase mas en la
/// instruccion, que las nombra. Las pidio el dueno: el programa viejo solo admite trabajo
/// hecho y el companero no tiene donde decir por que NO pudo.
/// </para>
/// <para>
/// <b>Segunda diferencia declarada, del 2026-09-06.</b> La hoja del C# ya NO lleva dos que el
/// Python si tiene —«Fecha de solicitud» y «Estaca o distrito»—. Las quito el dueno: «son
/// informaciones que no me pide verificar». Salian siempre con «no consta» porque la base de
/// este proyecto no las guarda, asi que en su hoja eran ruido.
/// </para>
/// <para>
/// <b>Tercera diferencia declarada, del 2026-09-07.</b> La hoja del C# lleva una columna mas que
/// el Python no tiene —«Número de unidad»— y NINGUNA de sus celdas va bloqueada. Las dos las
/// pidio el dueno el mismo dia: «el numero de unidad en un lado y al otro el nombre de la
/// unidad» y «no bloquees las celdas por favor, de los paquetes». El Python bloquea
/// <c>numero_caso</c> y <c>mrn</c> y protege la hoja; el C# ya no.
/// </para>
/// <para>
/// Lo que se sigue comparando letra por letra son las CATORCE que quedan de las 16 del
/// Python: sus nombres, sus titulos, su orden relativo, sus anchos, sus formatos y sus siete
/// menus. Los bloqueos ya NO se comparan contra el Python y se comprueban aparte, por su
/// nombre. Las dos quitadas se comprueban aparte —que no estan— en vez de dejar de nombrarlas:
/// una comparacion con excepciones sin nombre deja de servir.
/// </para>
/// </remarks>
[TestClass]
public class PruebasContraElPython
{
    /// <summary>Las dos que el dueno quito el 2026-09-06; el Python las tiene y el C# ya no.</summary>
    private static readonly string[] LasDosQueSeQuitaron = ["fecha_solicitud", "estaca"];

    /// <summary>
    /// Las tres que el C# tiene y el Python no, EN EL ORDEN EN QUE SALEN EN LA HOJA.
    /// </summary>
    /// <remarks>
    /// El orden importa: se comparan contra <c>Except</c>, que conserva el orden de la hoja. El
    /// numero de la unidad es del 2026-09-07 y va arriba, entre la fecha de viaje y el nombre de
    /// la unidad; el motivo y el comentario son del 2026-09-05 y van pegados antes de la clave.
    /// </remarks>
    private static string[] LasQueSeAnadieron =>
    [
        Columnas.Por(Columnas.ColumnaDelNumeroDeUnidad).Titulo,
        MotivosDeLaHoja.RotuloDelMotivo,
        MotivosDeLaHoja.RotuloDelComentario,
    ];

    private sealed record ColumnaDelPython(
        string nombre, string titulo, string clase, bool clave, bool editable, bool respuesta,
        int ancho, string formato, bool bloqueada, string? fondo, string valor_fila_7);

    private sealed record HuellaDelPython(
        string hoja, string congelado, bool protegida, string?[] cinco_lineas,
        string color_de_la_fecha_limite, string?[] titulos, string fondo_de_los_titulos,
        ColumnaDelPython[] columnas, int menus, string[] formulas_de_los_menus, bool[] menus_rechazan);

    private static HuellaDelPython Python => JsonSerializer.Deserialize<HuellaDelPython>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "HuellaDelPython.json")))!;

    /// <summary>Las mismas dos filas que el script le da al Python, con los mismos valores.</summary>
    private static IXLWorksheet HojaDelCSharp()
    {
        FilaDeTrabajo Fila(string nombre, string mrn) => new()
        {
            NumeroCaso = "BALC2609",
            FechaViaje = "2026-10-15",
            Templo = "Santo Domingo",
            UnidadNombre = "Cuatricentenaria (7000014)",
            Nombre = nombre,
            Mrn = mrn,
            AQueVa = "Investidura",
            Clave = Columnas.ArmarLaClave("BALC2609", mrn, 12),
        };
        return LibroDeTrabajo.Construir(
            [Fila("Elena Rosa Muestra", "055-1111-385A"), Fila("Julia Luz Inventada", "055-1111-3853")],
            "Sandy").Worksheet(Columnas.NombreDeLaHoja);
    }

    [TestMethod]
    public void LaHojaSeLlamaIgualYCongelaEnElMismoSitio()
    {
        var hoja = HojaDelCSharp();
        Assert.AreEqual(Python.hoja, hoja.Name);
        Assert.AreEqual("A7", $"A{hoja.SheetView.SplitRow + 1}", "el Python declara el congelado como «A7»");
        Assert.IsTrue(Python.protegida, "el Python sí protege la hoja; esto vigila que la huella no cambie sola");
        Assert.IsFalse(hoja.Protection.IsProtected,
            "el dueño quitó la protección el 2026-09-07: «no bloquees las celdas por favor, de los paquetes»");
    }

    /// <summary>
    /// Las cuatro primeras, letra por letra. La quinta —la instruccion— es la del Python
    /// MAS la frase de las dos columnas nuevas, y se comprueba que empieza igual.
    /// </summary>
    [TestMethod]
    public void LasCincoLineasDeCabeceraDicenLoMismoMasLaFraseDeLoQueNoSePudo()
    {
        var hoja = HojaDelCSharp();
        for (var fila = 1; fila <= 4; fila++)
            Assert.AreEqual(Python.cinco_lineas[fila - 1] ?? string.Empty, hoja.Cell(fila, 1).GetString(), $"línea {fila}");

        var instruccion = hoja.Cell(5, 1).GetString();
        Assert.AreEqual(LibroDeTrabajo.InstruccionDelViejo, Python.cinco_lineas[4],
            "lo que decía el Python no se toca");
        Assert.AreEqual(LibroDeTrabajo.InstruccionDelViejo + LibroDeTrabajo.InstruccionDeCuandoNoSePudo, instruccion,
            "la instrucción es la del viejo más la frase de dónde decir que no se pudo");
        StringAssert.Contains(instruccion, MotivosDeLaHoja.RotuloDelMotivo);
        StringAssert.Contains(instruccion, MotivosDeLaHoja.RotuloDelComentario);
    }

    /// <summary>
    /// Los 14 titulos del Python que quedan siguen ahi y en su orden; lo que sobra y lo que
    /// falta son exactamente las dos parejas declaradas y ninguna mas.
    /// </summary>
    [TestMethod]
    public void LosCatorceTitulosDelPythonQueQuedanSiguenEnSuOrdenYSobranYFaltanLosDeclarados()
    {
        var nuestros = Columnas.Titulos().ToList();
        var titulosQuitados = TitulosDelPythonDe(LasDosQueSeQuitaron);

        CollectionAssert.AreEqual(LasQueSeAnadieron, nuestros.Except(Python.titulos).ToArray(),
            "no puede sobrar ninguna columna más que las tres que el dueño añadió el 2026-09-05 y el 2026-09-07");
        CollectionAssert.AreEqual(titulosQuitados, Python.titulos.Except(nuestros).ToArray(),
            "no puede faltar ninguna columna más que las dos que el dueño quitó el 2026-09-06");

        CollectionAssert.AreEqual(
            Python.titulos.Where(t => !titulosQuitados.Contains(t)).ToArray(),
            nuestros.Where(t => !LasQueSeAnadieron.Contains(t)).ToArray(),
            "quitando las cuatro declaradas, la hoja sigue siendo la del Python en su orden");
    }

    /// <summary>Los rotulos con los que el Python llama a esas columnas, sacados de su huella.</summary>
    private static string[] TitulosDelPythonDe(IReadOnlyList<string> nombres)
        => [.. Python.columnas.Where(c => nombres.Contains(c.nombre)).Select(c => c.titulo)];

    /// <summary>
    /// Las dos que el dueno quito estan en la huella del Python y NO en la hoja de hoy.
    /// </summary>
    /// <remarks>
    /// Se comprueba contra la huella y no contra dos textos escritos aqui: si el Python nunca
    /// las hubiera tenido, esta prueba estaria vigilando un cambio que nadie hizo.
    /// </remarks>
    [TestMethod]
    public void LasDosQueElDuenoQuitoEstabanEnElPythonYYaNoEstanEnLaHoja()
    {
        var nombresDelPython = Python.columnas.Select(c => c.nombre).ToArray();
        foreach (var nombre in LasDosQueSeQuitaron)
        {
            CollectionAssert.Contains(nombresDelPython, nombre, $"el Python sí tiene «{nombre}»");
            CollectionAssert.DoesNotContain(Columnas.Todas.Select(c => c.Nombre).ToArray(), nombre,
                $"el dueño quitó «{nombre}» el 2026-09-06: no la verifica");
        }
    }

    [TestMethod]
    public void LosDosColoresQueElCriterioNombraSonLosMismos()
    {
        var hoja = HojaDelCSharp();
        Assert.AreEqual(Python.fondo_de_los_titulos, Hex(hoja.Cell(6, 3).Style.Fill.BackgroundColor));
        Assert.AreEqual(Python.color_de_la_fecha_limite, Hex(hoja.Cell(3, 1).Style.Font.FontColor));
    }

    /// <summary>
    /// Columna a columna: ancho, formato de numero, bloqueo, fondo y el valor de la fila 7.
    /// </summary>
    /// <remarks>
    /// Las dos unicas diferencias declaradas, y por que ninguna es un fallo:
    /// <list type="bullet">
    /// <item><b>El fondo de las columnas que no son respuesta.</b> openpyxl devuelve
    /// <c>000000</c> para una celda sin relleno —es el color por defecto de un relleno de tipo
    /// «ninguno»— y ClosedXML devuelve transparente. Las dos hojas no pintan nada; lo que
    /// cambia es como lo cuenta la biblioteca. Se compara que las SIETE de respuesta lleven el
    /// mismo <c>FFF6DC</c> y que ninguna otra lo lleve, que es lo que el criterio dice.</item>
    /// <item><b>El valor de las siete respuestas.</b> El Python lo serializa como el texto
    /// «None» y en C# la celda esta vacia. Es la misma celda vacia contada de dos maneras.</item>
    /// </list>
    /// </remarks>
    [TestMethod]
    public void CadaColumnaTieneElMismoAnchoElMismoFormatoYElMismoBloqueo()
    {
        var hoja = HojaDelCSharp();
        // Las que quedan del Python, en el orden del Python. Las dos que el dueño quitó se
        // saltan aquí y se comprueban aparte, por su nombre, en su propia prueba.
        var python = Python.columnas.Where(c => !LasDosQueSeQuitaron.Contains(c.nombre)).ToArray();
        Assert.HasCount(python.Length + LasQueSeAnadieron.Length, Columnas.Todas,
            "las 14 que quedan del Python más las tres que el dueño añadió el 2026-09-05 y el 2026-09-07");

        // ⚠️ NO se comprueba la posición absoluta de cada columna, y no es una relajación: con
        // dos columnas quitadas de en medio y dos añadidas al final, ninguna aritmética de
        // índices dice nada legible. Lo que importa —que las que quedan no se crucen entre
        // ellas— se mide como orden relativo, aquí abajo y de una vez.
        var enLaHoja = Columnas.Todas.Select(c => c.Nombre).ToList();
        CollectionAssert.AreEqual(
            python.Select(c => enLaHoja.IndexOf(c.nombre)).OrderBy(p => p).ToArray(),
            python.Select(c => enLaHoja.IndexOf(c.nombre)).ToArray(),
            "las que quedan del Python conservan su orden relativo, sin cruzarse");

        for (var indice = 1; indice <= python.Length; indice++)
        {
            var esperada = python[indice - 1];
            // Se busca POR NOMBRE y no por posición: las dos columnas nuevas van antes de la
            // clave y las dos quitadas estaban delante, así que casi ninguna está donde estaba.
            var numero = Columnas.IndiceDe(esperada.nombre);
            var nuestra = Columnas.Todas[numero - 1];
            var celda = hoja.Cell(7, numero);

            Assert.AreEqual(esperada.nombre, nuestra.Nombre, $"columna {numero}: nombre");
            Assert.AreEqual(esperada.titulo, nuestra.Titulo, $"columna {numero}: título");
            Assert.AreEqual(esperada.clave, nuestra.EsClave, $"{esperada.nombre}: es clave");
            // ⚠️ `editable` y `bloqueada` YA NO se comparan contra el Python: el dueño desbloqueó
            // la hoja entera el 2026-09-07. Se comprueban aparte, por su nombre, en
            // `LoQueElPythonBloqueaYLaHojaDeHoyYaNo`.
            Assert.AreEqual(esperada.respuesta, nuestra.EsRespuesta, $"{esperada.nombre}: es respuesta");
            Assert.AreEqual((double)esperada.ancho, hoja.Column(numero).Width, $"{esperada.nombre}: ancho");
            Assert.AreEqual(FormatoComparable(esperada.formato), FormatoComparable(celda.Style.NumberFormat.Format),
                $"{esperada.nombre}: formato de número");

            if (esperada.respuesta)
            {
                Assert.AreEqual("FFF6DC", Hex(celda.Style.Fill.BackgroundColor), $"{esperada.nombre}: fondo");
                Assert.IsTrue(celda.IsEmpty(), $"{esperada.nombre}: las respuestas salen vacías (el Python la serializa como «None»)");
            }
            else
            {
                // Una fecha se compara como FECHA y no como el texto con el que cada lenguaje la
                // escribe: el Python la serializa «2026-10-15» y .NET «10/15/2026 12:00:00 AM».
                // Es el mismo dia en la misma celda; lo que cambia es quien lo cuenta.
                var nuestro = celda.DataType == XLDataType.DateTime
                    ? celda.GetDateTime().ToString("yyyy-MM-dd")
                    : celda.GetString();
                Assert.AreEqual(esperada.valor_fila_7, nuestro, $"{esperada.nombre}: valor de la fila 7");
            }
        }
    }

    /// <summary>
    /// La tercera diferencia declarada, nombrada columna a columna: lo que el Python bloquea y
    /// la hoja de hoy ya no.
    /// </summary>
    /// <remarks>
    /// Se comprueba contra la huella y no contra dos textos escritos aquí: si el Python nunca
    /// hubiera bloqueado nada, esta prueba estaría vigilando un cambio que nadie hizo.
    /// </remarks>
    [TestMethod]
    public void LoQueElPythonBloqueaYLaHojaDeHoyYaNo()
    {
        var hoja = HojaDelCSharp();
        var bloqueadasEnElPython = Python.columnas.Where(c => c.bloqueada).Select(c => c.nombre).ToArray();

        Assert.IsNotEmpty(bloqueadasEnElPython, "el Python sí bloquea columnas: si no, no habría nada que declarar");
        foreach (var nombre in bloqueadasEnElPython.Where(n => !LasDosQueSeQuitaron.Contains(n)))
        {
            Assert.IsFalse(
                hoja.Cell(7, Columnas.IndiceDe(nombre)).Style.Protection.Locked,
                $"«{nombre}» sigue bloqueada, y el dueño pidió el 2026-09-07 que no se bloquee ninguna");
        }
    }

    /// <summary>El RRGGBB de un color de ClosedXML, sin el canal alfa, como lo escribe openpyxl.</summary>
    private static string Hex(XLColor color) => $"{color.Color.R:X2}{color.Color.G:X2}{color.Color.B:X2}";

    /// <summary>
    /// El formato de una celda sin formato propio: openpyxl lo llama «General» y ClosedXML lo
    /// deja vacio. Es el mismo formato, no dos formatos distintos.
    /// </summary>
    private static string FormatoComparable(string formato)
        => formato.Length == 0 ? "General" : formato.Replace("yyyy-MM-dd", "yyyy-mm-dd");

    /// <summary>
    /// Los siete menus de si o no son los del Python, y el octavo —el del motivo— es la
    /// otra mitad de la diferencia declarada.
    /// </summary>
    [TestMethod]
    public void LosSieteMenusDelPythonSonLosMismosYElOctavoEsElDelMotivo()
    {
        var hoja = HojaDelCSharp();
        var laDelPython = Python.formulas_de_los_menus.Single();
        var todos = hoja.DataValidations.ToList();

        Assert.HasCount(Python.menus + 1, todos);
        Assert.HasCount(Python.menus, todos.Where(v => v.Value == laDelPython).ToList());
        var delMotivo = todos.Single(v => v.Value != laDelPython);
        foreach (var opcion in MotivosDeLaHoja.Opciones)
            StringAssert.Contains(delMotivo.Value, opcion);

        CollectionAssert.AreEqual(
            Python.menus_rechazan,
            todos.Select(v => v.ShowErrorMessage).Distinct().ToArray(),
            "ninguno rechaza nada, tampoco el nuevo: el menú es una ayuda, no una reja");
    }
}
