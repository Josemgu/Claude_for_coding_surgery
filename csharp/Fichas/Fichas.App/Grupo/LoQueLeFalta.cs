using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;

namespace Fichas.App.Grupo;

/// <summary>
/// Que le falta a un documento para estar «listo para asignar», contado sobre el caso y
/// sus personas y sin abrir ventana.
/// </summary>
/// <remarks>
/// <para>⛔ <b>No es una firma y no marca nada como verificado.</b> Es una LECTURA del
/// estado, la respuesta a «¿le falta algo a este documento?», y el dueno dijo el
/// 2026-09-05 que esa pregunta la contesta el programa solo: <i>«Si el sistema escanea y
/// verifica todos los campos sin mi intervencion, debe decir "listo para asignar": el
/// sistema lleno todos los campos y a mi solo me deberia dejar verificarlo»</i>. La regla
/// permanente 5 sigue entera: ni un campo pasa a <c>verificado = 1</c> por aqui.</para>
///
/// <para>Sus palabras dicen dos cosas —<b>ninguno vacio</b> y <b>ninguno dudoso</b>— y
/// «dudoso» se entiende como lo entiende <see cref="EstadosDeCampo.EsDudoso"/>: vacio, con
/// una forma que no cumple, tachado sin corregir, de poca confianza, sin fila que diga de
/// donde salio, o con la confianza en «no se sabe». Con su exencion: lo que Miguel firmo y
/// lo que marco como que <b>no esta en el papel</b> ya no falta, porque ahi no queda nada
/// que buscar.</para>
///
/// <para>⛔ <b>Se llama a esa funcion y no se copia la regla, y ese es el arreglo entero
/// del 2026-09-06.</b> Hasta ese dia aqui se miraba solo el vacio y la forma, y en
/// Correccion se miraba ademas la confianza, el tachon y la marca de «no está en el papel»:
/// eran dos veredictos sobre el mismo documento, y <c>DECISIONES.md</c> midio <b>seis</b>
/// casos donde contestaban al reves. El peor dejaba documentos <b>atrapados en la cola para
/// siempre</b>: Miguel marcaba «no está en el papel», Correccion lo soltaba y la cola se lo
/// quedaba, sin forma de sacarlo. Con una sola funcion, esa divergencia no puede volver.</para>
///
/// <para>⚠️ <b>Lo que esto cuesta, medido y no supuesto.</b> Saber si un campo es dudoso
/// exige su procedencia, y por eso entra <see cref="ProcedenciasDeUnaPasada"/>: preguntarla
/// documento a documento cuesta <b>4,4 s</b> con las 18 000 lecturas reales —veintidos veces
/// el presupuesto de Inicio— y en bloque, <b>50 ms</b>. Quien llama la lee UNA vez y la pasa;
/// no hay forma de llamar a esto sin haberla leido, y eso es a proposito.</para>
/// </remarks>
public static class LoQueLeFalta
{
    /// <summary>Como se llama en la pantalla cada campo del caso que el sistema tiene que traer.</summary>
    /// <remarks>
    /// Los rotulos son los mismos que ensena la pantalla de Correccion, para que el dueno
    /// lea el mismo nombre en los dos sitios. Se escriben aqui y no se importan de alli
    /// porque alli van pegados a las constantes de columna de <c>Fichas.Lectura</c>, y una
    /// pantalla que nombra <c>Fichas.Lectura</c> rompe lo que dice <c>Fichas.App.csproj</c>.
    /// </remarks>
    public const string RotuloDelNumeroDeCaso = "N.º de caso";

    /// <summary>El numero de unidad, 6 o 7 digitos.</summary>
    public const string RotuloDeLaUnidadNumero = "N.º de unidad";

    /// <summary>El nombre de la unidad tal como lo trae la etiqueta del papel.</summary>
    public const string RotuloDeLaUnidadNombre = "Unidad";

    /// <summary>La fecha del viaje al templo.</summary>
    public const string RotuloDeLaFechaDeViaje = "Fecha de viaje";

    /// <summary>El nombre del templo.</summary>
    public const string RotuloDelTemplo = "Templo";

    /// <summary>La cedula de miembro de una persona.</summary>
    public const string RotuloDeLaCedula = "Cédula";

    /// <summary>El nombre de una persona.</summary>
    public const string RotuloDelNombre = "Nombre";

    /// <summary>La columna del numero de caso, tal como se llama en la base.</summary>
    /// <remarks>
    /// ⚠️ <b>Las siete columnas se escriben aqui como texto y no se importan.</b> Viven en
    /// <c>Fichas.Lectura.Extraccion</c>, y una pantalla que nombre <c>Fichas.Lectura</c>
    /// rompe lo que dice <c>Fichas.App.csproj</c>. Que no se separen de las de verdad NO
    /// queda a la buena fe: lo compara <c>PruebasDeUnSoloVeredicto</c> contra
    /// <c>ModeloDeCorreccion.CamposDelCasoQueSeDibujan</c>, que es la lista que la pantalla
    /// de Correccion dibuja de verdad. Si un dia se separan, la prueba se pone roja el mismo
    /// dia, que es lo que no paso con <c>templo_nombre</c> en septiembre.
    /// </remarks>
    public const string ColumnaDelNumeroDeCaso = "numero_caso";

    /// <summary>La columna del numero de unidad.</summary>
    public const string ColumnaDeLaUnidadNumero = "unidad_numero";

    /// <summary>La columna del nombre de unidad.</summary>
    public const string ColumnaDeLaUnidadNombre = "unidad_nombre";

    /// <summary>La columna de la fecha de viaje.</summary>
    public const string ColumnaDeLaFechaDeViaje = "fecha_viaje";

    /// <summary>La columna del nombre del templo.</summary>
    public const string ColumnaDelTemplo = "templo_nombre";

    /// <summary>La columna de la cedula de miembro.</summary>
    public const string ColumnaDeLaCedula = "mrn";

    /// <summary>La columna del nombre de una persona.</summary>
    public const string ColumnaDelNombre = "nombre";

    /// <summary>Las cinco columnas del caso que se miran, en el orden en que se leen.</summary>
    public static IReadOnlyList<string> ColumnasDelCaso { get; } =
    [
        ColumnaDelNumeroDeCaso, ColumnaDeLaUnidadNumero, ColumnaDeLaUnidadNombre,
        ColumnaDeLaFechaDeViaje, ColumnaDelTemplo,
    ];

    /// <summary>Las dos columnas de texto de cada persona, en el mismo orden.</summary>
    public static IReadOnlyList<string> ColumnasDeLaPersona { get; } =
        [ColumnaDeLaCedula, ColumnaDelNombre];

    /// <summary>
    /// Que le falta a este documento, con el rotulo de cada cosa; vacio significa listo.
    /// </summary>
    /// <param name="caso">El documento.</param>
    /// <param name="personas">Las personas de ese documento, ya leidas.</param>
    /// <param name="procedencias">
    /// De donde salio cada campo, leida de una vez por quien llama. Es obligatoria a
    /// proposito: sin ella este metodo tendria que adivinar, y adivinar es justo lo que hacia
    /// antes del 2026-09-06. Si de verdad no hay procedencia que leer, se pasa
    /// <see cref="ProcedenciasDeUnaPasada.Ninguna"/>, que deja el veredicto del lado prudente.
    /// </param>
    public static IReadOnlyList<string> DeUnDocumento(
        Caso caso, IReadOnlyList<Persona> personas, ProcedenciasDeUnaPasada procedencias)
    {
        ArgumentNullException.ThrowIfNull(caso);
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(procedencias);

        var falta = new List<string>();
        var delCaso = new Registro(procedencias, TablaDeProcedencia.Casos, caso.Id);

        Mirar(falta, delCaso, RotuloDelNumeroDeCaso, ColumnaDelNumeroDeCaso,
              caso.NumeroCaso, ReglasDeCampo.MotivoDelNumeroDeCaso);
        Mirar(falta, delCaso, RotuloDeLaUnidadNumero, ColumnaDeLaUnidadNumero,
              caso.UnidadNumero, ReglasDeCampo.MotivoDeLaUnidadNumero);
        Mirar(falta, delCaso, RotuloDeLaUnidadNombre, ColumnaDeLaUnidadNombre,
              caso.UnidadNombre, ReglasDeCampo.MotivoDelNombreDeUnidad);
        Mirar(falta, delCaso, RotuloDeLaFechaDeViaje, ColumnaDeLaFechaDeViaje,
              caso.FechaViaje, ReglasDeCampo.MotivoDeLaFechaDeViaje);
        Mirar(falta, delCaso, RotuloDelTemplo, ColumnaDelTemplo, caso.TemploNombre, SiempreVale);

        // Un documento sin ninguna persona leida no esta listo, y no por falta de un campo:
        // es que no hay a quien recomendar. Decirlo con su propia frase evita que se lea
        // como «le falta la cedula» de una persona que no existe.
        if (personas.Count == 0)
        {
            falta.Add("sin ninguna persona leída");
            return falta;
        }

        foreach (var persona in personas.OrderBy(p => p.FilaFormulario ?? int.MaxValue).ThenBy(p => p.Id))
        {
            var suyo = new Registro(procedencias, TablaDeProcedencia.Personas, persona.Id);
            var quien = DeQuien(persona);
            Mirar(falta, suyo, $"{RotuloDeLaCedula} de {quien}", ColumnaDeLaCedula,
                  persona.Mrn, ReglasDeCampo.MotivoDelMrn);
            Mirar(falta, suyo, $"{RotuloDelNombre} de la fila {persona.FilaFormulario ?? 0}",
                  ColumnaDelNombre, persona.Nombre, SiempreVale);
        }

        return falta;
    }

    /// <summary>Si a este documento no le falta nada; es la lectura «listo para asignar».</summary>
    /// <param name="caso">El documento.</param>
    /// <param name="personas">Las personas de ese documento, ya leidas.</param>
    /// <param name="procedencias">De donde salio cada campo, leida de una vez por quien llama.</param>
    public static bool EstaListo(
        Caso caso, IReadOnlyList<Persona> personas, ProcedenciasDeUnaPasada procedencias)
        => DeUnDocumento(caso, personas, procedencias).Count == 0;

    /// <summary>La fila a la que pertenecen los campos que se estan mirando.</summary>
    /// <remarks>
    /// Existe para no arrastrar tres argumentos iguales por cada campo: la procedencia, la
    /// tabla y el id no cambian dentro de un mismo registro.
    /// </remarks>
    private readonly record struct Registro(
        ProcedenciasDeUnaPasada Procedencias, TablaDeProcedencia Tabla, long Id);

    /// <summary>
    /// Anade el rotulo si a ese campo hay que mirarlo, preguntandolo donde lo pregunta Correccion.
    /// </summary>
    private static void Mirar(
        List<string> falta,
        Registro registro,
        string rotulo,
        string columna,
        string? valor,
        Func<string?, string?> motivo)
    {
        if (registro.Procedencias.EsDudoso(
                registro.Tabla, registro.Id, columna, valor, motivo(valor) is null))
            falta.Add(rotulo);
    }

    /// <summary>Los campos que no tienen forma obligatoria: cualquier texto no vacio vale.</summary>
    private static string? SiempreVale(string? valor) => null;

    /// <summary>Como se nombra a una persona en la lista de lo que falta, sin inventar nada.</summary>
    private static string DeQuien(Persona persona)
        => string.IsNullOrWhiteSpace(persona.Nombre)
            ? $"la fila {persona.FilaFormulario ?? 0}"
            : persona.Nombre.Trim();
}
