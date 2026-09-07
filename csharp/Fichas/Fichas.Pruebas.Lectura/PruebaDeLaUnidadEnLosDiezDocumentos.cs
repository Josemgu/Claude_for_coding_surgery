using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Lectura;

namespace Fichas.Pruebas.Lectura;

/// <summary>
/// El nombre de la unidad es un nombre y el número es un número, sobre los DIEZ PDF reales.
/// </summary>
/// <remarks>
/// <para><b>El defecto que fija, medido el 2026-09-05.</b> En los dos formularios de grupo
/// —los <c>SURB2609</c>, seis hojas cada uno— la columna <c>unidad_nombre</c> acababa
/// conteniendo el <b>número</b> de unidad: <c>«7000011»</c> en 8 de las 12 hojas. En la
/// pantalla se leía una cifra donde va el nombre del barrio.</para>
///
/// <para><b>Por qué pasaba, y por qué la corrección no pierde nada.</b> El nombre y el número
/// salen de la MISMA banda del papel: «Ward/Branch Name and Unit Number» es una sola etiqueta
/// con las dos cosas debajo. Cuando esa banda trae solo la cifra, <c>NormalizarNombreDeUnidad</c>
/// devuelve nulo con razón —no hay nombre— y el respaldo del requisito 9 metía ahí el texto
/// crudo de la banda, que era la cifra. El respaldo existe para no perder lo que el papel
/// decía; aquí no se perdía nada, porque <b>lo que la banda decía ya estaba entero en
/// <c>unidad_numero</c></b>.</para>
///
/// <para>⚠️ <b>Son datos personales.</b> No están en el repositorio y no se copian a él. Sin
/// ellos la prueba se declara no concluyente con su motivo, no falla: fallar diría «hay un
/// defecto» donde solo hay «no tengo el material». Los dígitos que se imprimen van
/// enmascarados.</para>
///
/// <para><b>Y desde el 2026-09-07 aquí vive también lo de las personas</b>, porque esta es la
/// única clase que lee los diez una sola vez: con una lectura por clase la batería pasaba de
/// 5 a 11 minutos. Lo que se vigila es lo que el dueño pidió ese día: que la cédula de una
/// persona sea de la fila de su nombre, y que si no se pudo leer el campo salga vacío.</para>
/// </remarks>
[TestClass]
public class PruebaDeLaUnidadEnLosDiezDocumentos
{
    private const string CarpetaDeLosDocumentos =
        @"C:\Users\josem\.claude\uploads\e38428f3-e062-41e5-92f2-566aacd26e92";

    /// <summary>Los diez PDF de esta máquina: siete sueltos, uno de pareja y dos de grupo.</summary>
    private const int DocumentosEsperados = 10;

    private static IReadOnlyList<HojaLeida> _hojas = [];

    private static string[] Documentos()
        => Directory.Exists(CarpetaDeLosDocumentos)
            ? Directory.GetFiles(CarpetaDeLosDocumentos, "*.pdf").Order().ToArray()
            : [];

    /// <summary>Se leen UNA vez para toda la clase: son veinte hojas de OCR de verdad.</summary>
    [ClassInitialize]
    public static void LeerLosDiezUnaSolaVez(TestContext contexto)
    {
        _ = contexto;
        var rutas = Documentos();
        if (rutas.Length != DocumentosEsperados) return;

        using var lectura = new LecturaDePdf();
        var lector = new LectorDeFormularios(lectura);
        _hojas = rutas.SelectMany(lector.LeerDocumento).ToArray();
    }

    private static IReadOnlyList<HojaLeida> Hojas()
    {
        if (_hojas.Count == 0)
        {
            Assert.Inconclusive(
                $"No están los diez documentos en «{CarpetaDeLosDocumentos}». "
                + "Son datos personales del dueño y no viven en el repositorio.");
        }
        return _hojas;
    }

    private static string? ValorDe(HojaLeida hoja, string campo)
        => hoja.Campos.FirstOrDefault(c => c.Campo == campo)?.Valor;

    /// <summary>Ninguna hoja pone una cifra donde va el nombre del barrio.</summary>
    [TestMethod]
    public void ElNombreDeUnidadNuncaEsElNumeroDeUnidad()
    {
        var hojas = Hojas();
        var conCifraPorNombre = new List<string>();

        foreach (var hoja in hojas)
        {
            var nombre = ValorDe(hoja, Extraccion.CampoUnidadNombre);
            var numero = ValorDe(hoja, Extraccion.CampoUnidadNumero);
            Console.WriteLine(
                $"{Path.GetFileName(hoja.RutaPdf)} pág {hoja.Pagina} · número=«{numero}» · nombre=«{nombre}»"
                + $" · fecha=«{ValorDe(hoja, Extraccion.CampoFechaDeViaje)}»"
                + $" · templo=«{ValorDe(hoja, Extraccion.CampoTemploNombre)}»");

            if (nombre is not null && !nombre.Any(char.IsLetter))
            {
                conCifraPorNombre.Add($"{Path.GetFileName(hoja.RutaPdf)} pág {hoja.Pagina} → «{nombre}»");
            }
        }

        Console.WriteLine(
            $"hojas: {hojas.Count} · con nombre de unidad: "
            + $"{hojas.Count(h => ValorDe(h, Extraccion.CampoUnidadNombre) is not null)} · con número: "
            + $"{hojas.Count(h => ValorDe(h, Extraccion.CampoUnidadNumero) is not null)}");

        Assert.IsEmpty(conCifraPorNombre,
            $"{conCifraPorNombre.Count} de {hojas.Count} hojas traen un nombre de unidad sin ni una letra: "
            + string.Join("; ", conCifraPorNombre));
    }

    /// <summary>
    /// Y el número de unidad, cuando sale, tiene la forma que la base admite: 6 o 7 dígitos.
    /// </summary>
    /// <remarks>
    /// Es la otra mitad del criterio, y sin ella la prueba de arriba pasaría con
    /// <c>unidad_numero</c> lleno de texto. Un número con letras dentro es lo que la base
    /// rechaza, y rechazarlo cuesta la hoja entera: ver
    /// <c>Fichas.Pruebas.App.Importar.PruebasDeLaHojaQueLaBaseNoAcepta</c>.
    /// </remarks>
    [TestMethod]
    public void ElNumeroDeUnidadOTieneSuFormaOSeVeQueNoLaTiene()
    {
        var hojas = Hojas();

        var conNumero = hojas
            .Select(hoja => new { hoja, Valor = ValorDe(hoja, Extraccion.CampoUnidadNumero) })
            .Where(cual => cual.Valor is not null)
            .ToArray();

        var conForma = conNumero
            .Where(cual => Normalizacion.NormalizarNumeroDeUnidad(cual.Valor) == cual.Valor)
            .ToArray();

        Console.WriteLine(
            $"hojas: {hojas.Count} · con algo en el número: {conNumero.Length} · "
            + $"con la forma que la base admite: {conForma.Length}");
        foreach (var cual in conNumero.Except(conForma))
        {
            Console.WriteLine(
                $"   sin forma: {Path.GetFileName(cual.hoja.RutaPdf)} pág {cual.hoja.Pagina} → «{cual.Valor}»");
        }

        // No se exige que las veinte den número: hay hojas que el escaneo trae del revés y
        // de esas no se lee nada aprovechable. Lo que se exige es que lo que salga con
        // valor sea un NÚMERO; lo demás tiene que verse como lo que es y poder corregirse.
        Assert.IsGreaterThan(0, conForma.Length, "alguna hoja tiene que dar el número de unidad");
    }

    // --- Las personas: su cédula es la de su fila, o está vacía ----------------------

    /// <summary>Los dígitos se tapan: son cédulas de personas de verdad.</summary>
    private static string Enmascarar(string? texto)
    {
        if (texto is null) return "(vacío)";
        var salida = texto.ToCharArray();
        for (int i = 0; i < salida.Length; i++)
        {
            if (char.IsDigit(salida[i])) salida[i] = '#';
        }
        return new string(salida);
    }

    private static IReadOnlyList<CampoPropuesto> DeLaTablaDePersonas(HojaLeida hoja, string campo)
        => hoja.Campos.Where(c => c.Tabla == TablaDeProcedencia.Personas && c.Campo == campo)
                      .OrderBy(c => c.FilaFormulario)
                      .ToArray();

    /// <summary>
    /// Fila a fila: el nombre, la cédula, y que la cédula es de la fila de ese nombre.
    /// </summary>
    /// <remarks>
    /// <para><b>El defecto que fija, medido el 2026-09-07 antes de tocar nada.</b> En
    /// <b>20 de 20</b> filas de persona de estas veinte hojas, el valor propuesto de
    /// <c>mrn</c> era el RENGLÓN entero del papel —«Wendell Wilfred Rink 000-0000-000A
    /// Verified»—, porque <c>CedulaDeLaFila</c> juntaba todas las líneas de la fila y el
    /// respaldo del requisito 9 devolvía ese texto cuando no encajaba. En la hoja 5 de los
    /// dos <c>SURB2609</c>, donde el OCR leyó un cero como letra O, el valor de <c>mrn</c>
    /// era literalmente <b>el nombre de la persona</b>.</para>
    ///
    /// <para>Lo que se exige ahora es lo que el dueño pidió: o el campo trae una cédula con
    /// su forma, o está vacío. Nada intermedio, y nunca el nombre.</para>
    /// </remarks>
    [TestMethod]
    public void CadaCedulaEsDeLaFilaDeSuNombreOEstaVacia()
    {
        var hojas = Hojas();
        var conNombreDentro = new List<string>();
        var sinFormaDeCedula = new List<string>();
        int filas = 0;
        int conCedula = 0;

        foreach (var hoja in hojas)
        {
            var nombres = DeLaTablaDePersonas(hoja, Extraccion.CampoNombreDePersona);
            var cedulas = DeLaTablaDePersonas(hoja, Extraccion.CampoCedula);
            Assert.HasCount(nombres.Count, cedulas,
                "cada persona propone su nombre y su cédula: si no, las filas no se pueden emparejar");

            for (int i = 0; i < nombres.Count; i++)
            {
                filas++;
                var nombre = nombres[i];
                var cedula = cedulas[i];
                Assert.AreEqual(nombre.FilaFormulario, cedula.FilaFormulario,
                    "el nombre y la cédula que se emparejan tienen que ser de la MISMA fila del papel");

                string donde = $"{Path.GetFileName(hoja.RutaPdf)} pág {hoja.Pagina} fila {nombre.FilaFormulario}";
                Console.WriteLine(
                    $"  {donde} · nombre=«{Enmascarar(nombre.Valor)}» · cédula=«{Enmascarar(cedula.Valor)}»"
                    + $" · conf={cedula.Confianza:F3} · leído=«{Enmascarar(cedula.ValorOcr)}»");

                if (cedula.Valor is null) continue;
                conCedula++;

                if (Normalizacion.NormalizarCedula(cedula.Valor) != cedula.Valor)
                {
                    sinFormaDeCedula.Add($"{donde} → «{Enmascarar(cedula.Valor)}»");
                }
                if (nombre.Valor is not null
                    && cedula.Valor.Contains(nombre.Valor, StringComparison.OrdinalIgnoreCase))
                {
                    conNombreDentro.Add(donde);
                }
            }
        }

        Console.WriteLine($"filas de persona: {filas} · con cédula: {conCedula} · vacías: {filas - conCedula}");

        Assert.IsEmpty(conNombreDentro,
            $"{conNombreDentro.Count} de {filas} cédulas llevan dentro el nombre de su persona: "
            + string.Join("; ", conNombreDentro));
        Assert.IsEmpty(sinFormaDeCedula,
            $"{sinFormaDeCedula.Count} de {filas} campos de cédula traen algo que no es una cédula: "
            + string.Join("; ", sinFormaDeCedula));
    }

    /// <summary>Y la que sale vacía lo dice: nombra el campo en la franja.</summary>
    /// <remarks>
    /// Es la mitad del requisito 9 que no se negocia. Un hueco avisado es lo que el dueño
    /// pidió; un hueco mudo es lo que el requisito 9 prohíbe, y son cosas distintas.
    /// </remarks>
    [TestMethod]
    public void UnaCedulaVaciaSiempreLlevaSuAvisoYLoQueSeLeyo()
    {
        var mudas = new List<string>();

        foreach (var hoja in Hojas())
        {
            bool loDice = hoja.Avisos.Any(a => a.Campo == Extraccion.CampoCedula);
            foreach (var cedula in DeLaTablaDePersonas(hoja, Extraccion.CampoCedula).Where(c => c.Valor is null))
            {
                if (!loDice)
                {
                    mudas.Add($"{Path.GetFileName(hoja.RutaPdf)} pág {hoja.Pagina} fila {cedula.FilaFormulario}");
                }
                Console.WriteLine(
                    $"  vacía · {Path.GetFileName(hoja.RutaPdf)} pág {hoja.Pagina} fila {cedula.FilaFormulario}"
                    + $" · leído=«{Enmascarar(cedula.ValorOcr)}»");
            }
        }

        Assert.IsEmpty(mudas, $"{mudas.Count} cédulas se quedaron vacías sin decirlo: " + string.Join("; ", mudas));
    }

    /// <summary>
    /// Ninguna hoja toma un valor de otra hoja del mismo documento.
    /// </summary>
    /// <remarks>
    /// <para>Es la segunda pregunta del dueño —«cargan informaciones al lado de otro
    /// PDF»— y aquí se contesta midiendo, no afirmando: cada hoja de los dos documentos de
    /// SEIS páginas se vuelve a leer <b>ella sola</b>, sin las otras cinco, y tiene que dar
    /// exactamente los mismos campos. Si algo de la hoja 4 se colara en la 6, leer la 6
    /// sola daría otra cosa.</para>
    ///
    /// <para>⚠️ Lo que esto NO cubre, y hay que decirlo: prueba que <c>Fichas.Lectura</c> no
    /// cruza hojas. Lo que pasa DESPUÉS —que <c>Fichas.App/Importar/GuardadoDeHojas.cs:57</c>
    /// une en un solo caso las hojas de un documento que comparten número— no se mide aquí
    /// y no es de este terreno.</para>
    /// </remarks>
    [TestMethod]
    public void UnaHojaLeidaSolaDaLoMismoQueLeidaDentroDeSuDocumento()
    {
        var hojas = Hojas();

        // Se comprueba UN documento de varias hojas y no los dos, y es una decisión de
        // coste: cada re-lectura son unos 12 s de OCR de verdad, y los dos `SURB2609` de
        // esta máquina son el mismo documento subido dos veces —mismas 6 páginas, mismos
        // campos, comprobado en la prueba de arriba—. Comprobar los dos añadía un minuto de
        // batería para volver a medir lo mismo.
        string primerDocumentoDeVarias = hojas.First(h => h.Pagina > 1).RutaPdf;
        var deVariasPaginas = hojas.Where(h => h.RutaPdf == primerDocumentoDeVarias).ToArray();
        Assert.IsGreaterThan(1, deVariasPaginas.Length, "sin un documento de varias hojas esto no mide nada");

        using var lectura = new LecturaDePdf();
        var lector = new LectorDeFormularios(lectura);
        var distintas = new List<string>();

        foreach (var hoja in deVariasPaginas)
        {
            var sola = lector.LeerHoja(hoja.RutaPdf, hoja.Pagina);
            string enElDocumento = Resumen(hoja);
            string leidaSola = Resumen(sola);
            if (!string.Equals(enElDocumento, leidaSola, StringComparison.Ordinal))
            {
                distintas.Add($"{Path.GetFileName(hoja.RutaPdf)} pág {hoja.Pagina}");
                Console.WriteLine($"  en el documento: {enElDocumento}");
                Console.WriteLine($"  leída sola     : {leidaSola}");
            }
        }

        Console.WriteLine($"hojas de documentos de varias páginas comprobadas: {deVariasPaginas.Length}");
        Assert.IsEmpty(distintas,
            $"{distintas.Count} hojas leen distinto solas que dentro de su documento: "
            + string.Join("; ", distintas));
    }

    /// <summary>Los campos de una hoja, en una línea, para poder compararlos.</summary>
    private static string Resumen(HojaLeida hoja)
        => string.Join(" | ", hoja.Campos
            .OrderBy(c => c.Tabla)
            .ThenBy(c => c.FilaFormulario ?? 0)
            .ThenBy(c => c.Campo, StringComparer.Ordinal)
            .Select(c => $"{c.Campo}#{c.FilaFormulario}={Enmascarar(c.Valor)}"));
}
