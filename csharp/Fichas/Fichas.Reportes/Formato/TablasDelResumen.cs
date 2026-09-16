using ClosedXML.Excel;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;
using static Fichas.Reportes.Formato.PinturaDelResumen;

namespace Fichas.Reportes.Formato;

/// <summary>
/// Las dos tablas de arriba de la hoja unica —por unidad y por agente— y la leyenda del semaforo.
/// </summary>
/// <remarks>
/// <para><b>El semaforo de «Sin completar» por unidad son tres reglas de formato condicional</b>,
/// en el orden del mockup: verde con cero; naranja con pendientes y el paquete devuelto; rojo
/// con pendientes y sin devolver. Es lo que pidio el pase. En la tabla por agente el mismo
/// semaforo se pinta celda a celda, porque «devolvió» ahi es «3 de 3» y una formula que lo
/// lea seria fragil.</para>
///
/// <para>Las cifras entran como NUMEROS —sumar es la mitad de para que sirve una hoja de
/// calculo— y el numero de unidad como TEXTO, con formato <c>@</c>, que es lo que impide que
/// Excel se coma un cero de delante. Los totales los escribe el programa, no <c>SUM</c>: son
/// los que ya conto el resumen y asi el PDF y el Excel dicen lo mismo.</para>
/// </remarks>
internal static class TablasDelResumen
{
    /// <summary>La marca de «devolvió».</summary>
    private const string Si = "✓";

    /// <summary>La marca de «no devolvió».</summary>
    private const string No = "✗";

    /// <summary>La raya de «no hay agente», y de cualquier hueco de esta hoja.</summary>
    internal const string Raya = "—";

    // ---- por unidad ---------------------------------------------------------

    /// <summary>La tabla «Por unidad» en B8:H, con su total y el semaforo por formato condicional.</summary>
    /// <param name="hoja">La hoja.</param>
    /// <param name="resumen">El resumen.</param>
    /// <param name="sitio">Donde cae cada fila.</param>
    internal static void PorUnidad(IXLWorksheet hoja, ResumenDelPeriodo resumen, DisposicionDelResumen sitio)
    {
        HojaDelResumen.TituloDeSeccion(hoja, DisposicionDelResumen.FilaDeLosTitulos, "B", "H", "Por unidad");
        Rotulos(hoja, DisposicionDelResumen.FilaDeLosRotulos, 2, ["N.º", "Unidad", "Viajaron", "Completos", "Sin completar", "Agente", "Devolvió"]);

        var fila = DisposicionDelResumen.PrimeraFilaDeDatos;
        foreach (var unidad in resumen.PorUnidad)
        {
            Texto(hoja.Cell(fila, 2), unidad.Numero);
            hoja.Cell(fila, 3).SetValue(unidad.Nombre);
            hoja.Cell(fila, 4).SetValue(unidad.Viajaron);
            hoja.Cell(fila, 5).SetValue(unidad.Completos);
            hoja.Cell(fila, 6).SetValue(unidad.SinCompletar);
            hoja.Cell(fila, 7).SetValue(unidad.Agente);
            // Varios agentes en una unidad no caben en 22 caracteres: la celda se parte en lineas.
            hoja.Cell(fila, 7).Style.Alignment.WrapText = true;
            if (unidad.Agente == Vocabulario.SinAgente) hoja.Cell(fila, 7).Style.Font.FontColor = Apagado;
            Marca(hoja.Cell(fila, 8), unidad.Devolvio);
            fila++;
        }

        Total(hoja, sitio.FilaDelTotalDeUnidades, 2, 8);
        hoja.Cell(sitio.FilaDelTotalDeUnidades, 2).SetValue($"Total · {Plural.Con(resumen.Unidades, "unidad", "unidades")}");
        hoja.Cell(sitio.FilaDelTotalDeUnidades, 4).SetValue(resumen.Viajaron);
        hoja.Cell(sitio.FilaDelTotalDeUnidades, 5).SetValue(resumen.Completos);
        hoja.Cell(sitio.FilaDelTotalDeUnidades, 6).SetValue(resumen.SinCompletar);
        hoja.Cell(sitio.FilaDelTotalDeUnidades, 7).SetValue(Plural.Con(resumen.Agentes, "agente", "agentes"));
        hoja.Cell(sitio.FilaDelTotalDeUnidades, 8).SetValue(DeCuantos(resumen.CasosDevueltos, resumen.CasosConAgente));

        ALaDerecha(hoja.Range(DisposicionDelResumen.PrimeraFilaDeDatos, 4, sitio.FilaDelTotalDeUnidades, 6));
        AlCentro(hoja.Cell(sitio.FilaDelTotalDeUnidades, 8));
        // Una unidad con varios agentes ocupa dos lineas; lo demas de su fila, y la fila de
        // enfrente de la tabla por agente, se queda a media altura y no pegado abajo.
        hoja.Range(DisposicionDelResumen.PrimeraFilaDeDatos, 2, sitio.FilaDelTotalDeUnidades - 1, 14)
            .Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        if (resumen.PorUnidad.Count > 0)
            SemaforoPorFormula(hoja.Range(DisposicionDelResumen.PrimeraFilaDeDatos, 6, sitio.FilaDelTotalDeUnidades - 1, 6));
    }

    /// <summary>Las tres reglas del semaforo sobre la columna «Sin completar», mirando «Devolvió» dos columnas a la derecha.</summary>
    /// <remarks>Las formulas son relativas a la primera celda del rango; Excel las desplaza fila a fila.</remarks>
    /// <param name="sinCompletar">La columna F de la tabla por unidad, sin la fila de total.</param>
    private static void SemaforoPorFormula(IXLRange sinCompletar)
    {
        var primera = sinCompletar.FirstCell().Address.RowNumber;

        Regla(sinCompletar, $"=$F{primera}=0", VerdeFondo, VerdeTinta);
        Regla(sinCompletar, $"=AND($F{primera}>0,$H{primera}=\"{Si}\")", NaranjaFondo, NaranjaTinta);
        Regla(sinCompletar, $"=AND($F{primera}>0,$H{primera}<>\"{Si}\")", RojoFondo, RojoTinta);
    }

    /// <summary>Una regla de formato condicional: cuando la formula da verdadero, ese fondo y esa letra en negrita.</summary>
    /// <param name="rango">El rango.</param>
    /// <param name="formula">La formula, con «=» delante.</param>
    /// <param name="fondo">El color de relleno.</param>
    /// <param name="letra">El color de la letra.</param>
    private static void Regla(IXLRange rango, string formula, XLColor fondo, XLColor letra)
    {
        var estilo = rango.AddConditionalFormat().WhenIsTrue(formula);
        estilo.Fill.BackgroundColor = fondo;
        estilo.Font.FontColor = letra;
        estilo.Font.Bold = true;
    }

    // ---- por agente ---------------------------------------------------------

    /// <summary>La tabla «Por agente» en J8:N, con su total y el semaforo pintado celda a celda.</summary>
    /// <param name="hoja">La hoja.</param>
    /// <param name="resumen">El resumen.</param>
    /// <param name="sitio">Donde cae cada fila.</param>
    internal static void PorAgente(IXLWorksheet hoja, ResumenDelPeriodo resumen, DisposicionDelResumen sitio)
    {
        HojaDelResumen.TituloDeSeccion(hoja, DisposicionDelResumen.FilaDeLosTitulos, "J", "N", "Por agente");
        Rotulos(hoja, DisposicionDelResumen.FilaDeLosRotulos, 10, ["Agente", "Asignados", "Completos", "Sin completar", "Devolvió"]);

        var fila = DisposicionDelResumen.PrimeraFilaDeDatos;
        foreach (var agente in resumen.PorAgente)
        {
            hoja.Cell(fila, 10).SetValue(agente.Nombre);
            if (agente.Nombre == Vocabulario.SinAgente) hoja.Cell(fila, 10).Style.Font.FontColor = Apagado;
            hoja.Cell(fila, 11).SetValue(agente.Asignados);
            hoja.Cell(fila, 12).SetValue(agente.Completos);
            hoja.Cell(fila, 13).SetValue(agente.SinCompletar);
            SemaforoPintado(hoja.Cell(fila, 13), agente);
            hoja.Cell(fila, 14).SetValue(agente.CasosACargo == 0 ? Raya : DeCuantos(agente.CasosDevueltos, agente.CasosACargo));
            AlCentro(hoja.Cell(fila, 14));
            fila++;
        }

        Total(hoja, sitio.FilaDelTotalDeAgentes, 10, 14);
        hoja.Cell(sitio.FilaDelTotalDeAgentes, 10).SetValue("Total");
        hoja.Cell(sitio.FilaDelTotalDeAgentes, 11).SetValue(resumen.PorAgente.Sum(a => a.Asignados));
        hoja.Cell(sitio.FilaDelTotalDeAgentes, 12).SetValue(resumen.PorAgente.Sum(a => a.Completos));
        hoja.Cell(sitio.FilaDelTotalDeAgentes, 13).SetValue(resumen.PorAgente.Sum(a => a.SinCompletar));
        hoja.Cell(sitio.FilaDelTotalDeAgentes, 14).SetValue(DeCuantos(resumen.CasosDevueltos, resumen.CasosConAgente));
        AlCentro(hoja.Cell(sitio.FilaDelTotalDeAgentes, 14));

        ALaDerecha(hoja.Range(DisposicionDelResumen.PrimeraFilaDeDatos, 11, sitio.FilaDelTotalDeAgentes, 13));
    }

    /// <summary>El semaforo de un agente, pintado: verde sin pendientes; naranja con pendientes y todo devuelto; rojo si no.</summary>
    /// <param name="celda">La celda de «Sin completar» de ese agente.</param>
    /// <param name="agente">El agente.</param>
    private static void SemaforoPintado(IXLCell celda, AgenteDelResumen agente)
    {
        var (fondo, letra) = agente.SinCompletar == 0
            ? (VerdeFondo, VerdeTinta)
            : agente.CasosACargo > 0 && agente.CasosDevueltos == agente.CasosACargo
                ? (NaranjaFondo, NaranjaTinta)
                : (RojoFondo, RojoTinta);

        celda.Style.Fill.BackgroundColor = fondo;
        celda.Style.Font.FontColor = letra;
        celda.Style.Font.Bold = true;
    }

    // ---- la leyenda ---------------------------------------------------------

    /// <summary>La leyenda del semaforo, en una sola celda con tres puntos de color.</summary>
    /// <param name="hoja">La hoja.</param>
    /// <param name="fila">En que fila.</param>
    internal static void Leyenda(IXLWorksheet hoja, int fila)
    {
        hoja.Row(fila - 1).Height = 7.5;
        var celda = hoja.Range($"B{fila}:H{fila}").Merge().FirstCell();
        var texto = celda.CreateRichText();
        Punto(texto, VerdeMarca, "Todo completo");
        Punto(texto, NaranjaMarca, "Pendientes · paquete devuelto");
        Punto(texto, RojoMarca, "Pendientes · sin devolver");
    }

    /// <summary>Un punto de color con su palabra al lado, en 9 puntos.</summary>
    /// <param name="texto">El texto enriquecido de la celda.</param>
    /// <param name="color">El color del punto.</param>
    /// <param name="palabra">Lo que significa.</param>
    private static void Punto(IXLRichText texto, XLColor color, string palabra)
    {
        texto.AddText("● ").SetFontColor(color).SetFontSize(9);
        texto.AddText($"{palabra}    ").SetFontColor(Apagado).SetFontSize(9);
    }

    // ---- lo compartido ------------------------------------------------------

    /// <summary>Una fila de rotulos: relleno marino, letra blanca en negrita, una celda por rotulo.</summary>
    /// <param name="hoja">La hoja.</param>
    /// <param name="fila">En que fila.</param>
    /// <param name="desde">La primera columna, en numero.</param>
    /// <param name="rotulos">Los rotulos, en orden.</param>
    internal static void Rotulos(IXLWorksheet hoja, int fila, int desde, string[] rotulos)
    {
        hoja.Row(fila).Height = 16.5;
        for (var i = 0; i < rotulos.Length; i++)
        {
            var celda = hoja.Cell(fila, desde + i);
            celda.SetValue(rotulos[i]);
            Pintar(celda.AsRange(), Marino, Blanco);
            celda.Style.Font.Bold = true;
            celda.Style.Font.FontSize = 10;
        }
    }

    /// <summary>Un rotulo que ocupa varias columnas fundidas.</summary>
    /// <param name="hoja">La hoja.</param>
    /// <param name="fila">En que fila.</param>
    /// <param name="desde">La primera columna.</param>
    /// <param name="hasta">La ultima columna.</param>
    /// <param name="rotulo">El rotulo.</param>
    internal static void RotuloFundido(IXLWorksheet hoja, int fila, string desde, string hasta, string rotulo)
    {
        var rango = hoja.Range($"{desde}{fila}:{hasta}{fila}");
        Pintar(rango, Marino, Blanco);
        var celda = rango.Merge().FirstCell();
        celda.SetValue(rotulo);
        celda.Style.Font.Bold = true;
        celda.Style.Font.FontSize = 10;
    }

    /// <summary>La fila de total: negrita, fondo panel y una raya media marino encima.</summary>
    /// <param name="hoja">La hoja.</param>
    /// <param name="fila">En que fila.</param>
    /// <param name="desde">La primera columna, en numero.</param>
    /// <param name="hasta">La ultima columna, en numero.</param>
    private static void Total(IXLWorksheet hoja, int fila, int desde, int hasta)
    {
        hoja.Row(fila).Height = 16.5;
        var rango = hoja.Range(fila, desde, fila, hasta);
        rango.Style.Font.Bold = true;
        rango.Style.Fill.BackgroundColor = Panel;
        RayaEncima(rango, XLBorderStyleValues.Medium, Marino);
    }

    /// <summary>La marca de «devolvió»: ✓ verde, ✗ roja o una raya gris, centrada y en Segoe UI Symbol.</summary>
    /// <param name="celda">La celda.</param>
    /// <param name="devolvio">Sí, no, o nulo si no hay agente.</param>
    private static void Marca(IXLCell celda, bool? devolvio)
    {
        var (texto, color) = devolvio switch
        {
            true => (Si, VerdeMarca),
            false => (No, RojoMarca),
            null => (Raya, Apagado),
        };
        celda.SetValue(texto);
        celda.Style.Font.FontName = LetraDeLasMarcas;
        celda.Style.Font.FontColor = color;
        AlCentro(celda);
    }

    /// <summary>Un texto que entra como texto pase lo que pase: el numero de unidad y el de caso.</summary>
    /// <param name="celda">La celda.</param>
    /// <param name="valor">El texto.</param>
    internal static void Texto(IXLCell celda, string valor)
    {
        celda.Style.NumberFormat.Format = FormatoDeTexto;
        celda.SetValue(valor);
    }

    /// <summary>«3 de 3».</summary>
    /// <param name="cuantos">Los devueltos.</param>
    /// <param name="deCuantos">Los que habia.</param>
    private static string DeCuantos(int cuantos, int deCuantos)
        => $"{HojaDelResumen.Numero(cuantos)} de {HojaDelResumen.Numero(deCuantos)}";
}
