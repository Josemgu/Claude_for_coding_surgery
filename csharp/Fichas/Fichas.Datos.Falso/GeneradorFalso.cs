using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Datos.Falso;

/// <summary>
/// Inventa una base entera con la forma de la de verdad: N casos, sus personas, los
/// companeros y las asignaciones. Con la misma semilla sale exactamente igual.
/// </summary>
/// <remarks>
/// ⚠️ Esto NO es un dato de nadie: los nombres se componen de dos listas cortas y los MRN
/// se numeran. Existe para que las pantallas se puedan medir con 3 000 documentos sin
/// esperar a Fichas.Datos, que es la condicion del requisito 5 del dueno.
/// </remarks>
public static class GeneradorFalso
{
    /// <summary>Dieciséis nombres corrientes; se combinan con un apellido de la lista de abajo y no son de nadie.</summary>
    private static readonly string[] NombresDePila =
    [
        "Maria", "Jose", "Ana", "Luis", "Carmen", "Pedro", "Rosa", "Juan",
        "Elena", "Miguel", "Sandra", "Carlos", "Lucia", "Rafael", "Marta", "Andres",
    ];

    /// <summary>Catorce apellidos; los tres primeros son a propósito de nadie, y el resto son apellidos corrientes.</summary>
    private static readonly string[] Apellidos =
    [
        "Fulano", "Mengano", "Anonimo", "Ramirez", "Santana", "Del Rosario", "Peralta",
        "Mejia", "Nunez", "Castillo", "Fernandez", "Aquino", "Reyes", "Cabrera",
    ];

    /// <summary>Nombres de unidad con la forma de los que traen los formularios; ninguno está tomado de un PDF real.</summary>
    private static readonly string[] Unidades =
    [
        "Castries Branch", "Paramaribo Branch", "Santo Domingo Este", "Barahona",
        "San Cristobal", "La Vega", "Puerto Plata",
    ];

    /// <summary>Los tres templos a los que viajan los casos inventados.</summary>
    private static readonly string[] Templos =
    [
        "Santo Domingo Dominican Republic", "Caracas Venezuela", "Port-au-Prince Haiti",
    ];

    /// <summary>Las cuatro letras con las que empieza un número de caso; se combinan con cuatro dígitos de año y mes.</summary>
    private static readonly string[] LetrasDeCaso = ["BARC", "CASP", "CASD", "SDQE", "LVEG", "PPLT"];

    /// <summary>Los ocho compañeros del equipo inventado; el primero es Miguel, y los dos últimos entran desactivados.</summary>
    private static readonly string[] NombresDeCompaneros =
    [
        "Miguel", "Sandy", "Ramon", "Yudelka", "Franklin", "Altagracia", "Wilkin", "Noemi",
    ];

    /// <summary>
    /// Genera un almacen con la cantidad de casos que se pida, siempre igual con la misma semilla.
    /// </summary>
    /// <param name="cantidadDeCasos">Cuantos casos se inventan; 3 000 es la cifra del requisito 5.</param>
    /// <param name="semilla">La semilla del sorteo; la misma semilla da la misma base.</param>
    /// <param name="reloj">El reloj desde el que se cuentan las fechas de viaje.</param>
    /// <returns>Un almacén con 8 compañeros (6 activos), los casos pedidos con entre 1 y 4 personas cada uno, asignaciones para cerca de la mitad, unos pocos ilegibles y la procedencia de cada campo. Con 3 000 casos y la semilla 20260904: 7 531 personas, 1 490 asignaciones, 12 ilegibles (medido por su prueba).</returns>
    public static AlmacenFalso Generar(int cantidadDeCasos, int semilla, IReloj reloj)
    {
        var almacen = new AlmacenFalso(reloj, semilla);
        var sorteo = new SorteoDeterminista(semilla);

        InventarCompaneros(almacen);
        InventarCasosConSusPersonas(almacen, sorteo, Math.Max(0, cantidadDeCasos));
        InventarAsignaciones(almacen, sorteo);
        InventarIlegibles(almacen, sorteo);

        // ⚠️ Va la ULTIMA a proposito, y no es un detalle de estilo. El sorteo es una sola
        // sucesion: meter un paso nuevo en medio corre todos los que vienen detras, y con eso
        // los casos, las personas, las asignaciones y los ilegibles saldrian distintos con la
        // MISMA semilla. Puesta al final, lo que ya habia sale byte a byte igual que antes y
        // lo unico que cambia es que ahora hay procedencia.
        ProcedenciaInventada.Sembrar(almacen, sorteo);

        return almacen;
    }

    /// <summary>Da de alta los companeros; los dos ultimos entran desactivados a proposito.</summary>
    /// <remarks>No consume el sorteo: son siempre los mismos ocho, dados de alta hace 400 días y, los dos de baja, hace 30. Todos con el rol y la categoría por defecto.</remarks>
    /// <param name="almacen">Dónde se escriben; sus ids son los ocho primeros del contador.</param>
    private static void InventarCompaneros(AlmacenFalso almacen)
    {
        var creadoEn = almacen.Reloj.HoyMasDias(-400);
        for (var i = 0; i < NombresDeCompaneros.Length; i++)
        {
            var activo = i < NombresDeCompaneros.Length - 2;
            var id = almacen.SiguienteId();
            almacen.Companeros[id] = new Companero
            {
                Id = id,
                Nombre = NombresDeCompaneros[i],
                Activo = activo,
                DesactivadoEn = activo ? null : almacen.Reloj.HoyMasDias(-30),
                CreadoEn = creadoEn,
            };
        }
    }

    /// <summary>Inventa los casos y, por cada uno, entre una y cuatro personas.</summary>
    /// <param name="almacen">Dónde se escriben.</param>
    /// <param name="sorteo">El sorteo ya empezado; el orden caso-personas-caso-personas es parte de lo que la semilla reproduce.</param>
    /// <param name="cuantos">Cuántos casos; ya viene saneado a 0 o más.</param>
    private static void InventarCasosConSusPersonas(AlmacenFalso almacen, SorteoDeterminista sorteo, int cuantos)
    {
        for (var i = 0; i < cuantos; i++)
        {
            var casoId = almacen.SiguienteId();
            almacen.Casos[casoId] = InventarUnCaso(almacen, sorteo, casoId, i);
            InventarLasPersonasDe(almacen, sorteo, casoId);
        }
    }

    /// <summary>Compone un caso con su numero, su fecha de viaje y su estado.</summary>
    /// <remarks>
    /// Lo que reparte: 6 de cada 100 sin número de caso; el número repite el par letras-mes en
    /// muchos documentos, como desde la migración 12; 8 de cada 100 con fecha se archivan; 5 de
    /// cada 100 entran como captura manual; 15 de cada 100 sin templo. Los que tienen estado
    /// lo llevan como si lo hubiera marcado un paquete devuelto, con el mismo compañero en
    /// las dos mitades del desdoble de la migración 14.
    /// </remarks>
    /// <param name="almacen">De donde salen el reloj y el número de compañeros.</param>
    /// <param name="sorteo">El sorteo ya empezado.</param>
    /// <param name="casoId">El id ya repartido para este caso.</param>
    /// <param name="orden">Su posición, base 0; decide el lote del PDF (50 por archivo), la hoja (1 a 6) y los cuatro dígitos del número.</param>
    /// <returns>El caso completo; el que llama lo escribe en el almacén.</returns>
    private static Caso InventarUnCaso(AlmacenFalso almacen, SorteoDeterminista sorteo, long casoId, int orden)
    {
        var fechaViaje = InventarFechaDeViaje(almacen, sorteo);
        var archivado = fechaViaje is not null && sorteo.ConProbabilidad(8);
        var estado = InventarEstado(sorteo);
        var marcadoPor = estado is null ? (long?)null : 1 + sorteo.Hasta(Math.Max(1, almacen.Companeros.Count));

        return new Caso
        {
            Id = casoId,
            // El numero de caso NO es unico desde la migracion 12: varios documentos lo comparten.
            NumeroCaso = sorteo.ConProbabilidad(6)
                ? null
                : $"{sorteo.Elegir(LetrasDeCaso)}{2608 + (orden % 4)}",
            DuplicadoDe = null,
            EstadoMarcadoPor = marcadoPor,
            EstadoMarcadoEn = estado is null ? null : almacen.Reloj.HoyMasDias(-sorteo.Hasta(20)),
            EstadoMarcadoOrigen = estado is null ? null : "paquete devuelto por el companero",
            EstadoDelCompanero = estado,
            EstadoDelCompaneroPor = marcadoPor,
            EstadoDelCompaneroEn = estado is null ? null : almacen.Reloj.HoyMasDias(-sorteo.Hasta(20)),
            UnidadNumero = sorteo.ConProbabilidad(45) ? "7000011" : $"70000{sorteo.Hasta(10)}{sorteo.Hasta(10)}",
            FechaViaje = fechaViaje,
            CapturaManual = sorteo.ConProbabilidad(5),
            Archivado = archivado,
            FechaArchivado = archivado ? almacen.Reloj.HoyMasDias(-sorteo.Hasta(10)) : null,
            // ⚠️ La ruta apunta a un disco que NO existe, y es a proposito. Hasta el
            // 2026-09-06 decia C:\Users\josem\Documents\Fichas\pdf\, que es la carpeta REAL
            // del dueno: una base INVENTADA llevaba escrita dentro la ruta de sus datos de
            // verdad, y bastaba con que algun dia alguien escribiera ahi en vez de leer. Lo
            // vio el programador del archivado, que se encontro «No se pudo abrir la hoja 5
            // de C:\Users\josem\Documents\Fichas\pdf\lote-000.pdf» corriendo con --falso.
            RutaPdf = $@"Z:\INVENTADO\NO-EXISTE\lote-{orden / 50:D3}.pdf",
            CreadoEn = almacen.Reloj.HoyMasDias(-sorteo.Hasta(120)),
            EstadoRecomendacion = estado,
            PaginaPdf = 1 + (orden % 6),
            UnidadNombre = sorteo.Elegir(Unidades),
            TemploNombre = sorteo.ConProbabilidad(85) ? sorteo.Elegir(Templos) : null,
        };
    }

    /// <summary>Reparte las fechas de viaje: unas pocas ya pasaron, unas cuantas caen en los proximos 7 dias.</summary>
    /// <remarks>De cada 100: 8 sin fecha, 12 ya viajaron (hace 1 a 59 días), 12 en la franja roja (hoy a hoy más 7), y el resto entre 8 y 179 días.</remarks>
    /// <param name="almacen">De donde sale el reloj.</param>
    /// <param name="sorteo">El sorteo ya empezado.</param>
    /// <returns>Una fecha ISO-8601, o nulo para el caso al que todavía no se le sabe la fecha.</returns>
    private static string? InventarFechaDeViaje(AlmacenFalso almacen, SorteoDeterminista sorteo)
    {
        var dado = sorteo.Hasta(100);
        if (dado < 8) return null;                                   // Sin fecha: sale en su lista aparte.
        if (dado < 20) return almacen.Reloj.HoyMasDias(-sorteo.Entre(1, 60));   // Ya viajo.
        if (dado < 32) return almacen.Reloj.HoyMasDias(sorteo.Hasta(8));        // La franja roja de 7 dias.
        return almacen.Reloj.HoyMasDias(sorteo.Entre(8, 180));
    }

    /// <summary>Elige el estado de la recomendacion; la mayoria sigue sin marcar.</summary>
    /// <remarks>De cada 100: 55 sin marcar, 25 completas, 20 no completas. Se devuelve el texto de la columna y no el enumerado porque es lo que la base guarda.</remarks>
    /// <param name="sorteo">El sorteo ya empezado.</param>
    /// <returns><c>completa</c>, <c>no_completa</c> o nulo.</returns>
    private static string? InventarEstado(SorteoDeterminista sorteo)
    {
        var dado = sorteo.Hasta(100);
        if (dado < 55) return null;
        return dado < 80 ? "completa" : "no_completa";
    }

    /// <summary>Inventa entre una y cuatro personas del caso, con sus casillas y sus pasos.</summary>
    /// <remarks>
    /// Todas comparten apellido, como una familia en el mismo formulario. 12 de cada 100 van sin
    /// MRN, que son las que rompen la reconciliación; el MRN se compone como texto para que
    /// conserve el cero de delante. <c>PudoViajar</c> y <c>MotivoNoViajo</c> se dejan siempre a
    /// nulo: nadie los ha contestado todavía.
    /// </remarks>
    /// <param name="almacen">Dónde se escriben.</param>
    /// <param name="sorteo">El sorteo ya empezado.</param>
    /// <param name="casoId">El caso al que pertenecen.</param>
    private static void InventarLasPersonasDe(AlmacenFalso almacen, SorteoDeterminista sorteo, long casoId)
    {
        var cuantas = sorteo.Entre(1, 5);
        var apellido = sorteo.Elegir(Apellidos);
        for (var fila = 1; fila <= cuantas; fila++)
        {
            var id = almacen.SiguienteId();
            var traeMrn = sorteo.ConProbabilidad(88);
            almacen.Personas[id] = new Persona
            {
                Id = id,
                CasoId = casoId,
                // El MRN es TEXT y puede empezar por cero: se compone como texto, nunca como numero.
                Mrn = traeMrn ? $"{sorteo.Hasta(1000):D3}-{sorteo.Hasta(10000):D4}-{sorteo.Hasta(10000):D4}" : null,
                Nombre = $"{sorteo.Elegir(NombresDePila)} {apellido}",
                FilaFormulario = fila,
                OrdRecibirPropias = InventarCasilla(sorteo),
                OrdObservarSellamiento = InventarCasilla(sorteo),
                OrdTraductor = InventarCasilla(sorteo),
                OrdInvestidura = InventarCasilla(sorteo),
                OrdSellamientoEsposos = InventarCasilla(sorteo),
                OrdSellamientoHijoPadres = InventarCasilla(sorteo),
                PaginaPdf = fila <= 6 ? fila : 6,
                EstadoPropuesto = sorteo.ConProbabilidad(30) ? "listo" : null,
                NotaCompanero = sorteo.ConProbabilidad(12) ? "Pendiente de entrevista con el obispo." : null,
                PropuestoPor = sorteo.ConProbabilidad(30) ? 1 + sorteo.Hasta(6) : null,
                PropuestoEn = sorteo.ConProbabilidad(30) ? almacen.Reloj.HoyMasDias(-sorteo.Hasta(15)) : null,
                MotivoNoViajo = null,
                PudoViajar = null,
                PasoPreparacion = InventarCasilla(sorteo),
                PasoInformacion = InventarCasilla(sorteo),
                PasoCitaDelTemplo = InventarCasilla(sorteo),
                PasoAccionesRequeridas = InventarCasilla(sorteo),
                PasoEntrevistas = InventarCasilla(sorteo),
                PasoListoParaElTemplo = InventarCasilla(sorteo),
                LlamoAlLider = sorteo.ConProbabilidad(20) ? true : null,
            };
        }
    }

    /// <summary>Los tres valores de una casilla: marcada, no marcada, o nadie la miro.</summary>
    /// <remarks>De cada 100: 20 sin mirar (nulo), 50 marcadas, 30 no marcadas.</remarks>
    /// <param name="sorteo">El sorteo ya empezado.</param>
    private static bool? InventarCasilla(SorteoDeterminista sorteo)
    {
        var dado = sorteo.Hasta(100);
        if (dado < 20) return null;
        return dado < 70;
    }

    /// <summary>Asigna a un companero activo aproximadamente la mitad de los casos.</summary>
    /// <remarks>Una asignación viva por caso elegido, fechada entre hoy y hace 29 días; los archivados también entran, que es el caso de los 1 000 que Inicio limpia al llegar.</remarks>
    /// <param name="almacen">Dónde se escriben; si no hay ningún activo no se asigna nada.</param>
    /// <param name="sorteo">El sorteo ya empezado.</param>
    private static void InventarAsignaciones(AlmacenFalso almacen, SorteoDeterminista sorteo)
    {
        var activos = almacen.Companeros.Values.Where(c => c.Activo).Select(c => c.Id).ToList();
        if (activos.Count == 0) return;

        foreach (var caso in almacen.Casos.Values.OrderBy(c => c.Id))
        {
            if (!sorteo.ConProbabilidad(50)) continue;
            var id = almacen.SiguienteId();
            almacen.Asignaciones[id] = new Asignacion
            {
                Id = id,
                CasoId = caso.Id,
                CompaneroId = sorteo.Elegir(activos),
                AsignadoEn = almacen.Reloj.HoyMasDias(-sorteo.Hasta(30)),
                Activa = true,
                DesactivadaEn = null,
            };
        }
    }

    /// <summary>Deja unos pocos renglones de documentos que no se pudieron leer.</summary>
    /// <remarks>Uno más uno por cada 100 casos, con tope de 12; ninguno apunta a un caso.</remarks>
    /// <param name="almacen">Dónde se escriben.</param>
    /// <param name="sorteo">El sorteo ya empezado.</param>
    private static void InventarIlegibles(AlmacenFalso almacen, SorteoDeterminista sorteo)
    {
        var cuantos = Math.Min(12, 1 + (almacen.Casos.Count / 100));
        for (var i = 0; i < cuantos; i++)
        {
            var id = almacen.SiguienteId();
            almacen.Ilegibles[id] = new RenglonIlegible
            {
                Id = id,
                RutaPdf = $@"Z:\INVENTADO\NO-EXISTE\ilegible-{i:D2}.pdf",
                PaginaPdf = sorteo.ConProbabilidad(70) ? 1 + sorteo.Hasta(6) : null,
                Motivo = sorteo.ConProbabilidad(50) ? "sin_texto" : "sin_campos",
                Detalle = null,
                LineasLeidas = sorteo.Hasta(4),
                CasoId = null,
                RegistradoEn = almacen.Reloj.HoyMasDias(-sorteo.Hasta(40)),
            };
        }
    }
}
