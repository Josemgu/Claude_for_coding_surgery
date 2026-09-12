using Fichas.Reportes.Consultas;

namespace Fichas.Reportes.Reglas;

/// <summary>
/// Lo que hoy no se puede saber del reporte, y por que no se puede.
/// </summary>
/// <remarks>
/// Van aparte del armado del documento porque son otra responsabilidad, y de las que importan:
/// el documento CUENTA, y esto DICE DE QUE NO SE FIA el numero que acaba de contar. Es la
/// mitad del reporte que evita que un cero se lea como «ningún problema» cuando significa
/// «nadie lo anotó».
///
/// <b>Ninguno esta escrito a mano.</b> Cada aviso nace de una comprobacion —una lista vacia,
/// un conteo mayor que cero— y desaparece solo cuando la causa desaparece. Un aviso fijo que
/// dice «esto no se puede calcular» seguiria ahi el dia que si se pueda, y entonces el reporte
/// estaria mintiendo al reves.
///
/// ⚠️ <b>Los DOS defectos que el pase manda arreglar al portar, medidos antes de tocar nada:</b>
/// <list type="bullet">
///   <item>
///     «<c>reportes/avisos.py:64</c> hace <c>sorted({numero_caso})</c> y revienta con un caso sin
///     numero»: la linea 64 es texto de la cadena de documentacion. El <c>sorted</c> de verdad
///     esta en la linea 80 y YA lleva <c>or SIN_NUMERO_DE_CASO</c> — el defecto esta arreglado
///     en el Python desde la version 7 del esquema. Aqui se porta arreglado y hay una prueba
///     que lo fija.
///   </item>
///   <item>
///     «<c>avisos.py:100</c> tiene un “1 personas”»: el archivo tiene 97 lineas. El defecto SI
///     existe, en la linea 83, y se arregla aqui con <see cref="Plural"/>.
///   </item>
/// </list>
/// </remarks>
public static class Avisos
{
    /// <summary>Como se nombra aqui un caso al que no se le pudo leer el numero.</summary>
    /// <remarks>
    /// Va escrito en esta capa y no importado de la de pantallas: ningun modulo de reportes
    /// mira la interfaz, y abrir esa dependencia por una cadena de texto pondria la capa de
    /// pantallas debajo de la de documentos.
    /// </remarks>
    public const string SinNumeroDeCaso = "(sin número de caso)";

    /// <summary>Existe mientras nadie haya dicho que valor significa «resuelta».</summary>
    /// <remarks>
    /// Se apaga solo. Desde el 2026-09-03 «completa» resuelve (DECISIONES.md), asi que hoy
    /// devuelve nulo y el aviso NO sale. Se conserva la funcion porque la decision se puede
    /// deshacer, y entonces el aviso tiene que volver sin que nadie se acuerde de escribirlo.
    /// </remarks>
    public static string? DeLaRecomendacionCompleta()
    {
        // La lista de estados que resuelven vive en el contrato: hoy lleva «completa».
        if (Estados.SinResolver("completa") == false) return null;

        return "«¿Recomendación completa?» sale «no se puede saber» en todas las filas, y no es "
               + "un fallo del reporte. De los valores de estado de recomendación que constan en "
               + "el material del proyecto, NINGUNO significa que la recomendación esté resuelta; "
               + "el valor que lo significaría todavía no lo ha dicho el dueño. Hasta que lo diga, "
               + "este reporte prefiere decir que no lo sabe antes que afirmar que alguien viajó "
               + "con todo en regla.";
    }

    /// <summary>Existe cuando hay casos del periodo de los que nadie ha dicho nada.</summary>
    /// <remarks>
    /// Es el aviso que impide leer la metrica 3 al reves. Un cero en «problemas detectados»
    /// puede ser un mes sin problemas o un mes sin anotar, y sin este aviso las dos cosas se ven
    /// exactamente igual.
    /// </remarks>
    /// <param name="deteccion">La métrica 3 ya calculada; se mira <see cref="Deteccion.SinEstadoRegistrado"/>.</param>
    /// <returns>El aviso, o nulo si todos los casos del periodo tienen estado escrito.</returns>
    public static string? DeLosCasosSinEstado(Deteccion deteccion)
    {
        if (deteccion.SinEstadoRegistrado == 0) return null;

        var cuantos = deteccion.SinEstadoRegistrado;
        var total = deteccion.CasosDelPeriodo;
        return $"{cuantos} de los {total} "
               + Plural.Palabra(total, "casos", "casos")
               + " que viajaban en el período no "
               + Plural.Palabra(cuantos, "tiene", "tienen")
               + " escrito ningún estado de recomendación. "
               + (cuantos == 1 ? "Ese caso NO cuenta" : "Esos casos NO cuentan")
               + " como problema en la métrica 3: "
               + (cuantos == 1 ? "cuenta" : "cuentan")
               + " como que nadie ha dicho nada. Si la métrica 3 sale en cero, puede ser porque "
               + "no hubo problemas o porque nadie los anotó, y con estos datos no se puede "
               + "distinguir una cosa de la otra.";
    }

    /// <summary>Existe cuando alguien no pudo viajar y su caso no tiene fecha de viaje.</summary>
    /// <remarks>
    /// Sin fecha no cabe en ningun periodo, asi que no sale en ninguna de las dos partes. Una
    /// persona que no pudo viajar y no aparece en ningun reporte es exactamente la que se
    /// pierde, y por eso el aviso da los numeros de caso: con ellos se arregla en un minuto
    /// poniendoles la fecha.
    ///
    /// ⚠️ <b>Un caso PUEDE no tener numero, y eso rompia el reporte entero.</b> Desde la version
    /// 7 del esquema el numero admite nulo, y desde la identidad por documento esos casos llegan
    /// hasta aqui. Ordenar un conjunto donde cae un nulo junto a texto levantaba
    /// <c>TypeError</c> en Python, asi que el informe completo se caia por culpa del caso que
    /// mas falta hace mirar. Se le pone la palabra ANTES de ordenar, y asi el conjunto es de
    /// texto y el orden vuelve a estar definido.
    ///
    /// El orden es ordinal a proposito: es el mismo que da <c>sorted</c> sobre cadenas en
    /// Python, y con el <c>«(sin número de caso)»</c> queda primero porque el parentesis va
    /// antes que cualquier letra.
    /// </remarks>
    /// <param name="personasSinFecha">Las anotadas como que no pudieron viajar cuyo caso no tiene fecha de viaje.</param>
    /// <returns>El aviso con los números de caso, o nulo si la lista viene vacía.</returns>
    public static string? DeLosQueNoCabenEnElPeriodo(IReadOnlyList<PersonaConSuCaso> personasSinFecha)
    {
        if (personasSinFecha.Count == 0) return null;

        var casos = string.Join(", ", personasSinFecha
            .Select(f => string.IsNullOrWhiteSpace(f.Caso.NumeroCaso) ? SinNumeroDeCaso : f.Caso.NumeroCaso!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal));

        var cuantas = personasSinFecha.Count;
        return Plural.Con(cuantas, "persona anotada", "personas anotadas")
               + " como que "
               + Plural.Palabra(cuantas, "no pudo viajar", "no pudieron viajar")
               + " NO "
               + Plural.Palabra(cuantas, "sale", "salen")
               + " en la parte 2 porque su caso no tiene fecha de viaje, y sin fecha no "
               + Plural.Palabra(cuantas, "cabe", "caben")
               + $" en ningún período. Están en los casos: {casos}. Ponerles la fecha de viaje "
               + (cuantas == 1 ? "la hace aparecer." : "las hace aparecer.");
    }

    /// <summary>Los avisos que hoy tocan, cada uno derivado de una medicion.</summary>
    /// <param name="deteccion">La métrica 3 ya calculada.</param>
    /// <param name="personasSinFecha">Las que no pudieron viajar y no tienen fecha de viaje.</param>
    /// <returns>Solo los avisos que existen, en el orden fijo de los tres; vacía si no toca ninguno.</returns>
    public static IReadOnlyList<string> DelReporte(
        Deteccion deteccion, IReadOnlyList<PersonaConSuCaso> personasSinFecha)
        => new[]
            {
                DeLaRecomendacionCompleta(),
                DeLosCasosSinEstado(deteccion),
                DeLosQueNoCabenEnElPeriodo(personasSinFecha),
            }
            .Where(aviso => aviso is not null)
            .Select(aviso => aviso!)
            .ToList();
}
