using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Lectura;

namespace Fichas.App.Importar;

/// <summary>
/// La mitad que escribe filas: el alta del caso, sus personas y su procedencia.
/// </summary>
public sealed partial class GuardadoDeHojas
{
    /// <summary>
    /// Da de alta el caso de esta hoja, con su procedencia y sus personas.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>El duplicado se busca ANTES del alta</b>, y el orden no es un detalle: si se
    /// buscara despues, el caso recien nacido se encontraria a si mismo y todo caso seria
    /// duplicado de si mismo.
    /// </remarks>
    /// <param name="hoja">La hoja leída.</param>
    /// <param name="campos">Los mismos campos, ya repartidos entre caso y personas.</param>
    /// <param name="numeroCaso">El número de caso que leyó esta hoja, o nulo.</param>
    /// <param name="casosDeEsteDocumento">Número de caso → id del caso abierto por una hoja anterior de este PDF; se le añade esta hoja si es la primera con su número.</param>
    /// <returns>Un resultado con <c>Entro</c> falso si la base no dejó entrar el caso ni retirando campos.</returns>
    private ResultadoDeLaHoja AbrirUnCasoParaEstaHoja(
        HojaLeida hoja, CamposDeLaHoja campos, string? numeroCaso,
        Dictionary<string, long> casosDeEsteDocumento)
    {
        // La hermana se lee antes del alta porque el alta escribe en el diccionario del
        // documento, y entonces esta hoja se encontraria a si misma como su propia hermana.
        var hermana = numeroCaso is not null && casosDeEsteDocumento.TryGetValue(numeroCaso, out var deLaHermana)
            ? deLaHermana
            : (long?)null;
        var discrepancias = hermana is null ? [] : Discrepancias(_casos.Obtener(hermana.Value), campos);

        var mrn = campos.Personas.Select(persona => persona.ValorDelMrn)
            .Where(valor => valor is not null).Select(valor => valor!).ToArray();
        var duplicadoDe = _duplicados.CasoDelQueEsDuplicado(numeroCaso, mrn, hoja.RutaPdf, hoja.Pagina);

        var alta = DarDeAltaElCaso(hoja, campos, numeroCaso, duplicadoDe);
        if (alta.CasoId is null)
        {
            return HojaQueNoSePudoGuardar(hoja, alta.Avisos);
        }

        GuardarLaProcedenciaDelCaso(alta.CasoId.Value, campos);

        // ⚠️ El nulo NUNCA entra en el diccionario del documento: con la clave «no se sabe
        // el numero», la segunda hoja sin numero se uniria a la primera y dos familias
        // quedarian revueltas en un caso, sin forma de separarlas despues.
        // Y la hoja que contradijo a su hermana TAMPOCO se queda con la clave: si se la
        // quedara, la hoja siguiente se uniria a ella y la cadena acabaria fundiendo lo que
        // esto existe para separar. La primera hoja manda sobre su numero.
        if (numeroCaso is not null && hermana is null)
        {
            casosDeEsteDocumento[numeroCaso] = alta.CasoId.Value;
        }

        var (personas, avisosDeLasPersonas) = GuardarLasPersonas(alta.CasoId.Value, campos, hoja.Pagina);

        return new ResultadoDeLaHoja(
            Entro: true,
            CasoNuevo: true,
            CasoId: alta.CasoId,
            NumeroCaso: alta.NumeroCasoGuardado,
            PaginaPdf: hoja.Pagina,
            Personas: personas,
            Avisos:
            [
                .. hoja.Avisos, .. alta.Avisos, .. avisosDeLasPersonas,
                .. AvisoDeSinNumero(alta.NumeroCasoGuardado),
                .. AvisoDeDuplicado(duplicadoDe),
                .. discrepancias.Count > 0 && numeroCaso is not null
                    ? new[] { AvisoDeLaHojaAparte(discrepancias, numeroCaso) }
                    : [],
            ],
            PendienteDeIdentificar: alta.NumeroCasoGuardado is null,
            DuplicadoDe: duplicadoDe,
            Renglones:
            [
                .. alta.NumeroCasoGuardado is null && numeroCaso is null
                    ? new[] { MotivosDeIlegible.SinNumeroDeCaso } : [],
                .. alta.Retirados.Any(uno => uno.Campo == CamposDeLaHoja.CampoNumeroCaso)
                    ? new[] { MotivosDeIlegible.NumeroNoAceptado } : [],
                // UNO solo aunque se hayan retirado dos campos: es un solo hecho —«la base no
                // aceptó lo que se leyó aquí»— y dos renglones con la misma página se leen
                // como dos problemas distintos. Cuáles fueron lo dicen sus avisos.
                .. alta.Retirados.Any(uno => uno.Campo != CamposDeLaHoja.CampoNumeroCaso)
                    ? new[] { MotivosDeIlegible.CampoNoAceptado } : [],
                .. duplicadoDe is not null ? new[] { MotivosDeIlegible.EntroComoDuplicado } : [],
                .. discrepancias.Count > 0 ? new[] { MotivosDeIlegible.HojaAparte } : [],
            ]);
    }

    /// <summary>Lo que devuelve el alta: el caso que nacio, su numero y lo que hubo que retirar.</summary>
    /// <param name="CasoId">El caso que nacio, o nulo si la base no lo dejo entrar de ninguna forma.</param>
    /// <param name="NumeroCasoGuardado">El numero con el que se quedo; nulo si se retiro o no se leyo.</param>
    /// <param name="Avisos">Todo lo que hay que decir de esta alta.</param>
    /// <param name="Retirados">Los campos que hubo que quitar para que la fila entrara.</param>
    private readonly record struct AltaDelCaso(
        long? CasoId,
        string? NumeroCasoGuardado,
        IReadOnlyList<Aviso> Avisos,
        IReadOnlyList<CampoRetirado> Retirados);

    /// <summary>
    /// Mete el caso en la base; si el motor rechaza la fila, retira lo justo y la vuelve a meter.
    /// </summary>
    /// <remarks>
    /// ⚠️ El reintento es lo que impide perder una hoja entera —con sus personas dentro— por un
    /// campo mal leido. Que se retira, en que orden y por que esta en
    /// <see cref="RecortesQueSeIntentan"/>, junto con la medicion que lo obliga.
    ///
    /// <para>El valor retirado NO se pierde: viaja al renglon de ilegibles y a la fila de
    /// procedencia, que es de donde Miguel lo lee para teclearlo. Y no se «arregla» solo (regla
    /// permanente 1): un `CASP26O9` corregido a `CASP2609` por el programa seria un dato
    /// inventado.</para>
    /// </remarks>
    /// <param name="hoja">La hoja leída, de donde salen la ruta, la página y si fue captura manual.</param>
    /// <param name="campos">Los campos del caso, ya repartidos.</param>
    /// <param name="numeroCaso">El número de caso leído, o nulo.</param>
    /// <param name="duplicadoDe">El id del caso que esta hoja repite, o nulo; se guarda tal cual.</param>
    private AltaDelCaso DarDeAltaElCaso(
        HojaLeida hoja, CamposDeLaHoja campos, string? numeroCaso, long? duplicadoDe)
    {
        var caso = new Caso
        {
            NumeroCaso = numeroCaso,
            DuplicadoDe = duplicadoDe,
            UnidadNumero = campos.ValorDe(CamposDeLaHoja.CampoUnidadNumero),
            FechaViaje = campos.ValorDe(CamposDeLaHoja.CampoFechaViaje),
            UnidadNombre = campos.ValorDe(CamposDeLaHoja.CampoUnidadNombre),
            TemploNombre = campos.ValorDe(CamposDeLaHoja.CampoTemploNombre),
            CapturaManual = hoja.CapturaManual,
            RutaPdf = hoja.RutaPdf,
            PaginaPdf = hoja.Pagina >= 1 ? hoja.Pagina : null,
        };

        var alta = _casos.Guardar(caso);
        if (alta.SeEscribio) return new AltaDelCaso(alta.Id, numeroCaso, alta.Avisos, []);

        var avisos = new List<Aviso>(alta.Avisos);
        foreach (var retirados in RecortesQueSeIntentan(caso))
        {
            var reintento = _casos.Guardar(SinLosCampos(caso, retirados));
            avisos.AddRange(reintento.Avisos);
            if (!reintento.SeEscribio) continue;

            var seRetiroElNumero = retirados.Any(uno => uno.Campo == CamposDeLaHoja.CampoNumeroCaso);
            return new AltaDelCaso(
                reintento.Id,
                seRetiroElNumero ? null : numeroCaso,
                [.. avisos, .. retirados.Select(AvisoDeLoRetirado)],
                retirados);
        }

        return new AltaDelCaso(null, null, avisos, []);
    }

    /// <summary>Lo que se devuelve de una hoja cuyo caso el motor no dejo entrar.</summary>
    /// <param name="hoja">La hoja leída.</param>
    /// <param name="avisos">Lo que dijo la base en cada intento.</param>
    private static ResultadoDeLaHoja HojaQueNoSePudoGuardar(HojaLeida hoja, IReadOnlyList<Aviso> avisos) => new(
        Entro: false,
        CasoNuevo: false,
        CasoId: null,
        NumeroCaso: null,
        PaginaPdf: hoja.Pagina,
        Personas: 0,
        Avisos: [.. hoja.Avisos, .. avisos],
        PendienteDeIdentificar: false,
        DuplicadoDe: null,
        Renglones: [MotivosDeIlegible.SinTexto]);

    /// <summary>Las cinco filas de procedencia del caso, una por campo que se dibuja.</summary>
    /// <param name="casoId">El caso recién nacido.</param>
    /// <param name="campos">De dónde salen banda, confianza y valor crudo de cada campo.</param>
    private void GuardarLaProcedenciaDelCaso(long casoId, CamposDeLaHoja campos)
    {
        foreach (var campo in CamposDeLaHoja.CamposDelCasoConProcedencia)
        {
            Anotar(TablaDeProcedencia.Casos, casoId, campo, campos.Del(campo));
        }
    }

    /// <summary>Todas las personas de la hoja. Devuelve cuantas entraron y los avisos.</summary>
    /// <remarks>
    /// Las filas se corren detras de las que el caso ya tenia. Cada hoja numera sus
    /// personas desde 1, asi que seis hojas del mismo grupo traerian seis «fila 1»: el
    /// orden del papel se perderia justo donde hay que comparar contra el papel.
    /// </remarks>
    /// <param name="casoId">El caso al que pertenecen.</param>
    /// <param name="campos">Las personas de la hoja, con sus dos campos cada una.</param>
    /// <param name="paginaPdf">La página del PDF, base 1; cero o menos se guarda como nulo.</param>
    /// <returns>Cuántas entraron de verdad y un aviso por cada fila que la base rechazó.</returns>
    private (int Personas, IReadOnlyList<Aviso> Avisos) GuardarLasPersonas(
        long casoId, CamposDeLaHoja campos, int paginaPdf)
    {
        var desplazamiento = _personas.DeCaso(casoId)
            .Select(persona => persona.FilaFormulario ?? 0)
            .DefaultIfEmpty(0)
            .Max();

        var guardadas = 0;
        var avisos = new List<Aviso>();

        foreach (var deLaHoja in campos.Personas)
        {
            var alta = _personas.Guardar(new Persona
            {
                CasoId = casoId,
                Nombre = deLaHoja.ValorDelNombre,
                Mrn = deLaHoja.ValorDelMrn,
                FilaFormulario = deLaHoja.FilaFormulario is null ? null : deLaHoja.FilaFormulario + desplazamiento,
                PaginaPdf = paginaPdf >= 1 ? paginaPdf : null,
            });

            if (!alta.SeEscribio)
            {
                // Casi siempre es el UNIQUE (caso_id, mrn): con las hojas de un grupo
                // unidas en un caso, dos hojas pueden traer la misma persona. NO se
                // silencia: se nombra la fila y se dice que se descarto.
                avisos.Add(Aviso.Advierte(
                    $"La fila {deLaHoja.FilaFormulario} de esta página no entró: su cédula ya estaba en este caso.",
                    CamposDeLaHoja.CampoMrn,
                    "Compruébelo en el PDF: o es la misma persona repetida, o el lector leyó mal una de las dos. " +
                    string.Join(" ", alta.Avisos.Select(aviso => aviso.Linea))));
                continue;
            }

            guardadas++;
            foreach (var campo in CamposDeLaHoja.CamposDeLaPersonaConProcedencia)
            {
                Anotar(TablaDeProcedencia.Personas, alta.Id, campo, deLaHoja.Del(campo));
            }
        }

        return (guardadas, avisos);
    }

    /// <summary>La fila de procedencia de un campo, con su banda del escaneo.</summary>
    /// <remarks>
    /// Un campo que la hoja no trajo TAMBIEN deja su fila, con origen «vacio»: eso es lo
    /// que le dice a la pantalla de correccion que ahi hay que teclear algo, en vez de
    /// dejar un hueco que no se distingue de un campo que nadie miro.
    /// </remarks>
    /// <param name="tabla">Si el campo es del caso o de una persona.</param>
    /// <param name="registroId">El id de la fila en esa tabla.</param>
    /// <param name="campo">El nombre de la columna.</param>
    /// <param name="propuesto">Lo que leyó el lector, o nulo si el papel no lo traía.</param>
    private void Anotar(TablaDeProcedencia tabla, long registroId, string campo, CampoPropuesto? propuesto)
        => _procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = tabla,
            RegistroId = registroId,
            Campo = campo,
            Origen = propuesto?.Origen ?? OrigenDeCampo.Vacio,
            Confianza = propuesto?.Confianza,
            ValorOcr = propuesto?.ValorOcr ?? propuesto?.Valor,
            BandaX0 = propuesto?.Banda?.X0,
            BandaY0 = propuesto?.Banda?.Y0,
            BandaX1 = propuesto?.Banda?.X1,
            BandaY1 = propuesto?.Banda?.Y1,
            AnuladoPorTachon = propuesto?.AnuladoPorTachon ?? false,
            AusenteEnElPapel = propuesto is null,
            // ⛔ Verificado se queda en falso SIEMPRE (regla permanente 5). No hay ninguna
            // rama de este metodo que lo ponga: el unico camino es IProcedencia.Firmar.
        });

    /// <summary>El aviso de la hoja que entro sin numero de caso, o ninguno.</summary>
    /// <param name="numeroCasoGuardado">El número con el que se quedó el caso; nulo produce el aviso.</param>
    private static Aviso[] AvisoDeSinNumero(string? numeroCasoGuardado)
        => numeroCasoGuardado is not null ? [] :
        [
            Aviso.Advierte(
                "Una página entró SIN número de caso: hay que teclearlo a mano.",
                CamposDeLaHoja.CampoNumeroCaso,
                "Todo lo demás que se leyó está dentro y no hay que volver a teclearlo. Abra el caso, " +
                "mire el PDF en esa página y escriba el número; hasta entonces el caso no puede " +
                "cruzarse con el Excel que devuelven los compañeros."),
        ];

    /// <summary>El aviso del caso que repite a otro, o ninguno.</summary>
    /// <param name="duplicadoDe">El id del caso original; nulo no produce aviso.</param>
    private Aviso[] AvisoDeDuplicado(long? duplicadoDe)
    {
        if (duplicadoDe is null) return [];

        var original = _casos.Obtener(duplicadoDe.Value);
        var numero = original?.NumeroCaso ?? "sin número de caso";
        return
        [
            Aviso.Advierte(
                $"Un documento REPITE al caso n.º {duplicadoDe} ({numero}): entró igual, marcado.",
                CamposDeLaHoja.CampoNumeroCaso,
                "El caso que ya estaba NO se ha tocado y nada se ha fundido solo. Mírelos y decida " +
                "qué hacer con los dos. Se dice el número Y el id porque desde la migración 12 dos " +
                "casos pueden llevar el mismo número: el número solo no lleva a ninguna parte."),
        ];
    }

    /// <summary>Deja en la tabla los renglones que esta hoja tenga que dejar.</summary>
    /// <remarks>
    /// Va en el camino comun del guardado y no en la pantalla: un renglon que hay que
    /// acordarse de escribir es un renglon que algun dia no se escribe, y entonces el
    /// documento ilegible se pierde otra vez, que es lo que esta tabla existe para impedir.
    /// </remarks>
    /// <param name="hoja">La hoja leída, de donde salen ruta, página y líneas leídas.</param>
    /// <param name="resultado">Lo que pasó con ella; sus <c>Renglones</c> son los motivos que se escriben.</param>
    private void AnotarLosRenglones(HojaLeida hoja, ResultadoDeLaHoja resultado)
    {
        foreach (var motivo in resultado.Renglones)
        {
            _ilegibles.Registrar(new RenglonIlegible
            {
                RutaPdf = hoja.RutaPdf,
                PaginaPdf = hoja.Pagina >= 1 ? hoja.Pagina : null,
                Motivo = motivo,
                Detalle = DetalleDelRenglon(hoja, resultado, motivo),
                LineasLeidas = hoja.LineasLeidas,
                CasoId = resultado.CasoId,
                RegistradoEn = _reloj.Ahora(),
            });
        }
    }

    /// <summary>El texto libre que acompana al codigo del renglon.</summary>
    /// <param name="hoja">La hoja leída.</param>
    /// <param name="resultado">Lo que pasó con ella, con sus avisos.</param>
    /// <param name="motivo">Una de las constantes de <see cref="MotivosDeIlegible"/>.</param>
    private static string DetalleDelRenglon(HojaLeida hoja, ResultadoDeLaHoja resultado, string motivo)
    {
        var propio = motivo switch
        {
            MotivosDeIlegible.NumeroNoAceptado =>
                "El número leído fue «" + LoQueSeLeyoDelNumero(hoja) + "» y la base no lo acepta: " +
                "el caso entró SIN número, con todo lo demás dentro. Hay que teclearlo a mano.",
            MotivosDeIlegible.EntroComoDuplicado =>
                $"Es DUPLICADO del caso n.º {resultado.DuplicadoDe}, que NO se ha tocado. Entró igual y aparte.",
            MotivosDeIlegible.HojaAparte =>
                "Esta hoja decía ser del mismo caso que otra de este documento pero la contradecía: entró aparte.",
            MotivosDeIlegible.LoLeyoOtraHoja =>
                "Esta hoja SE UNIÓ al caso, y además leyó campos que al caso le faltan porque la hoja " +
                "que lo abrió no los leyó. No contradice a nadie y nada se ha movido: el valor no se " +
                "pone solo en el caso, se teclea mirando ESTA página.",
            MotivosDeIlegible.SinNumeroDeCaso =>
                "El lector no pudo leer el número de caso. Todo lo demás entró; el número se teclea a mano.",
            MotivosDeIlegible.CampoNoAceptado =>
                "La base no aceptó lo que se leyó en un campo de esta hoja: la hoja entró SIN él, con todo " +
                "lo demás dentro y con sus personas. Lo que decía el papel está guardado y se teclea a mano.",
            _ => "El lector no pudo sacar nada de esta hoja.",
        };

        // ⚠️ De un aviso se copia su LINEA, menos en uno. El renglon sobrevive a la tanda y
        // la franja no: si el valor que leyo la otra hoja se quedara solo en el detalle del
        // aviso, se perderia al cerrar la franja y el renglon diria «leyo algo» sin decir
        // que. Para `lo_leyo_otra_hoja` se copia tambien el detalle, que es el unico sitio
        // donde ese valor esta escrito —no esta en el caso, y `procedencia_campo` no tiene
        // columna de pagina donde ponerlo—.
        var conDetalle = motivo == MotivosDeIlegible.LoLeyoOtraHoja;
        var avisos = string.Join(" ", resultado.Avisos.Select(
            aviso => conDetalle && aviso.Detalle is not null ? $"{aviso.Linea} {aviso.Detalle}" : aviso.Linea));
        return string.IsNullOrWhiteSpace(avisos) ? propio : $"{propio} {avisos}";
    }

    /// <summary>Lo que el lector leyo en el numero de caso, aunque no se pudiera guardar.</summary>
    /// <param name="hoja">La hoja leída.</param>
    /// <returns>El valor limpio, si no el crudo del OCR, y si no «(nada)».</returns>
    private static string LoQueSeLeyoDelNumero(HojaLeida hoja)
    {
        var campo = hoja.Campos.FirstOrDefault(
            uno => uno.Tabla == TablaDeProcedencia.Casos && uno.Campo == CamposDeLaHoja.CampoNumeroCaso);
        return campo?.Valor ?? campo?.ValorOcr ?? "(nada)";
    }
}
