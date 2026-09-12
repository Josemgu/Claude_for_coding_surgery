using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Datos.Falso;

/// <summary>
/// Una extraccion que solo sabe leer las anclas mas simples de las lineas inventadas.
/// </summary>
/// <remarks>
/// ⚠️ NO son las reglas de verdad. Las de verdad son 1 159 pruebas en Python y se portan
/// en la fase C3, no aqui. Esto existe para que la pantalla de correccion tenga algo que
/// pintar mientras tanto. Regla permanente 1: no adivina nada; lo que no encuentra sale
/// con origen «vacio» y valor nulo.
/// <para>
/// ⚠️ <b>Los nombres de campo son los de las COLUMNAS, en espanol y con guion bajo</b>, que
/// es lo que devuelve la extraccion de verdad (<c>Fichas.Lectura.Extraccion</c>) y lo que
/// viaja a <c>procedencia_campo.campo</c>. Hasta el 2026-09-04 aqui iban los nombres de las
/// propiedades de C# («TemploNombre»), y con ellos una prueba de bandas pasaba en verde
/// midiendo una coincidencia que en el programa de verdad no existia. Van escritos y no
/// tomados de <c>Fichas.Lectura</c>: este proyecto solo conoce <c>Fichas.Contratos</c>.
/// </para>
/// </remarks>
public sealed class ExtraccionFalsa : IExtraccion
{
    /// <summary>Propone los campos del caso buscando la etiqueta de cada uno en las lineas.</summary>
    /// <remarks>Solo tres: <c>templo_nombre</c>, <c>fecha_viaje</c> y <c>unidad_nombre</c>. Las anotaciones no se usan para proponer nada; solo se cuenta cuántas hay para avisarlo.</remarks>
    /// <param name="lineas">Las líneas de OCR de la hoja; vacía deja los tres campos con origen vacío.</param>
    /// <param name="anotaciones">Las anotaciones de la hoja; solo se cuentan.</param>
    /// <returns>Siempre tres campos, con valor o sin él, y una advertencia si alguno quedó sin leer.</returns>
    public ResultadoDeExtraccion ProponerCamposDelCaso(
        IReadOnlyList<LineaDeOcr> lineas,
        IReadOnlyList<AnotacionDelPdf> anotaciones)
    {
        var campos = new List<CampoPropuesto>
        {
            BuscarTrasLaEtiqueta(lineas, "Temple Name:", TablaDeProcedencia.Casos, "templo_nombre"),
            BuscarTrasLaEtiqueta(lineas, "Fecha de viaje:", TablaDeProcedencia.Casos, "fecha_viaje"),
            BuscarTrasLaEtiqueta(lineas, "Unit Number:", TablaDeProcedencia.Casos, "unidad_nombre"),
        };

        var avisos = new List<Aviso>();
        var vacios = campos.Count(c => c.Valor is null);
        if (vacios > 0)
        {
            avisos.Add(Aviso.Advierte(
                $"{vacios} campo(s) del caso quedaron sin leer.",
                string.Empty,
                "No se inventa ninguno: quedan vacios y marcados para que los teclees en la correccion."));
        }
        if (anotaciones.Count > 0)
        {
            avisos.Add(Aviso.Informa(
                $"La hoja trae {anotaciones.Count} anotacion(es) escritas encima.",
                string.Empty,
                "Una anotacion manda sobre lo que lea el OCR debajo."));
        }

        return new ResultadoDeExtraccion(campos, avisos);
    }

    /// <summary>Propone una persona por cada linea que parezca traer un MRN.</summary>
    /// <param name="lineas">Las líneas de OCR de la hoja; se busca en cada una la forma 000-0000-0000.</param>
    /// <param name="anotaciones">No se usan aquí; están por cumplir el contrato.</param>
    /// <returns>Un campo <c>mrn</c> por persona reconocida, numeradas por orden de aparición; sin ninguna, la lista vacía con una advertencia y nunca una persona inventada.</returns>
    public ResultadoDeExtraccion ProponerCamposDePersonas(
        IReadOnlyList<LineaDeOcr> lineas,
        IReadOnlyList<AnotacionDelPdf> anotaciones)
    {
        var campos = new List<CampoPropuesto>();
        var fila = 0;
        foreach (var linea in lineas)
        {
            var mrn = BuscarUnMrn(linea.Texto);
            if (mrn is null) continue;
            fila++;
            campos.Add(new CampoPropuesto(
                TablaDeProcedencia.Personas, "mrn", mrn,
                OrigenDeCampo.Ocr, linea.Confianza, linea.Banda, fila));
        }

        return campos.Count > 0
            ? new ResultadoDeExtraccion(campos, [])
            : ResultadoDeExtraccion.Nada(Aviso.Advierte(
                "No se reconocio ninguna persona en esta hoja.",
                string.Empty,
                "La hoja se guarda igual y queda en la lista de documentos por revisar."));
    }

    /// <summary>Recorta espacios y devuelve el valor tal cual; no arregla nada (regla permanente 1).</summary>
    /// <param name="campo">La columna a la que va; se copia tal cual en el campo propuesto, siempre con tabla <c>casos</c>.</param>
    /// <param name="valorLeido">Lo leído; nulo, vacío o solo espacios da un campo con origen vacío y valor nulo.</param>
    /// <returns>Un solo campo propuesto, sin confianza ni banda.</returns>
    public ResultadoDeExtraccion Normalizar(string campo, string? valorLeido)
    {
        var limpio = valorLeido?.Trim();
        var campos = new[]
        {
            new CampoPropuesto(
                TablaDeProcedencia.Casos, campo, string.IsNullOrEmpty(limpio) ? null : limpio,
                string.IsNullOrEmpty(limpio) ? OrigenDeCampo.Vacio : OrigenDeCampo.Ocr,
                null, null),
        };
        return new ResultadoDeExtraccion(campos, []);
    }

    /// <summary>Busca la primera linea que empiece por la etiqueta y devuelve lo que va detras.</summary>
    /// <remarks>Si la etiqueta aparece pero no lleva nada detrás, se deja de buscar: ese es el campo vacío del papel, no un motivo para mirar la línea siguiente.</remarks>
    /// <param name="lineas">Dónde buscar.</param>
    /// <param name="etiqueta">El texto fijo que precede al valor, sin distinguir mayúsculas.</param>
    /// <param name="tabla">A qué tabla irá el campo propuesto.</param>
    /// <param name="campo">El nombre de la columna que se propone.</param>
    /// <returns>El campo con el valor, la confianza y la banda de esa línea; o con origen vacío y valor nulo si no se encontró.</returns>
    private static CampoPropuesto BuscarTrasLaEtiqueta(
        IReadOnlyList<LineaDeOcr> lineas, string etiqueta, TablaDeProcedencia tabla, string campo)
    {
        foreach (var linea in lineas)
        {
            var donde = linea.Texto.IndexOf(etiqueta, StringComparison.OrdinalIgnoreCase);
            if (donde < 0) continue;
            var valor = linea.Texto[(donde + etiqueta.Length)..].Trim();
            if (valor.Length == 0) break;
            return new CampoPropuesto(tabla, campo, valor, OrigenDeCampo.Ocr, linea.Confianza, linea.Banda);
        }
        return new CampoPropuesto(tabla, campo, null, OrigenDeCampo.Vacio, null, null);
    }

    /// <summary>Busca un MRN con la forma 000-0000-0000 dentro de un texto; nulo si no hay.</summary>
    /// <remarks>Once dígitos exactos: aquí no se admite la letra final que sí admite el guardado de personas, porque esto solo tiene que reconocer los MRN que inventa el generador.</remarks>
    /// <param name="texto">La línea entera; se prueba cada ventana de 13 caracteres.</param>
    /// <returns>El primer trozo que cumpla la forma, o nulo.</returns>
    private static string? BuscarUnMrn(string texto)
    {
        for (var i = 0; i + 13 <= texto.Length; i++)
        {
            var trozo = texto.Substring(i, 13);
            if (trozo[3] == '-' && trozo[8] == '-'
                && trozo.Where((_, j) => j is not 3 and not 8).All(char.IsAsciiDigit))
            {
                return trozo;
            }
        }
        return null;
    }
}
