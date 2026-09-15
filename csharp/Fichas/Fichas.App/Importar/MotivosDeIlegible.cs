namespace Fichas.App.Importar;

/// <summary>
/// Los codigos con los que se archiva lo que no entro bien. Un codigo, no una frase.
/// </summary>
/// <remarks>
/// Son los mismos que el Python escribe en <c>datos/ilegibles.py</c>, letra por letra:
/// la base del dueno ya tiene renglones guardados con ellos, y cambiarlos partiria la
/// lista en dos —los de antes y los de ahora— sin que nadie se entere.
///
/// <para>Un codigo y no una frase porque un codigo se agrupa y se cuenta: «¿cuantos
/// documentos no se pudieron abrir este mes?» no se contesta sobre texto libre.</para>
/// </remarks>
public static class MotivosDeIlegible
{
    /// <summary>El archivo no se pudo abrir como PDF.</summary>
    public const string NoSePudoAbrir = "no_se_pudo_abrir";

    /// <summary>El OCR no devolvio ni una linea de esta hoja.</summary>
    public const string SinTexto = "sin_texto";

    /// <summary>La hoja entro pero sin numero de caso: hay que teclearlo a mano.</summary>
    public const string SinNumeroDeCaso = "sin_numero_de_caso";

    /// <summary>La hoja entro repitiendo a un caso que ya estaba. NUNCA es un rechazo.</summary>
    public const string EntroComoDuplicado = "entro_como_duplicado";

    /// <summary>La hoja decia ser del mismo caso que su hermana pero la contradecia.</summary>
    public const string HojaAparte = "hoja_aparte";

    /// <summary>
    /// Esta hoja leyo un campo del caso que el caso NO tiene, porque la hoja que lo abrio
    /// no lo leyo. Se une igual; lo que deja es el rastro de que alguien SI lo leyo.
    /// </summary>
    /// <remarks>
    /// ⚠️ Este codigo es NUEVO (2026-09-07) y es lo contrario de <see cref="HojaAparte"/>:
    /// alli dos hojas dicen cosas DISTINTAS y no pueden ser ciertas a la vez, asi que la
    /// segunda entra aparte; aqui solo hay UNA lectura y nadie la contradice, asi que la
    /// hoja se une y el caso no cambia.
    ///
    /// <para><b>Por que un renglon y no meterlo en el caso.</b> <c>procedencia_campo</c> no
    /// tiene columna de pagina —son 16 y ninguna lo es—, y la pantalla de correccion dibuja
    /// la banda de un campo del caso sobre <c>casos.pagina_pdf</c>, que es la hoja que lo
    /// abrio. Meter ahi el valor de otra pagina pintaria su recuadro sobre la pagina
    /// equivocada: seria volver a cruzar las informaciones. El renglon SI tiene
    /// <c>pagina_pdf</c> y <c>caso_id</c>, asi que es el unico sitio de la base donde hoy se
    /// puede decir «esto lo leyo la pagina 4 y le falta al caso 7».</para>
    ///
    /// <para><b>Cuanto sale.</b> Medido el 2026-09-07 sobre las 20 hojas de los diez PDF
    /// reales del dueno: <b>0 veces</b>. Las hojas que leen la unidad leen todas lo mismo, y
    /// las que no la leen no leen ningun campo. No puede inundar la franja de avisos.</para>
    /// </remarks>
    public const string LoLeyoOtraHoja = "lo_leyo_otra_hoja";

    /// <summary>
    /// El motor no acepto el numero de caso leido, y el caso entro sin el.
    /// </summary>
    /// <remarks>
    /// ⚠️ Este codigo es NUEVO: no existe en el Python. Existe porque el esquema lleva
    /// <c>CHECK (numero_caso GLOB '[A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9]')</c> y el
    /// OCR lee `CASP2609` como `CASP26O9` —una letra O por un cero— con la frecuencia
    /// suficiente para que importe. Con ese CHECK la fila no entra en SQLite y el
    /// documento entero se perderia; asi entra sin numero, con el valor crudo escrito
    /// aqui para que Miguel lo teclee.
    ///
    /// <para>Quitar el CHECK es la salida que el supervisor recomienda (PENDIENTES.md,
    /// FASE C2), y es una decision del dueno que todavia no esta tomada. Cuando la tome,
    /// este camino deja de dispararse solo: no hay que borrar nada.</para>
    /// </remarks>
    public const string NumeroNoAceptado = "numero_no_aceptado";

    /// <summary>
    /// El motor no acepto OTRO campo del caso, y el caso entro sin el.
    /// </summary>
    /// <remarks>
    /// ⚠️ Este codigo tambien es NUEVO, y existe por una medicion del 2026-09-05: sobre los dos
    /// formularios de grupo del dueno, <b>4 de 20 hojas no entraban</b>. Sus paginas 5 y 6 vienen
    /// del reves en el escaneo, asi que el OCR devolvia <c>«WANLCA Bzeench»</c> donde va el numero
    /// de unidad y <c>«eptomber 2026 Currency:»</c> donde va la fecha; el esquema lleva un
    /// <c>CHECK … GLOB</c> en las dos columnas y rechazaba la fila entera, con sus personas dentro.
    ///
    /// <para>Va aparte de <see cref="NumeroNoAceptado"/> y no lo sustituye: aquel ya esta escrito
    /// en los renglones de la base del dueno, y juntarlos partiria la cuenta de «cuantos hay que
    /// teclear el numero» sin que nadie se entere. Cual fue el campo y que decia lo dicen los
    /// avisos de ese mismo renglon.</para>
    /// </remarks>
    public const string CampoNoAceptado = "campo_no_aceptado";

    /// <summary>
    /// La hoja no es un formulario de recomendación: no se encontró ninguna de sus nueve
    /// etiquetas impresas. No abre caso.
    /// </summary>
    /// <remarks>
    /// ⚠️ Codigo NUEVO del 2026-09-15. Medido con el lector real sobre un impreso sintetico
    /// de otra clase: 9 de 9 etiquetas ausentes y un solo campo, el token «FORD2610» con
    /// forma de numero de caso. Sin este codigo, ese impreso nacia como caso «FORD2610 · sin
    /// unidad leida · 0 personas», que es lo que el dueño vio en su lista. El motivo entero
    /// esta en <c>GuardadoDeHojas.Papel.cs</c>.
    /// </remarks>
    public const string FormularioDesconocido = "formulario_desconocido";

    /// <summary>
    /// El archivo del que salio el caso contiene hoy OTRO papel: el caso se quedo sin escaneo.
    /// </summary>
    /// <remarks>
    /// ⚠️ Codigo NUEVO del 2026-09-15. Lo escribe la comprobacion de los papeles
    /// (<see cref="ComprobacionDeLosPapeles"/>) cuando varios documentos apuntan a la misma
    /// hoja del mismo archivo con numeros distintos y lo que hay en el archivo es de otro:
    /// los que no son de ese papel pierden la ruta, y este renglon dice donde estaba y que
    /// hay ahora ahi.
    /// </remarks>
    public const string PapelPerdido = "papel_perdido";

    /// <summary>
    /// Como se lee ese codigo en una pantalla, en espanol.
    /// </summary>
    /// <remarks>
    /// <para>Los codigos se guardan y se cuentan; lo que se ENSENA es esto. Un renglon que
    /// dijera <c>no_se_pudo_abrir</c> delante del dueno le hace traducir jerga para saber
    /// que le paso a su papel.</para>
    ///
    /// <para>⚠️ <b>Un codigo que no este en esta lista se devuelve TAL CUAL</b>, y es a
    /// proposito: la base del dueno lleva renglones escritos por el programa viejo en
    /// Python, y manana puede aparecer un codigo nuevo. Un codigo feo delante es mejor que
    /// un renglon mudo —al menos se puede buscar—, y es la misma decision que
    /// <c>EtiquetasDeLaCarga</c> tomo en el borrado.</para>
    /// </remarks>
    /// <param name="codigo">El codigo tal como esta en <c>documentos_ilegibles.motivo</c>.</param>
    public static string EnEspanol(string? codigo) => codigo switch
    {
        NoSePudoAbrir => "no se pudo abrir el archivo",
        SinTexto => "no se leyó ni una línea",
        SinNumeroDeCaso => "no se leyó el número de caso",
        EntroComoDuplicado => "repite a un documento que ya estaba",
        HojaAparte => "la hoja contradecía a su hermana",
        LoLeyoOtraHoja => "lo leyó otra hoja del mismo documento",
        NumeroNoAceptado => "la base no aceptó el número de caso leído",
        CampoNoAceptado => "la base no aceptó un campo leído",
        FormularioDesconocido => "no es un formulario de recomendación",
        PapelPerdido => "el archivo contiene hoy otro papel: el documento se quedó sin escaneo",
        null or "" => "sin motivo anotado",
        _ => codigo,
    };
}
