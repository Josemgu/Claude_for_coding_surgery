using Fichas.Contratos.Modelos;

namespace Fichas.Reportes.Reglas;

/// <summary>
/// Cual de los valores de <c>estado_recomendacion</c> significa que ya no hay nada que hacer.
/// </summary>
/// <remarks>
/// Portado de <c>datos/estados.py</c>. Desde el 2026-09-03 el unico que resuelve es
/// <b>«completa»</b>, que es la palabra que dijo el dueno (DECISIONES.md). <c>no_completa</c>
/// NO entra, y no es un descuido: un documento que Miguel —o la hoja del companero— dio por
/// incompleto es justo el que tiene que seguir saliendo en el bloque rojo.
///
/// ⚠️ <b>Una diferencia medida con el Python, y va dicha.</b> El Python compara el texto crudo
/// contra la tupla, asi que «Completa» con mayuscula le sale SIN resolver. Aqui se lee con
/// <see cref="Caso.LeerEstado"/>, que es el contrato del programa nuevo y recorta espacios y
/// mayusculas, asi que «Completa » le sale resuelta. Se usa el contrato porque es lo que ya
/// leen las demas pantallas: dos lecturas distintas del mismo texto en el mismo programa es
/// justo lo que nadie puede depurar.
/// </remarks>
public static class Estados
{
    /// <summary>Si de ese caso NO consta ningun estado escrito. Nulo y vacio son lo mismo aqui.</summary>
    /// <remarks>
    /// Es lo contrario de «sin problema»: un caso del que nadie ha dicho nada no es un caso
    /// tranquilo, y sale en su propio numero en vez de repartido entre los otros.
    /// </remarks>
    /// <param name="estadoRecomendacion">El texto crudo de la columna <c>estado_recomendacion</c>.</param>
    public static bool SinEstadoEscrito(string? estadoRecomendacion)
        => string.IsNullOrWhiteSpace(estadoRecomendacion);

    /// <summary>Si esa recomendacion sigue pendiente. Un estado que no consta cuenta como pendiente.</summary>
    /// <param name="estadoRecomendacion">El texto crudo de la columna; se lee con <see cref="Caso.LeerEstado"/>.</param>
    public static bool SinResolver(string? estadoRecomendacion)
        => Caso.LeerEstado(estadoRecomendacion) != EstadoDeRecomendacion.Completa;

    /// <summary>«sí», «no» o la palabra que dice que no se puede saber. Nunca se rellena a ojo.</summary>
    /// <param name="estadoRecomendacion">El texto crudo de la columna; vacío o nulo da <see cref="Vocabulario.SinDato"/>.</param>
    public static string TextoDeRecomendacionCompleta(string? estadoRecomendacion)
    {
        if (SinEstadoEscrito(estadoRecomendacion)) return Vocabulario.SinDato;
        return SinResolver(estadoRecomendacion) ? "no" : "sí";
    }

    /// <summary>Por que no esta completa, con las palabras del dueno.</summary>
    /// <remarks>
    /// Las tres frases son suyas, del 2026-09-05: <i>«no completado, no se pudo comunicar con el
    /// líder, o el líder no lo hizo»</i>. La columna guarda una clave corta
    /// (<c>no_se_pudo_comunicar</c>) y no la prosa, a proposito: asi la redaccion se cambia sin
    /// migrar datos. Este metodo es el unico sitio donde la clave se convierte en frase, para
    /// que el reporte, la pantalla y el Excel no digan tres cosas distintas.
    /// </remarks>
    /// <param name="motivo">La clave guardada; cualquier valor fuera de los tres conocidos da «no se dijo por qué».</param>
    public static string TextoDelMotivo(MotivoDeNoCompletar motivo) => motivo switch
    {
        MotivoDeNoCompletar.NoSePudoComunicar => "no se pudo comunicar con el líder",
        MotivoDeNoCompletar.ElLiderNoLoHizo => "el líder no lo hizo",
        MotivoDeNoCompletar.OtraRazon => "otra razón",
        _ => "no se dijo por qué",
    };

    /// <summary>«sí», «no» o «no consta». Un nulo es una respuesta, no un hueco.</summary>
    /// <param name="pudoViajar">Lo que anotó el compañero, o nulo si no lo dijo.</param>
    public static string TextoDeSiViajo(bool? pudoViajar)
        => pudoViajar switch
        {
            true => "sí",
            false => "no",
            null => Vocabulario.SinDato,
        };
}
