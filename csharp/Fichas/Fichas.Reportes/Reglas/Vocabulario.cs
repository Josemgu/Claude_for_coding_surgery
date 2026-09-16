namespace Fichas.Reportes.Reglas;

/// <summary>
/// Las palabras con las que este informe dice las cosas. Aqui no se cuenta ni se consulta.
/// </summary>
/// <remarks>
/// Vive aparte para que los DOS modulos que arman secciones puedan compartirlas sin
/// importarse entre ellos, que seria un circulo. Si cada uno escribiera la suya, un informe
/// podria decir «sin asignar» en una tabla y «sin agente» en la otra, y quien lo lee no
/// sabria si son dos cosas.
/// </remarks>
public static class Vocabulario
{
    /// <summary>El titulo del documento.</summary>
    public const string Titulo = "Fichas — Reporte de recomendaciones al templo";

    /// <summary>Con lo que abre el informe: la pregunta que se le hace al programa.</summary>
    /// <remarks>Escrita como pregunta y no como categoria: «Resumen» no dice nada, y esto si.</remarks>
    public const string TitularDeLaPortada = "Cuántas personas viajaron sin estar listas";

    /// <summary>El rotulo del viejo para «los seis pasos contestados que sí».</summary>
    /// <remarks>
    /// ⚠️ <b>Estos tres rotulos son del programa VIEJO y volvieron por decision del dueno</b>
    /// (<c>DECISIONES.md</c>, 2026-09-04): <i>«si el informe de los jefes como el viejo está
    /// bien»</i>. Estuvieron diciendo «con la preparación completa» y «sin la preparación
    /// completa» desde el criterio C8-2, para que «verificada» no significara dos cosas en el
    /// mismo programa —aqui son los seis pasos del sistema del lider; en la pantalla de
    /// correccion es la FIRMA de Miguel sobre un campo leido—.
    ///
    /// <b>El riesgo de aquel criterio sigue siendo real y por eso no se tira entero:</b> lo que
    /// se conserva es la aclaracion, dicha DENTRO del informe en la nota de
    /// <c>SeccionesDeDireccion.QuienViajoSinVerificar</c>. Se prefiere explicarlo en el
    /// documento que el dueno lee a cambiarle las palabras que el pidio.
    ///
    /// Viven aqui, y no repetidos en cada tabla, porque son SEIS rotulos en cuatro tablas: con
    /// la cadena escrita seis veces, cambiar la palabra otra vez deja alguna sin cambiar y el
    /// informe se lee como si hablara de dos cosas.
    /// </remarks>
    public const string ConLaPreparacionCompleta = "Verificadas";

    /// <summary>El rotulo del viejo para «le falto algun paso».</summary>
    public const string SinLaPreparacionCompleta = "Sin verificar";

    /// <summary>Como se llama en el viejo la seccion que abre el informe.</summary>
    public const string QuienesViajaronSinVerificar = "Quiénes viajaron sin verificar";

    /// <summary>Lo que se escribe donde la base no tiene el dato.</summary>
    /// <remarks>Dos maneras de decir «no lo sé» en el mismo programa se leen como dos cosas.</remarks>
    public const string SinDato = "no consta";

    /// <summary>Lo que se escribe donde el agente no dejo comentario.</summary>
    /// <remarks>
    /// No se deja la celda en blanco: en blanco se lee como «el agente no dijo nada», y hoy lo
    /// que pasa casi siempre es otra cosa —que la hoja del companero todavia no trae la columna
    /// donde escribirlo, que es la FASE C14—. El motivo entero va en el aviso del reporte;
    /// esta palabra solo evita el hueco.
    /// </remarks>
    public const string SinComentario = "sin comentario";

    /// <summary>Cuando ni un «sí» ni un «no» serian ciertos.</summary>
    public const string NoSePuedeSaber = "no se puede saber";

    /// <summary>Como se llama un caso que no lleva nadie.</summary>
    public const string SinAgente = "sin asignar";

    /// <summary>Como se llama una unidad de la que no consta el nombre.</summary>
    public const string SinUnidad = "sin unidad";

    /// <summary>La palabra con la que se marca un caso archivado.</summary>
    /// <remarks>
    /// Del dueno, literal (DECISIONES.md, 2026-09-03): «Lo que archivo debe verse en el
    /// calendario, debe decir archivado».
    /// </remarks>
    public const string Archivado = "archivado";

    // ⛔ Aqui vivia `ElPaisNoSeGuarda`, que valia `SinDato` y rellenaba la columna «País» de
    // «Los viajes» —pedida el 2026-09-03, nunca leida del papel porque no esta impreso, y
    // siempre «no consta»—. El dueno la quito el 2026-09-16: «"no consta" no es una respuesta;
    // a dónde viajarán es el templo». La columna se fue del PDF y del Excel, y con ella la
    // constante y la nota que la explicaba. Si algun dia el pais entra al esquema (ADR-0002),
    // esta es la historia.

    /// <summary>El rotulo de la columna con el numero de la unidad, en las seis tablas.</summary>
    /// <remarks>
    /// Es el MISMO texto que usa el paquete de los companeros
    /// (<c>Fichas.Paquetes.Columnas.ColumnaDelNumeroDeUnidad</c>, «Número de unidad»), y se
    /// repite a proposito en vez de importarse: los reportes no conocen a los paquetes, y dos
    /// rotulos distintos para el mismo dato harian que Miguel filtrara por uno y no por el otro.
    /// </remarks>
    public const string RotuloDelNumeroDeUnidad = "Número de unidad";

    // ⛔ Aqui vivia `UnidadConSuNumero`, que pegaba las dos cosas en una sola celda —«Castries
    // Branch · 0700016»— con este cuerpo:
    //
    //     $"{(string.IsNullOrWhiteSpace(nombre) ? "unidad sin nombre" : nombre)} · "
    //     + $"{(string.IsNullOrWhiteSpace(numero) ? "sin número" : numero)}"
    //
    // El dueno pidio el 2026-09-08 los dos datos en dos columnas —«Sí, pártelo en dos»—,
    // sabiendo que eso cambia tambien el PDF, porque pegados NO SE PODIA FILTRAR POR NUMERO DE
    // UNIDAD en el Excel: el filtro veia una sola cadena con las dos cosas dentro. Ya no hay
    // nada que pegar y se quedo sin ningun sitio desde donde llamarla. Lo que hacia queda
    // escrito aqui por si algun dia se quiere volver a juntar las dos en una sola celda.
    //
    // Es el mismo movimiento que hizo el paquete de los companeros el 2026-09-07, y el
    // comentario gemelo esta en `Fichas.Paquetes/Paquetes.cs`.

    /// <summary>El nombre de la unidad, o la palabra que dice que no consta.</summary>
    /// <remarks>
    /// Lo que la base guarde es lo que sale: si el escaneo dejo el numero dentro del nombre,
    /// sale dentro del nombre, porque corregirlo aqui seria cambiar un dato leido (regla
    /// permanente 1).
    /// </remarks>
    /// <param name="nombre">Lo que guarda la base; nulo o en blanco da «unidad sin nombre».</param>
    public static string NombreDeUnidad(string? nombre)
        => string.IsNullOrWhiteSpace(nombre) ? "unidad sin nombre" : nombre;

    /// <summary>El numero de la unidad, o la palabra que dice que no consta.</summary>
    /// <remarks>
    /// ⚠️ Sale TAL CUAL, con su cero de delante si lo trae. Su columna se declara
    /// <c>ClaseDeColumna.Texto</c> en las seis tablas justamente por eso: como <c>Crudo</c>, un
    /// numero de siete digitos sin cero delante entraria en el <c>.xlsx</c> como NUMERO —lo
    /// decide <c>LibroDelInforme.EsUnRecuentoQueSeDejaSumar</c>, cuyo tope son 9 digitos— y
    /// dejaria de poder compararse con el del papel.
    /// </remarks>
    /// <param name="numero">Lo que guarda la base; nulo o en blanco da «sin número».</param>
    public static string NumeroDeUnidad(string? numero)
        => string.IsNullOrWhiteSpace(numero) ? "sin número" : numero;

    /// <summary>El nombre de la persona, o la palabra que dice que no se leyo.</summary>
    /// <remarks>
    /// No se deja en blanco: un hueco en un informe a la direccion se lee como un dato que se
    /// perdio por el camino, y lo que pasa es que el reconocimiento no leyo ese nombre y nadie
    /// lo ha corregido todavia.
    /// </remarks>
    /// <param name="nombre">Lo que leyó el reconocimiento; nulo o en blanco da «nombre sin leer».</param>
    public static string PersonaOSinNombre(string? nombre)
        => string.IsNullOrWhiteSpace(nombre) ? "nombre sin leer" : nombre;

    /// <summary>El estado tal como esta guardado, o la palabra que dice que no hay ninguno.</summary>
    /// <remarks>Solo cambia los guiones bajos por espacios: «no_completa» sale «no completa». No traduce ni corrige.</remarks>
    /// <param name="estadoRecomendacion">El texto crudo de la columna; nulo o vacío da <see cref="SinDato"/>.</param>
    public static string TextoDelEstado(string? estadoRecomendacion)
        => string.IsNullOrEmpty(estadoRecomendacion) ? SinDato : estadoRecomendacion.Replace("_", " ");
}
