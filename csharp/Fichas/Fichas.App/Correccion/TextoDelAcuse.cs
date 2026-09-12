using System.Globalization;

using Fichas.App.Vocabulario;
using Fichas.Contratos.Modelos;

namespace Fichas.App.Correccion;

/// <summary>
/// La linea del pie que dice que se guardo, y que se quedo sin guardar.
/// </summary>
/// <remarks>
/// Portado de <c>interfaz/acuse_de_guardado.py</c>. Va como clase sin ventana a proposito:
/// asi la frase que ve Miguel se comprueba en una prueba, que es la unica forma de que
/// alguien la mire de verdad. El control que la pinta es <c>Cascara/Acuse.xaml</c>, que ya
/// existe y se apaga solo a los pocos segundos: «Guardado a las 09:41» diez minutos
/// despues, con tres campos tecleados encima sin guardar, es una mentira que ademas se cree.
/// </remarks>
public static class TextoDelAcuse
{
    /// <summary>Tope de la linea del pie; mas que esto ya no es una linea.</summary>
    public const int LargoMaximoDeLaLinea = 120;

    /// <summary>
    /// Lo que dice el pie despues de guardar.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>«Sin guardar» solo se escribe cuando de verdad no se guardo.</b> Hasta el
    /// 2026-09-04 esta linea decia «1 campo sin guardar: Cedula (1)» por un valor que la
    /// pantalla se habia negado a escribir; era cierta y era el defecto, porque el
    /// requisito 9 del dueno —«avisar, nunca impedir»— y el texto de la migracion 17
    /// dicen lo contrario: la cedula rara <b>se guarda</b> y se senala. Un valor raro
    /// entra, y entonces el pie tiene que decir que entro y que hay que mirarlo.
    /// <para>
    /// La frase vieja se queda para el UNICO caso en que es cierta: que el almacen se
    /// negara a escribir —por ejemplo, dos personas del mismo caso con la misma cedula,
    /// que sigue siendo <c>UNIQUE (caso_id, mrn)</c>—. Ahi calla la hora: lo que hay que
    /// mirar no es cuando se guardo sino que no entro.
    /// </para>
    /// </remarks>
    /// <param name="hora">La hora en HH:MM que devuelve <see cref="HoraDe"/>; puede ir vacia.</param>
    /// <param name="camposGuardados">Cuantos campos llegaron a escribirse.</param>
    /// <param name="camposSenalados">Los que se guardaron y hay que mirar, en el orden en que se ven.</param>
    /// <param name="camposQueElAlmacenNoAdmitio">Los que el almacen se nego a escribir; casi siempre vacia.</param>
    /// <param name="fraseDelDocumento">
    /// Como esta el documento entero, ya compuesta por <see cref="FraseDelDocumento"/>;
    /// vacia para no decir nada de el.
    /// </param>
    public static string Componer(
        string hora,
        int camposGuardados,
        IReadOnlyList<string> camposSenalados,
        IReadOnlyList<string> camposQueElAlmacenNoAdmitio,
        string fraseDelDocumento = "")
    {
        ArgumentNullException.ThrowIfNull(camposSenalados);
        ArgumentNullException.ThrowIfNull(camposQueElAlmacenNoAdmitio);

        if (camposQueElAlmacenNoAdmitio.Count > 0)
        {
            var cuantos = camposQueElAlmacenNoAdmitio.Count;
            var cabeza = $"Guardado · {cuantos} campo{Plural(cuantos)} sin guardar: ";
            return cabeza + Enumerar(camposQueElAlmacenNoAdmitio, LargoMaximoDeLaLinea - cabeza.Length);
        }

        var principio = string.IsNullOrEmpty(hora) ? "Guardado" : $"Guardado a las {hora}";
        var cuenta = camposGuardados == 0
            ? "sin cambios"
            : $"{camposGuardados} campo{Plural(camposGuardados)}";
        var delDocumento = string.IsNullOrEmpty(fraseDelDocumento) ? string.Empty : $" · {fraseDelDocumento}";

        if (camposSenalados.Count == 0) return $"{principio} · {cuenta}{delDocumento}";

        // Lo que se recorta es la lista de nombres, nunca la frase del documento: la lista
        // ya sabe decir «y 2 más» y la frase no se puede decir a medias.
        var antes = $"{principio} · {cuenta}{delDocumento} · por revisar: ";
        return antes + Enumerar(camposSenalados, LargoMaximoDeLaLinea - antes.Length);
    }

    /// <summary>
    /// Como esta el documento entero: listo para asignar, o cuanto falta para estarlo.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Corregido por el dueno el 2026-09-05, el dia siguiente de escribirse.</b> Salia
    /// cuando MIGUEL habia firmado todos los campos, y el lo corrigio: «Yo doy ese veredicto
    /// cuando el sistema no completa todos los campos. Si el sistema escanea y verifica
    /// todos los campos sin mi intervencion, debe decir "listo para asignar": el sistema
    /// lleno todos los campos y a mi solo me deberia dejar verificarlo».
    /// <para>
    /// Por eso la frase de la falta ya no dice «por dar por buenos», que sonaba a que le
    /// tocaba firmarlos a el: dice cuantos campos le faltan AL DOCUMENTO. Los que le faltan
    /// son los que el programa no pudo dejar cerrados —vacios, de poca confianza, tachados
    /// sin corregir, o con un valor que no cumple su forma—, y esos si son cosa suya.
    /// </para>
    /// <para>
    /// ⛔ Y esto no firma nada ni es un estado de la base: no hay columna que lo guarde.
    /// </para>
    /// </remarks>
    /// <param name="listo">Si el programa dejo el documento sin ningun hueco.</param>
    /// <param name="camposQueLeFaltan">Cuantos campos siguen pidiendo que alguien los mire.</param>
    /// <param name="sinNingunaPersonaLeida">
    /// Si el documento no trae ni una persona. Tiene su propia frase porque no es un campo
    /// que falte: sin ella, un documento sin personas no estaria listo y el pie se quedaria
    /// callado o diria «faltan 0 campos», que no explica nada.
    /// </param>
    public static string FraseDelDocumento(
        bool listo, int camposQueLeFaltan, bool sinNingunaPersonaLeida = false)
    {
        // ⛔ 2026-09-07: AQUÍ SE DECÍA «listo para asignar · el sistema llenó todos los
        // campos», y era una de las cuatro palabras que el dueño retiró. Lo que aquel rótulo
        // decía no se pierde: sigue dicho, entero, en el DETALLE de la lectura.
        //
        // ⚠️ Y la palabra es «me falta» incluso cuando el sistema llenó todos los campos, que
        // es lo que más cuesta leer de este cambio. No es un descuido: él definió «resuelto»
        // como «que no queda nada que él tenga que hacer con eso», y a un documento sin huecos
        // y sin repartir le queda que él lo reparta. La alternativa —que Corrección dijera
        // «resuelto» de un documento que Inicio llama «me falta»— es exactamente el defecto
        // que este pase viene a quitar: la pantalla enseñando una pregunta y él leyendo la
        // otra. Lo que cambia al terminar de corregir es el DETALLE, que pasa de «le faltan 3
        // datos del papel» a «te toca a ti: repartirlo a un compañero».
        var lectura = LoQueSeLeeDeUnDocumento.De(
            EstadoDeRecomendacion.SinMarcar,
            archivado: false,
            cuantoLeFalta: listo ? 0 : camposQueLeFaltan,
            sinNingunaPersonaLeida,
            quienLoLleva: string.Empty,
            firma: string.Empty);

        return $"{lectura.Palabra} · {lectura.Detalle}";
    }

    /// <summary>
    /// Los nombres separados por comas, recortados para que la linea quepa.
    /// </summary>
    /// <remarks>
    /// Se recorta por el final y se dice cuantos quedaron fuera: una linea partida en el
    /// pie no se lee, y un «y 2 más» sigue diciendo que hay dos mas. Lo que NUNCA se
    /// recorta es el primero, porque entonces no se nombraria ninguno.
    /// </remarks>
    /// <param name="nombres">Las etiquetas, en el orden en que se ven; la primera va siempre.</param>
    /// <param name="largoDisponible">Cuantos caracteres quedan en la linea para la lista.</param>
    private static string Enumerar(IReadOnlyList<string> nombres, int largoDisponible)
    {
        var cabidos = new List<string>();
        foreach (var nombre in nombres)
        {
            var conEste = string.Join(", ", [.. cabidos, nombre]);
            var fuera = nombres.Count - cabidos.Count - 1;
            var cola = fuera > 0 ? $" y {fuera} más" : string.Empty;
            if (cabidos.Count > 0 && conEste.Length + cola.Length > largoDisponible) break;
            cabidos.Add(nombre);
        }

        var quedaron = nombres.Count - cabidos.Count;
        return string.Join(", ", cabidos) + (quedaron > 0 ? $" y {quedaron} más" : string.Empty);
    }

    /// <summary>
    /// La hora HH:MM de un instante ISO-8601 del reloj del programa; vacia si no se entiende.
    /// </summary>
    /// <remarks>
    /// Sale de <see cref="Fichas.Contratos.Puertos.IReloj.Ahora"/> y no de
    /// <c>DateTime.Now</c>: es la unica forma de que una prueba pueda decir que hora es.
    /// Una hora que no se entiende NO tumba el acuse (requisito 9): se queda sin hora, y
    /// lo importante —que se guardo y cuanto— se sigue diciendo.
    /// </remarks>
    /// <param name="instanteIso">El instante tal como lo da el reloj; si no se entiende, vacio.</param>
    public static string HoraDe(string instanteIso)
    {
        return DateTime.TryParse(
            instanteIso,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var instante)
            ? instante.ToString("HH:mm", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    /// <summary>La «s» del plural, para no repetir el ternario en cuatro sitios.</summary>
    /// <param name="cuantos">La cifra que va delante.</param>
    private static string Plural(int cuantos) => cuantos == 1 ? string.Empty : "s";
}
