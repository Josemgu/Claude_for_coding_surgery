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
