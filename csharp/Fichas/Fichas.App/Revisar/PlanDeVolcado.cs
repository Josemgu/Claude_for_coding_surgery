using System.Text;
using Fichas.Contratos.Puertos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Revisar;

/// <summary>
/// Qué carpetas y qué archivos hay que crear para volcar un mes al disco. Se arma sin tocar
/// el disco, y por eso se puede medir sin escribir nada.
/// </summary>
/// <remarks>
/// <para>Del dueño, 2026-09-05: «permite descargarlas por mes en esa organización y carpeta
/// creada, para yo tener todo organizado». La organización es la misma que se ve en
/// pantalla —<see cref="ArbolDeRevisar"/>— y sale del mismo árbol a propósito: si se armara
/// aparte, la pantalla y el disco podrían discrepar y nadie se enteraría.</para>
///
/// <para>Se separa de <see cref="VolcadoDeCarpetas"/> por lo de siempre: lo que decide y lo
/// que escribe no se mezclan. Aquí no hay una sola llamada a <c>File</c> ni a
/// <c>Directory</c>.</para>
/// </remarks>
/// <param name="Mes">El nombre del mes que se vuelca: «Septiembre 2026».</param>
/// <param name="Archivos">Los documentos a copiar, con su carpeta y su nombre de destino.</param>
/// <param name="Textos">Las hojas de personas a escribir, una por carpeta de unidad.</param>
public sealed record PlanDeVolcado(
    string Mes,
    IReadOnlyList<ArchivoAVolcar> Archivos,
    IReadOnlyList<TextoAVolcar> Textos)
{
    /// <summary>Cómo se llama la hoja que va dentro de cada carpeta de unidad.</summary>
    public const string NombreDeLaHojaDePersonas = "Personas de la unidad.txt";

    /// <summary>Lo más largo que se deja medir un nombre de archivo de destino.</summary>
    /// <remarks>
    /// Windows admite 255 por nombre, pero la ruta entera tiene su propio tope y la raíz la
    /// elige el dueño: si él vuelca en una carpeta ya profunda, un nombre largo la pasa. Se
    /// recorta el nombre, nunca la carpeta, porque la carpeta es la organización que pidió.
    /// </remarks>
    private const int LargoMaximoDelNombre = 120;

    /// <summary>Arma el plan de un mes: una carpeta por fecha, otra por unidad y dentro los documentos.</summary>
    /// <param name="mes">El mes ya agrupado por <see cref="ArbolDeRevisar.Agrupar"/>.</param>
    /// <param name="personas">De dónde salen las personas que van en la hoja de cada unidad.</param>
    public static PlanDeVolcado Para(GrupoDeMes mes, IPersonas personas)
    {
        ArgumentNullException.ThrowIfNull(mes);
        ArgumentNullException.ThrowIfNull(personas);

        var archivos = new List<ArchivoAVolcar>();
        var textos = new List<TextoAVolcar>();

        foreach (var fecha in mes.Fechas)
        {
            foreach (var unidad in fecha.Unidades)
            {
                var carpeta = CarpetaDe(mes, fecha, unidad);
                archivos.AddRange(DocumentosDe(unidad, carpeta));
                textos.Add(new TextoAVolcar(
                    carpeta, NombreDeLaHojaDePersonas, HojaDePersonas(fecha, unidad, personas)));
            }
        }

        return new PlanDeVolcado(mes.Carpeta, archivos, textos);
    }

    /// <summary>La ruta relativa mes / fecha / unidad, con cada tramo ya limpio para Windows.</summary>
    private static string CarpetaDe(GrupoDeMes mes, GrupoDeFecha fecha, GrupoDeUnidad unidad)
        => Path.Combine(
            VolcadoDeCarpetas.NombreSeguro(mes.Carpeta),
            VolcadoDeCarpetas.NombreSeguro(fecha.Carpeta),
            VolcadoDeCarpetas.NombreSeguro(unidad.Carpeta));

    /// <summary>Los documentos de una unidad, cada uno con un nombre de destino distinto.</summary>
    private static IEnumerable<ArchivoAVolcar> DocumentosDe(GrupoDeUnidad unidad, string carpeta)
    {
        var yaPuestos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var documento in unidad.Documentos)
        {
            yield return new ArchivoAVolcar(
                carpeta, documento.RutaDelPdf, NombreLibre(NombreDeDestino(documento), yaPuestos));
        }
    }

    /// <summary>
    /// Cómo se llama el documento dentro de la carpeta: número de caso, hoja y archivo de origen.
    /// </summary>
    /// <remarks>
    /// Lleva las tres cosas porque ninguna sola basta: los siete escaneos del dueño comparten
    /// el número <c>CASP2609</c> y son de siete familias distintas, así que el número no
    /// distingue; y un mismo PDF puede traer varias hojas, así que el archivo tampoco.
    /// </remarks>
    private static string NombreDeDestino(TarjetaDeDocumento documento)
    {
        var extension = Path.GetExtension(documento.Archivo);
        var origen = Path.GetFileNameWithoutExtension(documento.Archivo);
        var hoja = documento.Hoja.Length == 0 ? "sin hoja" : documento.Hoja;
        var nombre = $"{documento.NumeroDeCaso} · {hoja} · {origen}";
        if (nombre.Length > LargoMaximoDelNombre) nombre = nombre[..LargoMaximoDelNombre];
        return nombre + extension;
    }

    /// <summary>Un nombre que no esté ya puesto en esa carpeta; el repetido lleva « (2)» detrás.</summary>
    private static string NombreLibre(string propuesto, HashSet<string> yaPuestos)
    {
        if (yaPuestos.Add(propuesto)) return propuesto;

        var raiz = Path.GetFileNameWithoutExtension(propuesto);
        var extension = Path.GetExtension(propuesto);
        for (var intento = 2; ; intento++)
        {
            var otro = $"{raiz} ({intento}){extension}";
            if (yaPuestos.Add(otro)) return otro;
        }
    }

    /// <summary>
    /// La hoja con el paquete de personas de la unidad que viajará: nombre y MRN, por documento.
    /// </summary>
    /// <remarks>
    /// Es lo que el dueño pidió que hubiera dentro de la carpeta de la unidad —«y dentro el
    /// paquete de personas de la unidad que viajará»—. No inventa nada: lo que la base no
    /// tiene sale dicho como que falta, que es un dato (regla permanente 1).
    /// </remarks>
    private static string HojaDePersonas(GrupoDeFecha fecha, GrupoDeUnidad unidad, IPersonas personas)
    {
        var hoja = new StringBuilder();
        hoja.AppendLine($"Personas de la unidad {unidad.Carpeta}");
        hoja.AppendLine(fecha.Carpeta);
        hoja.AppendLine();

        var cantidad = 0;
        foreach (var documento in unidad.Documentos)
        {
            hoja.AppendLine($"{documento.NumeroDeCaso} · {documento.Archivo}");
            var suyas = personas.DeCaso(documento.CasoId);
            if (suyas.Count == 0) hoja.AppendLine("    (este documento no tiene ninguna persona en la base)");
            foreach (var persona in suyas)
            {
                var nombre = string.IsNullOrWhiteSpace(persona.Nombre) ? "sin nombre" : persona.Nombre;
                var mrn = string.IsNullOrWhiteSpace(persona.Mrn) ? "sin MRN" : persona.Mrn;
                hoja.AppendLine($"    {nombre} · {mrn}");
                cantidad++;
            }
            hoja.AppendLine();
        }

        hoja.AppendLine($"{Plural.Con(cantidad, "persona", "personas")} en "
            + Plural.Con(unidad.Documentos.Count, "documento", "documentos") + ".");
        return hoja.ToString();
    }
}

/// <summary>Un documento que hay que copiar al volcar.</summary>
/// <param name="CarpetaRelativa">La ruta desde la raíz elegida, ya limpia para Windows.</param>
/// <param name="RutaDeOrigen">Dónde está el PDF ahora; puede que ya no exista.</param>
/// <param name="NombreDeDestino">Cómo se llamará dentro de la carpeta.</param>
public sealed record ArchivoAVolcar(string CarpetaRelativa, string RutaDeOrigen, string NombreDeDestino);

/// <summary>Un texto que hay que escribir al volcar; hoy solo la hoja de personas.</summary>
/// <param name="CarpetaRelativa">La ruta desde la raíz elegida, ya limpia para Windows.</param>
/// <param name="Nombre">Cómo se llamará el archivo.</param>
/// <param name="Contenido">Lo que va dentro.</param>
public sealed record TextoAVolcar(string CarpetaRelativa, string Nombre, string Contenido);
