using Fichas.App.Cascara;
using Fichas.App.Importar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// El arreglo de datos para una base que ya tiene el daño: documentos que comparten papel y
/// no puede ser de todos.
/// </summary>
/// <remarks>
/// <para>La base del dueño (2026-09-15) tiene 37 documentos apuntando a la misma ruta, que
/// hoy contiene un impreso ajeno. Nadie guardó una huella del contenido, así que el único
/// modo de saber de quién es el papel que hay HOY en esa ruta es leerlo: si el número de
/// caso que se lee coincide con uno de los que lo disputan, ese lo conserva y los demás lo
/// pierden —y se dice—. Si no coincide con ninguno, no se decide nada: un OCR que lee
/// «PULC26O9» no puede quitarle el papel a PULC2609.</para>
///
/// <para>Solo se lee lo que está en disputa: una ruta con un solo documento —o con varios
/// del mismo número, que son duplicados legítimos— no se toca ni se lee. Con 3 000
/// documentos, leerlos todos serían horas.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaComprobacionDeLosPapeles : BaseDeImportacion
{
    /// <summary>Cuántas veces se leyó un papel; una por ruta en disputa, no una por documento.</summary>
    private int _lecturas;

    /// <summary>Un caso metido directo en la base, como los que ya tiene el dueño.</summary>
    /// <param name="numero">El número de caso.</param>
    /// <param name="ruta">La ruta que guarda.</param>
    /// <param name="pagina">La hoja que guarda.</param>
    private long CasoQueApuntaA(string numero, string ruta, int pagina = 1)
        => Datos.Casos.Guardar(new Caso
        {
            NumeroCaso = numero,
            RutaPdf = ruta,
            PaginaPdf = pagina,
            CreadoEn = "2026-09-10T10:00:00",
        }).Id;

    /// <summary>La comprobación con un lector que lee el rótulo de la hoja y cuenta las lecturas.</summary>
    private ComprobacionDeLosPapeles Comprobacion()
        => new(Datos.Casos, Datos.Ilegibles, new RelojDelSistema(), Copias, (ruta, pagina) =>
        {
            _lecturas++;
            var dice = PapelDePrueba.LoQueDiceLaHoja(ruta, pagina);
            return Fichas.Lectura.Normalizacion.NormalizarNumeroDeCaso(dice);
        });

    /// <summary>Los renglones guardados para ese caso.</summary>
    /// <param name="casoId">El caso.</param>
    private IReadOnlyList<RenglonIlegible> RenglonesDe(long casoId)
        => [.. Datos.Ilegibles.Listar(FiltroDeIlegibles.Todo, Pagina.Primera(500)).Elementos.Where(renglon => renglon.CasoId == casoId)];

    /// <summary>
    /// Dados cuatro documentos con la misma ruta y hoja, y un archivo que hoy dice FORD2610;
    /// cuando se comprueban; entonces FORD2610 conserva el papel (en su copia) y los otros
    /// tres lo pierden con su renglón; y lo que no está en disputa ni se lee ni se toca.
    /// </summary>
    [TestMethod]
    public void QuienNoEsDelPapelLoPierdeYQuienEsLoConserva()
    {
        var escaner = Path.Combine(Carpeta, "escaner");
        var scan = PapelDePrueba.Escribir(escaner, "Scan.pdf", "Demande FORD2610");
        var otro = PapelDePrueba.Escribir(escaner, "otro.pdf", "SANM2609");
        var pulc = CasoQueApuntaA("PULC2609", scan);
        var dejc1 = CasoQueApuntaA("DEJC2608", scan);
        var dejc2 = CasoQueApuntaA("DEJC2608", scan);
        var ford = CasoQueApuntaA("FORD2610", scan);
        var sanm = CasoQueApuntaA("SANM2609", otro);

        var comprobacion = Comprobacion();
        var plan = comprobacion.Planear();
        Assert.HasCount(1, plan.Disputas, "una sola ruta en disputa.");
        Assert.HasCount(4, plan.Disputas[0].Casos);

        var lecturas = comprobacion.Leer(plan);
        var resultado = comprobacion.Aplicar(lecturas);

        Assert.AreEqual(1, _lecturas, "se lee UNA vez por ruta en disputa, no una por documento.");
        Assert.AreEqual(3, resultado.Vaciados);
        Assert.AreEqual(1, resultado.Conservados);
        foreach (var perdido in new[] { pulc, dejc1, dejc2 })
        {
            var caso = Datos.Casos.Obtener(perdido)!;
            Assert.IsNull(caso.RutaPdf, $"el caso {perdido} no es del papel que hay en Scan.pdf.");
            Assert.IsNull(caso.PaginaPdf);
            var renglon = RenglonesDe(perdido).Single(uno => uno.Motivo == MotivosDeIlegible.PapelPerdido);
            Assert.Contains("Scan.pdf", renglon.Detalle ?? string.Empty, "el renglón dice dónde estaba.");
            Assert.Contains("FORD2610", renglon.Detalle ?? string.Empty, "y qué hay ahora ahí.");
        }

        var conservado = Datos.Casos.Obtener(ford)!;
        Assert.Contains("FORD2610", PapelDePrueba.LoQueDiceLaHoja(conservado.RutaPdf, conservado.PaginaPdf));
        Assert.IsTrue(conservado.RutaPdf!.StartsWith(Copias.Carpeta, StringComparison.OrdinalIgnoreCase),
            "el que conserva el papel pasa a la copia, para que el escáner no se lo vuelva a cambiar.");

        var intacto = Datos.Casos.Obtener(sanm)!;
        Assert.AreEqual(otro, intacto.RutaPdf, "lo que no está en disputa no se toca.");
    }

    /// <summary>Dada una base ya comprobada; cuando se comprueba otra vez; entonces no cambia nada.</summary>
    [TestMethod]
    public void ComprobarDosVecesNoCambiaNadaLaSegunda()
    {
        var scan = PapelDePrueba.Escribir(Path.Combine(Carpeta, "escaner"), "Scan.pdf", "Demande FORD2610");
        CasoQueApuntaA("PULC2609", scan);
        CasoQueApuntaA("FORD2610", scan);
        var comprobacion = Comprobacion();
        comprobacion.Aplicar(comprobacion.Leer(comprobacion.Planear()));
        var renglonesTrasLaPrimera = Contar("documentos_ilegibles");

        var segundoPlan = comprobacion.Planear();
        var segunda = comprobacion.Aplicar(comprobacion.Leer(segundoPlan));

        Assert.IsEmpty(segundoPlan.Disputas);
        Assert.AreEqual(0, segunda.Vaciados);
        Assert.AreEqual(renglonesTrasLaPrimera, Contar("documentos_ilegibles"));
    }

    /// <summary>
    /// Dado que el número leído no es de ninguno de los que disputan; cuando se comprueban;
    /// entonces nadie pierde el papel y se dice que no se decidió (regla permanente 1: nada
    /// se adivina).
    /// </summary>
    [TestMethod]
    public void SiLoLeidoNoEsDeNadieNoSeDecideYSeDice()
    {
        var scan = PapelDePrueba.Escribir(Path.Combine(Carpeta, "escaner"), "Scan.pdf", "XXXX0000");
        var pulc = CasoQueApuntaA("PULC2609", scan);
        var dejc = CasoQueApuntaA("DEJC2608", scan);
        var comprobacion = Comprobacion();

        var resultado = comprobacion.Aplicar(comprobacion.Leer(comprobacion.Planear()));

        Assert.AreEqual(0, resultado.Vaciados);
        Assert.AreEqual(2, resultado.SinDecidir);
        Assert.AreEqual(scan, Datos.Casos.Obtener(pulc)!.RutaPdf);
        Assert.AreEqual(scan, Datos.Casos.Obtener(dejc)!.RutaPdf);
        Assert.IsTrue(resultado.Avisos.Any(aviso => aviso.Linea.Contains("XXXX0000", StringComparison.Ordinal)));
    }

    /// <summary>Dado que el archivo en disputa ya no existe; entonces no se toca nada y se dice.</summary>
    [TestMethod]
    public void SiElArchivoYaNoEstaNoSeTocaNadaYSeDice()
    {
        var ruta = Path.Combine(Carpeta, "escaner", "borrado.pdf");
        var pulc = CasoQueApuntaA("PULC2609", ruta);
        CasoQueApuntaA("DEJC2608", ruta);
        var comprobacion = Comprobacion();

        var resultado = comprobacion.Aplicar(comprobacion.Leer(comprobacion.Planear()));

        Assert.AreEqual(0, _lecturas);
        Assert.AreEqual(0, resultado.Vaciados);
        Assert.AreEqual(2, resultado.SinDecidir);
        Assert.AreEqual(ruta, Datos.Casos.Obtener(pulc)!.RutaPdf);
    }

    /// <summary>Dos documentos con el mismo número en la misma hoja son duplicados legítimos, no una disputa.</summary>
    [TestMethod]
    public void DosDuplicadosDelMismoNumeroNoSonUnaDisputa()
    {
        var scan = PapelDePrueba.Escribir(Path.Combine(Carpeta, "escaner"), "Scan.pdf", "DEJC2608");
        CasoQueApuntaA("DEJC2608", scan);
        CasoQueApuntaA("DEJC2608", scan);

        var plan = Comprobacion().Planear();

        Assert.IsEmpty(plan.Disputas);
        Assert.AreEqual(2, plan.DocumentosConPapel);
    }

    /// <summary>
    /// Dado un documento cuyo archivo ya no está porque su carpeta se renombró; cuando se
    /// comprueba; entonces se busca el mismo nombre en las carpetas hermanas y, si el único
    /// que hay dice su número, el documento recupera su papel (en su copia).
    /// </summary>
    /// <remarks>
    /// En la base del dueño (medido en su máquina el 2026-09-15): 9 rutas archivadas apuntan a
    /// archivos que ya no existen porque las carpetas pasaron a llamarse «… Complete».
    /// </remarks>
    [TestMethod]
    public void UnPapelPerdidoSeRecuperaSiElMismoNombreEstaEnUnaCarpetaHermanaYDiceSuNumero()
    {
        var escaner = Path.Combine(Carpeta, "escaner");
        var dondeEstaba = Path.Combine(escaner, "Octubre", "ficha.pdf");
        Directory.CreateDirectory(Path.Combine(escaner, "Octubre"));
        PapelDePrueba.Escribir(Path.Combine(escaner, "Octubre Complete"), "ficha.pdf", "PULC2609");
        var pulc = CasoQueApuntaA("PULC2609", dondeEstaba);

        var resultado = Comprobacion().Aplicar(Comprobacion().Leer(Comprobacion().Planear()));

        Assert.AreEqual(1, resultado.Recuperados);
        var caso = Datos.Casos.Obtener(pulc)!;
        Assert.Contains("PULC2609", PapelDePrueba.LoQueDiceLaHoja(caso.RutaPdf, caso.PaginaPdf));
        Assert.IsTrue(caso.RutaPdf!.StartsWith(Copias.Carpeta, StringComparison.OrdinalIgnoreCase), "recuperado a su copia.");
    }

    /// <summary>Dado un papel perdido con dos candidatos del mismo nombre; entonces no se decide y se dice.</summary>
    [TestMethod]
    public void UnPapelPerdidoConDosCandidatosNoSeDecide()
    {
        var escaner = Path.Combine(Carpeta, "escaner");
        var dondeEstaba = Path.Combine(escaner, "Octubre", "Scan.pdf");
        Directory.CreateDirectory(Path.Combine(escaner, "Octubre"));
        PapelDePrueba.Escribir(Path.Combine(escaner, "Octubre Complete"), "Scan.pdf", "PULC2609");
        PapelDePrueba.Escribir(Path.Combine(escaner, "Noviembre"), "Scan.pdf", "PULC2609");
        var pulc = CasoQueApuntaA("PULC2609", dondeEstaba);

        var resultado = Comprobacion().Aplicar(Comprobacion().Leer(Comprobacion().Planear()));

        Assert.AreEqual(0, resultado.Recuperados);
        Assert.AreEqual(1, resultado.SinDecidir);
        Assert.AreEqual(dondeEstaba, Datos.Casos.Obtener(pulc)!.RutaPdf, "no se adivina entre dos.");
        Assert.AreEqual(0, _lecturas, "con dos candidatos no se lee ninguno.");
    }

    /// <summary>Dado un papel perdido cuyo único candidato dice OTRO número; entonces no se decide.</summary>
    [TestMethod]
    public void UnPapelPerdidoCuyoCandidatoDiceOtroNumeroNoSeDecide()
    {
        var escaner = Path.Combine(Carpeta, "escaner");
        var dondeEstaba = Path.Combine(escaner, "Octubre", "ficha.pdf");
        Directory.CreateDirectory(Path.Combine(escaner, "Octubre"));
        PapelDePrueba.Escribir(Path.Combine(escaner, "Octubre Complete"), "ficha.pdf", "DEJC2608");
        var pulc = CasoQueApuntaA("PULC2609", dondeEstaba);

        var resultado = Comprobacion().Aplicar(Comprobacion().Leer(Comprobacion().Planear()));

        Assert.AreEqual(0, resultado.Recuperados);
        Assert.AreEqual(1, resultado.SinDecidir);
        Assert.AreEqual(dondeEstaba, Datos.Casos.Obtener(pulc)!.RutaPdf);
    }

    /// <summary>
    /// Dados dos documentos con el mismo papel byte a byte en dos rutas, sin marca de
    /// duplicado; cuando se comprueban; entonces el más nuevo queda marcado como duplicado del
    /// más antiguo, con su renglón; y la segunda pasada no marca nada.
    /// </summary>
    /// <remarks>Los cuatro pares de la base del dueño: 76↔69, 77↔70, 78↔71 y 89↔72 (medido en su máquina).</remarks>
    [TestMethod]
    public void DosDocumentosConElMismoPapelByteAByteSeMarcanComoDuplicado()
    {
        var enOctubre = PapelDePrueba.Escribir(Path.Combine(Carpeta, "Octubre"), "ficha.pdf", "FORD2610");
        var enHaiti = Path.Combine(Carpeta, "HAITI Octubre", "ficha.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(enHaiti)!);
        File.Copy(enOctubre, enHaiti);
        var antiguo = CasoQueApuntaA("FORD2610", enOctubre);
        var nuevo = CasoQueApuntaA("FORD2610", enHaiti);

        var primera = Comprobacion().Aplicar(Comprobacion().Leer(Comprobacion().Planear()));
        var segunda = Comprobacion().Aplicar(Comprobacion().Leer(Comprobacion().Planear()));

        Assert.AreEqual(1, primera.Marcados);
        Assert.AreEqual(antiguo, Datos.Casos.Obtener(nuevo)!.DuplicadoDe);
        Assert.IsNull(Datos.Casos.Obtener(antiguo)!.DuplicadoDe, "el original no se toca.");
        Assert.IsTrue(RenglonesDe(nuevo).Any(renglon => renglon.Motivo == MotivosDeIlegible.EntroComoDuplicado));
        Assert.AreEqual(0, segunda.Marcados);
        Assert.AreEqual(0, _lecturas, "un par idéntico se decide por la huella, sin leer nada.");
    }

    /// <summary>Dos documentos con el mismo papel pero de hojas distintas de un PDF de grupo NO son un par.</summary>
    [TestMethod]
    public void ElMismoArchivoEnHojasDistintasNoEsUnPar()
    {
        var grupo = PapelDePrueba.Escribir(Path.Combine(Carpeta, "Octubre"), "grupo.pdf", "SURB2609", "SURB2609");
        CasoQueApuntaA("SURB2609", grupo, pagina: 1);
        var deLaHoja2 = CasoQueApuntaA("SURB2609", grupo, pagina: 2);

        var resultado = Comprobacion().Aplicar(Comprobacion().Leer(Comprobacion().Planear()));

        Assert.AreEqual(0, resultado.Marcados);
        Assert.IsNull(Datos.Casos.Obtener(deLaHoja2)!.DuplicadoDe);
    }

    /// <summary>La línea del resumen lleva las tres cifras, también cuando son cero.</summary>
    [TestMethod]
    public void LaLineaDelResumenLlevaLasCifras()
    {
        var resultado = Comprobacion().Aplicar(Comprobacion().Leer(Comprobacion().Planear()));

        Assert.Contains("0", resultado.Linea);
        Assert.IsFalse(string.IsNullOrWhiteSpace(resultado.Linea));
    }
}
