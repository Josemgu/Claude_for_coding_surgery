namespace Fichas.Pruebas.App.Inicio;

/// <summary>
/// C1-6: cero cuadros modales en Inicio y en las dos pantallas nuevas, cazados con una
/// busqueda sobre sus fuentes y con el denominador dicho.
/// </summary>
/// <remarks>
/// Lo que sustituye al cuadro es la franja de avisos de la cascara —una linea, «ver» y X—
/// y el acuse del pie. Requisito 4 y 9 del dueno: ni un parrafo, y avisar sin impedir.
///
/// Si no encuentra la carpeta —porque las pruebas corren desde un sitio del que no se
/// puede llegar al repositorio— NO pasa en verde: se declara no concluyente. Una
/// comprobacion que no encontro donde mirar no es una comprobacion.
/// </remarks>
[TestClass]
public sealed class PruebasSinCuadrosEnInicio
{
    /// <summary>Las cuatro palabras que abren un cuadro que detiene al usuario.</summary>
    private static readonly string[] LoProhibido =
        ["ContentDialog", "MessageDialog", "ShowAsync", "MessageBox"];

    /// <summary>Cuantos archivos tienen hoy las dos carpetas; menos que eso significa que se leyo mal.</summary>
    private const int ArchivosQueSeEsperan = 14;

    /// <summary>C1-6. Cero cuadros modales en las fuentes de Inicio y de Grupo, con su denominador.</summary>
    [TestMethod]
    public void NiUnCuadroModalEnInicioNiEnLasDosPantallasNuevas()
    {
        var archivos = LasFuentes();

        var culpables = new List<string>();
        foreach (var archivo in archivos)
        {
            var texto = File.ReadAllText(archivo);
            foreach (var palabra in LoProhibido)
            {
                if (texto.Contains(palabra, StringComparison.Ordinal))
                    culpables.Add($"{Path.GetFileName(archivo)} → {palabra}");
            }
        }

        Console.WriteLine($"Cuadros modales: {culpables.Count} en {archivos.Count} archivos de Inicio y Grupo.");
        Assert.IsGreaterThanOrEqualTo(
            ArchivosQueSeEsperan,
            archivos.Count,
            $"Se esperaban al menos {ArchivosQueSeEsperan} archivos y se leyeron {archivos.Count}.");
        Assert.IsEmpty(culpables, "Cuadros encontrados: " + string.Join(" · ", culpables));
    }

    /// <summary>
    /// Regla permanente 4: lo que se lee en pantalla va en espanol con sus tildes.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Esto existe porque la carpeta <c>Grupo</c> es nueva y el barrido general de
    /// <c>Cascara/PruebasDelEspanolDeLaPantalla.cs</c> no la mira:</b> su lista de carpetas
    /// esta escrita a mano y ese archivo es de otro terreno. Aqui se aplica la misma regla
    /// al terreno propio. El dia que la cascara se descongele, se anade «Grupo» a aquella
    /// lista y esta prueba se puede quitar.
    /// </remarks>
    [TestMethod]
    public void NingunTextoDeLasDosPantallasNuevasPierdeUnaTilde()
    {
        var textos = LosTextosQueVeElUsuario();
        var malos = new List<string>();

        foreach (var (donde, texto) in textos)
        {
            // Lo que va dentro de {…} es el NOMBRE DE UNA VARIABLE, que va sin tildes como
            // todo el codigo del proyecto. Sin quitarlo, «{dias} días» se acusaria de tener
            // «dias» mal escrito, y «arreglarlo» rompería el programa.
            var escrito = System.Text.RegularExpressions.Regex.Replace(texto, @"\{[^{}]*\}", " ");

            foreach (var (mal, bien) in PalabrasQueLlevanTilde)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(
                        escrito,
                        $@"(?<![\p{{L}}\d_]){System.Text.RegularExpressions.Regex.Escape(mal)}(?![\p{{L}}\d_])",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    malos.Add($"{donde}: «{texto}» → «{mal}» debe ser «{bien}»");
            }

            if (texto.Contains("(s)", StringComparison.Ordinal))
                malos.Add($"{donde}: «{texto}» usa la muleta del «(s)».");
        }

        Console.WriteLine($"Textos mal escritos: {malos.Count} de {textos.Count} leídos en Inicio y Grupo.");

        Assert.IsGreaterThanOrEqualTo(
            TextosQueTieneQueHaberLeido,
            textos.Count,
            $"Se leyeron {textos.Count} textos y se esperaban al menos {TextosQueTieneQueHaberLeido}. "
            + "Un barrido que no encontró textos no comprueba nada.");
        Assert.IsEmpty(malos, "Textos mal escritos:" + Environment.NewLine + string.Join(Environment.NewLine, malos));
    }

    /// <summary>
    /// Lo que el dueno pidio ver en Inicio, y NADA mas.
    /// </summary>
    /// <remarks>
    /// <para>Sus palabras el 2026-09-05: <i>«Lo único que quiero ver en Home es lo que está
    /// listo para asignar y lo que está asignado a los agentes»</i>. Los siete rotulos que
    /// Inicio tenia el 2026-09-04 —medidos por el supervisor con <c>grep</c> sobre este mismo
    /// archivo— no pueden seguir ahi. Se comprueba sobre el XAML porque es lo que el lee: una
    /// regla que solo mirara los datos no cazaria un rotulo olvidado en la pantalla.</para>
    ///
    /// <para>⚠️ <b>La segunda mitad de esta prueba cambio el 2026-09-09, y hay que saber por
    /// que.</b> Exigia leer «Listo para asignar», «Asignado a los agentes» y «Lo que no está
    /// completo» EN Inicio. El dueno lo deshizo: <i>«Listo para asignar debe ser una ventana,
    /// no debe estar en el home del sistema. Tampoco asignar a los agentes... Lo unico que
    /// quiero es el calendario y un cuadro»</i>. Los dos primeros se comprueban ahora en
    /// <c>PruebasDeLaPestanaDelFlujo</c>, que ademas exige que la pestana nueva exista y tenga
    /// entrada en el menu, y el tercero se convirtio en la cifra del cuadro.</para>
    ///
    /// <para><b>Los siete de 2026-09-05 siguen prohibidos y por eso la prueba no se borra:</b>
    /// que Inicio se haya vaciado aun mas no autoriza a que vuelva ninguno de aquellos.</para>
    /// </remarks>
    [TestMethod]
    public void InicioEnsenaLasDosCosasQuePidioElDuenoYNingunaDeLasSiete()
    {
        // Se leen los ATRIBUTOS, no el texto crudo del archivo: un comentario que explique
        // adónde se movió un rótulo no es un rótulo, y contarlo como tal haría imposible
        // dejar escrito el motivo del cambio justo donde se hizo.
        var rotulos = RotulosDe(Path.Combine(LaCarpeta("Inicio"), "PaginaDeInicio.xaml"));

        var losSiete = new[]
        {
            "Personas\npor viajar", "Con la recomendación\ncompleta",
            "Hojas sin\ndevolver", "Viajaron sin\nverificar",
            "Por verificar antes de que salgan", "Sin fecha de viaje", "El equipo",
        };

        var siguen = losSiete.Where(r => rotulos.Contains(r, StringComparer.Ordinal)).ToList();

        Console.WriteLine($"Rótulos leídos en Inicio: {rotulos.Count}.");
        Assert.IsGreaterThanOrEqualTo(6, rotulos.Count, "Un barrido que no leyó rótulos no comprueba nada.");
        Assert.IsEmpty(siguen, "Rótulos que el dueño no quiere ver en Inicio y siguen ahí: " + string.Join(" · ", siguen));

        // Las dos cosas que quedan desde el 2026-09-09: el cuadro y el calendario.
        Assert.Contains("Me falta por completar", rotulos);
        Assert.Contains("Lo que tienen los agentes", rotulos);
        Assert.Contains("Pulse un día para abrir el grupo que viaja ese día", rotulos);
    }

    /// <summary>Los textos escritos que un usuario lee en una pantalla, sin los enlaces.</summary>
    /// <param name="archivoXaml">La ruta del .xaml; se leen sus atributos <c>Text</c> y <c>Content</c> que no empiezan por «{».</param>
    private static List<string> RotulosDe(string archivoXaml)
        => [.. System.Xml.Linq.XDocument.Load(archivoXaml).Descendants()
            .SelectMany(e => e.Attributes())
            .Where(a => a.Name.LocalName is "Text" or "Content")
            .Select(a => a.Value)
            .Where(v => v.Length > 0 && !v.StartsWith('{'))];

    // ---- de donde salen los textos ------------------------------------------

    /// <summary>Lo minimo que hay que haber leido para que un cero signifique algo.</summary>
    private const int TextosQueTieneQueHaberLeido = 40;

    /// <summary>Las palabras que en espanol SIEMPRE llevan tilde o ene, con su forma correcta.</summary>
    /// <remarks>
    /// Van solo las inequivocas. «mas», «aun», «solo», «este» y «esta» NO estan: las cinco
    /// son palabras distintas con tilde y sin ella, y una lista que las metiera obligaria a
    /// poner tilde donde no va. Por lo mismo faltan «cuanto» —«en cuanto se corrija» va sin
    /// tilde— y «quien» —«quien lo lleva» tambien—: las dos formas existen y son correctas
    /// segun la frase, y esta prueba mira palabras sueltas, no frases.
    /// </remarks>
    private static readonly (string Mal, string Bien)[] PalabrasQueLlevanTilde =
    [
        ("numero", "número"), ("numeros", "números"),
        ("companero", "compañero"), ("companeros", "compañeros"),
        ("dia", "día"), ("dias", "días"),
        ("cuantos", "cuántos"), ("cuantas", "cuántas"),
        ("lider", "líder"), ("lideres", "líderes"), ("cedula", "cédula"),
        ("razon", "razón"), ("estan", "están"),
        ("manana", "mañana"), ("miercoles", "miércoles"), ("sabado", "sábado"),
        ("proximo", "próximo"), ("proximos", "próximos"),
        ("ultimo", "último"), ("ultima", "última"),
        ("informacion", "información"), ("recomendacion", "recomendación"),
        ("asignacion", "asignación"), ("verificacion", "verificación"),
        ("correccion", "corrección"), ("revision", "revisión"),
        ("tambien", "también"), ("despues", "después"), ("ademas", "además"),
        ("segun", "según"), ("ningun", "ningún"), ("algun", "algún"),
        ("unico", "único"), ("unica", "única"), ("pagina", "página"),
    ];

    /// <summary>Todo lo que un usuario puede leer en Inicio y en Grupo: XAML y literales.</summary>
    private static List<(string Donde, string Texto)> LosTextosQueVeElUsuario()
    {
        var mirados = new[]
        {
            "Text", "Content", "PlaceholderText", "Header", "Description",
            "ToolTipService.ToolTip", "AutomationProperties.Name", "Title",
        };

        var textos = new List<(string, string)>();

        foreach (var archivo in LasFuentes())
        {
            if (archivo.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                textos.AddRange(System.Xml.Linq.XDocument.Load(archivo).Descendants()
                    .SelectMany(e => e.Attributes())
                    .Where(a => mirados.Contains(a.Name.LocalName, StringComparer.Ordinal))
                    // Un valor entre llaves es un enlace o un recurso, no un texto escrito.
                    .Where(a => !a.Value.StartsWith('{') && a.Value.Length > 0)
                    .Select(a => ($"{Path.GetFileName(archivo)} → {a.Name.LocalName}", a.Value)));
                continue;
            }

            var lineas = File.ReadAllLines(archivo);
            for (var i = 0; i < lineas.Length; i++)
            {
                var linea = lineas[i].TrimStart();
                if (linea.StartsWith("//", StringComparison.Ordinal) || linea.StartsWith('*')) continue;

                foreach (System.Text.RegularExpressions.Match literal in
                         System.Text.RegularExpressions.Regex.Matches(lineas[i], "\"([^\"\\\\]*)\""))
                {
                    // Se exige un espacio dentro: es lo que separa un rotulo de una clave de
                    // datos como «numero_caso», que va sin tilde a proposito.
                    var texto = literal.Groups[1].Value;
                    if (texto.Contains(' ', StringComparison.Ordinal))
                        textos.Add(($"{Path.GetFileName(archivo)}:{i + 1}", texto));
                }
            }
        }

        return textos;
    }

    /// <summary>Los .cs y .xaml de las dos carpetas del terreno.</summary>
    private static List<string> LasFuentes()
        => [.. new[] { LaCarpeta("Inicio"), LaCarpeta("Grupo"), LaCarpeta("Flujo") }
            .SelectMany(c => Directory.EnumerateFiles(c, "*.*", SearchOption.AllDirectories))
            .Where(a => a.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                        || a.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))];

    /// <summary>Una carpeta de la app, buscada subiendo desde donde corre la prueba.</summary>
    /// <param name="cual">El nombre de la carpeta dentro de <c>Fichas.App</c>: «Inicio», «Grupo» o «Flujo».</param>
    /// <returns>La ruta completa; si no se encuentra, la prueba queda inconclusa.</returns>
    private static string LaCarpeta(string cual)
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var carpeta = Path.Combine(actual.FullName, "csharp", "Fichas", "Fichas.App", cual);
            if (Directory.Exists(carpeta)) return carpeta;
            actual = actual.Parent;
        }

        Assert.Inconclusive(
            $"No se encontró «csharp/Fichas/Fichas.App/{cual}» desde «{AppContext.BaseDirectory}». "
            + "Sin las fuentes delante esto no comprueba nada.");
        return string.Empty;
    }
}
