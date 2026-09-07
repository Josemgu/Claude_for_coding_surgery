using Fichas.Contratos.Modelos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Importar;

/// <summary>Una carpeta de dentro de lo elegido en la que el sistema no dejo entrar.</summary>
/// <remarks>
/// Lleva la ruta ENTERA y no solo el nombre: con 3 000 formularios repartidos por meses
/// hay muchas carpetas que se llaman igual, y «no se pudo leer "septiembre"» no le dice al
/// dueño cual de las cuatro.
/// </remarks>
/// <param name="Ruta">La carpeta, con su ruta completa.</param>
/// <param name="Motivo">Por que no se pudo, escrito para leerlo, no un codigo.</param>
public sealed record CarpetaQueNoSeDejoLeer(string Ruta, string Motivo);

/// <summary>
/// Lo que salio de recorrer lo elegido: los PDF, y lo que se quedo fuera.
/// </summary>
/// <remarks>
/// <para>Son las dos mitades del requisito 9 del proyecto. <see cref="Pdf"/> es «avisar,
/// nunca impedir»: una carpeta que no se deja leer no cuesta la tanda entera.
/// <see cref="CarpetasQueNoSeDejaronLeer"/> es la otra mitad, «nunca fallar callado»: lo
/// que no entro se puede nombrar.</para>
///
/// <para>El texto de los avisos se compone AQUI y no en la pantalla, por lo mismo que
/// <see cref="ResumenDeLaTanda"/>: asi se puede probar sin abrir ninguna ventana, que es lo
/// unico que hace que estas frases esten medidas y no supuestas.</para>
/// </remarks>
/// <param name="Pdf">Los PDF que si se encontraron, ordenados y sin repetidos.</param>
/// <param name="CarpetasQueNoSeDejaronLeer">Lo que se quedo fuera, con su motivo.</param>
public sealed record LoQueSeEncontro(
    IReadOnlyList<string> Pdf,
    IReadOnlyList<CarpetaQueNoSeDejoLeer> CarpetasQueNoSeDejaronLeer)
{
    /// <summary>
    /// Lo que hay que decirle al dueño de esta busqueda, o nulo si no hay nada que decir.
    /// </summary>
    /// <remarks>
    /// <para>Nulo SOLO cuando entro algo y no se quedo nada fuera, que es el caso normal:
    /// entonces el aviso que vale es el resumen de la tanda, y uno de mas seria ruido.</para>
    ///
    /// <para>⛔ Los tres textos son distintos a proposito, y el que mas importa es el
    /// tercero. «No habia ningun PDF» y «no me dejaron mirar» son cosas DISTINTAS: decir la
    /// primera cuando pasa la segunda deja al dueño tranquilo creyendo que ahi no habia
    /// nada que importar, y esos documentos no vuelven a mirarse nunca.</para>
    /// </remarks>
    public Aviso? AvisoDeLaBusqueda()
    {
        if (Pdf.Count == 0 && CarpetasQueNoSeDejaronLeer.Count > 0)
        {
            return Aviso.Problema(
                AvisoDeUnFalloEnPantalla.Recortar(
                    $"No se pudo entrar en {CuantasCarpetas()} de lo que eligió "
                    + $"—{LasQueSeNombran()}— y en lo demás no se encontró ningún PDF: "
                    + "no se importó nada."),
                string.Empty,
                DetalleDeLoQueSeQuedoFuera());
        }

        if (Pdf.Count == 0)
        {
            return Aviso.Advierte(
                "En lo que eligió no había ningún PDF: no se importó nada.",
                string.Empty,
                "Se buscaron archivos con extensión .pdf, también dentro de las subcarpetas, "
                + "y se pudo entrar en todas. Si esperaba encontrarlos, compruebe que no sean "
                + "imágenes sueltas o archivos de otro tipo.");
        }

        if (CarpetasQueNoSeDejaronLeer.Count > 0)
        {
            return Aviso.Advierte(
                AvisoDeUnFalloEnPantalla.Recortar(
                    $"{Plural.Palabra(Pdf.Count, "Entró", "Entraron")} {Pdf.Count} PDF; "
                    + $"no se pudo entrar en {CuantasCarpetas()}: {LasQueSeNombran()}."),
                string.Empty,
                DetalleDeLoQueSeQuedoFuera());
        }

        return null;
    }

    /// <summary>Un renglon por carpeta para <c>fichas.log</c>, con la ruta entera.</summary>
    /// <remarks>
    /// Va aparte del aviso y SIN recortar. La franja se cierra; el cuaderno es lo que queda
    /// para poder decir despues cual fue exactamente la carpeta que se quedo fuera.
    /// </remarks>
    public IEnumerable<string> RenglonesParaElCuaderno()
        => CarpetasQueNoSeDejaronLeer.Select(
            carpeta => $"IMPORTACIÓN  no se pudo entrar en «{carpeta.Ruta}»: {carpeta.Motivo}");

    /// <summary>Lo largo: todas las carpetas con su ruta entera y su motivo.</summary>
    private string DetalleDeLoQueSeQuedoFuera()
    {
        var texto = new System.Text.StringBuilder();
        texto.AppendLine(
            $"No se pudo entrar en {CuantasCarpetas()}, así que lo que hubiera dentro no entró:");
        texto.AppendLine();
        foreach (var carpeta in CarpetasQueNoSeDejaronLeer)
        {
            texto.AppendLine($"· {carpeta.Ruta} — {carpeta.Motivo}");
        }

        texto.AppendLine();
        texto.AppendLine(
            "Todo lo demás sí se leyó. Windows crea dentro de la carpeta de cada usuario "
            + "accesos heredados —«Application Data», «Mis documentos», «Configuración "
            + "local»— que él mismo no deja abrir; si eligió su carpeta de usuario, son "
            + "esos y no falta nada suyo. Si alguna de estas carpetas es suya y esperaba "
            + "documentos ahí, elíjala directamente para ver qué pasa con ella.");
        return texto.ToString();
    }

    /// <summary>«1 carpeta» o «3 carpetas».</summary>
    private string CuantasCarpetas()
        => Plural.Con(CarpetasQueNoSeDejaronLeer.Count, "carpeta", "carpetas");

    /// <summary>
    /// La primera por su nombre y, si hay mas, cuantas quedan.
    /// </summary>
    /// <remarks>
    /// En la linea va el NOMBRE y no la ruta entera: la franja pinta un renglon (requisito 4)
    /// y tres rutas de Windows no caben. Las rutas enteras estan en el detalle y en el
    /// cuaderno, que es donde se va a mirar cuando haga falta saber cual era exactamente.
    /// </remarks>
    private string LasQueSeNombran()
    {
        var primera = $"«{NombreDe(CarpetasQueNoSeDejaronLeer[0].Ruta)}»";
        var faltan = CarpetasQueNoSeDejaronLeer.Count - 1;
        return faltan == 0 ? primera : $"{primera} y {faltan} más";
    }

    /// <summary>El nombre de la carpeta; si no tiene (una raiz), la ruta entera.</summary>
    private static string NombreDe(string ruta)
    {
        var nombre = Path.GetFileName(ruta.TrimEnd(Path.DirectorySeparatorChar));
        return string.IsNullOrEmpty(nombre) ? ruta : nombre;
    }
}
