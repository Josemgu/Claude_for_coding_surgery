using System.Diagnostics;
using Fichas.App.Revisar;
using Fichas.Contratos.Modelos;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Correccion;

/// <summary>
/// Los dos desplegables de la cabecera: primero QUE GRUPO y despues QUE DOCUMENTO de ese
/// grupo.
/// </summary>
/// <remarks>
/// <para><b>Por que existe este archivo.</b> Palabras del dueno el 2026-09-07, probando el
/// programa: <i>«En corrección, permite trabajar por grupo, no todos juntos. Eso es súper
/// incómodo. El día que tenga 800 solicitudes tendré problemas»</i>, y despues: <i>«Esto no me
/// funciona para nada, no le veo función. Prefiero trabajar y corregir por grupo que todos
/// juntos»</i>.</para>
///
/// <para><b>Lo que habia, medido con la ventana abierta sobre el paquete publicado y una base
/// de 75 documentos repartidos en cinco grupos:</b> un solo desplegable plano cuya linea de al
/// lado decia <b>«50 de 75 casos»</b>. Los otros 25 no se podian abrir por ninguna via, y 11 de
/// ellos eran del mismo grupo del 17 de septiembre que el estaba trabajando.</para>
///
/// <para>⛔ <b>Aqui no se inventa ningun agrupado ni ningun veredicto.</b> Los grupos los arma
/// <see cref="GruposParaCorregir"/> sobre el arbol de Revisar, y lo que le falta a cada
/// documento lo contesta <see cref="LoQueLeFaltaACadaDocumento"/> llamando a la misma funcion
/// que la cola y la pantalla del grupo. Esta clase pinta y ata sucesos.</para>
///
/// <para>Va en su propio archivo porque <c>PaginaDeCorreccion.xaml.cs</c> ya pasaba del limite
/// blando de 300 lineas antes de esto.</para>
/// </remarks>
public sealed partial class PaginaDeCorreccion
{
    private IReadOnlyList<GrupoParaCorregir> _grupos = [];

    /// <summary>
    /// Los grupos SIN filtrar, que es donde esta el destino de lo que sale.
    /// </summary>
    /// <remarks>
    /// Se guardan aparte de <see cref="_grupos"/> a proposito: en los filtrados un documento
    /// resuelto ya no aparece —por definicion—, asi que preguntarles a donde paso no daria
    /// ninguna respuesta. Es la misma pasada; no cuesta una segunda lectura de la base.
    /// </remarks>
    private IReadOnlyList<GrupoParaCorregir> _todosLosGrupos = [];

    private IReadOnlyList<CasoEnElDesplegable> _documentos = [];
    private LoQueLeFaltaACadaDocumento? _loQueLeFalta;
    private bool _cambiandoDeGrupo;
    private int _documentosSinArchivar;

    /// <summary>Cuantos grupos de trabajo hay ahora mismo; lo lee la medicion.</summary>
    public int CuantosGrupos => _grupos.Count;

    /// <summary>Cuantos documentos ofrece el grupo elegido; lo lee la medicion.</summary>
    public int CuantosDocumentosDelGrupo => _documentos.Count;

    /// <summary>Cuantos documentos piden algo en toda la base; lo lee la medicion.</summary>
    public int CuantosConAlgoQueFalta => CuantosPidenAlgo();

    /// <summary>
    /// Cuantos de los que estan en la lista siguen pidiendo algo, SIN contar al invitado.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>Una sola cuenta para las dos cifras de la pantalla</b>, y ese es el arreglo:
    /// medido con la ventana abierta el 2026-09-09, tras corregir el ultimo dato la cabecera
    /// decia «2 con algo que falta, de 6» y el pie «queda 1 documento con algo que falta».
    /// Dos cifras de la misma pantalla que no encajan, y ninguna forma de saber cual creer. La
    /// diferencia era el invitado: el documento que acaba de resolverse y sigue delante.</para>
    ///
    /// <para>El criterio es el mismo <see cref="QueEntraEnCorreccion.SaleDeCorreccion"/> que
    /// decide quien entra, asi que la cifra no puede separarse de la lista que cuenta.</para>
    /// </remarks>
    private int CuantosPidenAlgo()
        => QueEntraEnCorreccion.CuantosPidenAlgo(
               _grupos, casoId => _loQueLeFalta?.LeFaltaAlgo(casoId) ?? true);

    /// <summary>
    /// Lee la base, arma los grupos y deja elegido el que contiene ese documento.
    /// </summary>
    /// <remarks>
    /// <para>Se lee de UNA pasada: las tarjetas por <see cref="TableroDeRevisar"/> —que es la
    /// misma lectura que hace Revisar— y lo que le falta a cada documento por
    /// <see cref="LoQueLeFaltaACadaDocumento"/>, en bloque. Lo que cuesta se anota en el
    /// cuaderno, para que se vea y no se suponga.</para>
    ///
    /// <para>⛔ <b>Los archivados no entran</b>, que es la regla del dueno del 2026-09-06:
    /// «debe pasar a archivado y no aparecer más en ningún lado». <c>TableroDeRevisar.Cargar</c>
    /// ya los deja fuera por defecto.</para>
    /// </remarks>
    /// <param name="casoQueSigueAbierto">
    /// El documento que hay que dejar elegido; 0 abre el primero del primer grupo.
    /// </param>
    private void LlenarLosGrupos(long casoQueSigueAbierto = 0)
    {
        if (Servicios is null) return;

        var cronometro = Stopwatch.StartNew();
        var tablero = new TableroDeRevisar(
            Servicios.Casos, Servicios.Asignaciones, Servicios.Companeros, Servicios.Reloj);
        tablero.Cargar();

        _todosLosGrupos = GruposParaCorregir.Armar(tablero.Cargadas);
        _loQueLeFalta = LoQueLeFaltaACadaDocumento.DeTodaLaBase(
            Servicios.Casos, Servicios.Personas, Servicios.Procedencia);

        // ⛔ Aqui es donde Correccion deja de ser un almacen. Hasta el 2026-09-09 se ofrecian
        // TODOS los documentos no archivados; desde hoy solo los que piden algo, que es lo que
        // el dueno pidio el 2026-09-07: «si voy a Correccion no debe estar ahi, porque ya esta
        // todo listo». El veredicto no se decide aqui: es LoQueLeFalta, el mismo de la cola y
        // el de la pantalla del grupo.
        _grupos = QueEntraEnCorreccion.Filtrar(
            _todosLosGrupos, _loQueLeFalta.LeFaltaAlgo, casoQueSigueAbierto);
        _documentosSinArchivar = tablero.Total;
        cronometro.Stop();

        Servicios.Registro.Anotar(string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "CORRECCION  {0} grupos con {1} documentos que piden algo, de {2} sin archivar, leidos en {3:F0} ms",
            _grupos.Count, CuantosPidenAlgo(), tablero.Total,
            cronometro.Elapsed.TotalMilliseconds));

        _cambiandoDeGrupo = true;
        _queGrupo.ItemsSource = _grupos.Select(grupo => grupo.Etiqueta).ToList();
        _cambiandoDeGrupo = false;

        if (_grupos.Count == 0)
        {
            _documentos = [];
            _queCaso.ItemsSource = null;
            // ⚠️ Dos frases y no una: «no hay ningun documento» y «no queda ninguno con algo
            // que falta» son cosas distintas, y desde que Correccion filtra, la segunda es la
            // normal. Decir la primera con 75 documentos resueltos en la base se leeria como
            // que el programa perdio la base entera.
            _deQueVa.Text = _documentosSinArchivar == 0
                ? TextoDeLosGrupos.NoHayNingunDocumento
                : TextoDeLaSalidaDeCorreccion.NoQuedaNadaQueCorregir;
            MostrarSiYaEstaResuelto();
            return;
        }

        // Se elige el grupo DEL documento que se estaba mirando y no el primero: llegar aqui
        // desde la pantalla del grupo o desde la cola y encontrarse otro grupo elegido seria
        // perder de vista justo el documento que se acaba de abrir.
        var donde = GruposParaCorregir.DondeEsta(_grupos, casoQueSigueAbierto);
        _cambiandoDeGrupo = true;
        _queGrupo.SelectedIndex = donde >= 0 ? donde : 0;
        _cambiandoDeGrupo = false;

        LlenarLosDocumentos(casoQueSigueAbierto);
    }

    /// <summary>
    /// Pone en el segundo desplegable TODOS los documentos del grupo elegido. Sin tope.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>Sin tope, y esa es la mitad del arreglo.</b> Hasta el 2026-09-07 aqui habia
    /// una constante <c>CuantosCasosSeOfrecen = 50</c> y la lista se cortaba ahi. Un grupo es
    /// (fecha · unidad), asi que son los documentos de un dia y una unidad: en la base medida,
    /// el grupo mas cargado trae 61.</para>
    ///
    /// <para>⚠️ Se pregunta por las personas de cada documento del GRUPO, una consulta corta
    /// por documento, que es lo que compra que dos formularios del mismo mes y la misma unidad
    /// se distingan sin abrirlos. Antes eran 50 consultas siempre; ahora son tantas como
    /// documentos tenga el grupo que se esta trabajando.</para>
    /// </remarks>
    /// <param name="casoQueSigueAbierto">El documento que hay que dejar elegido; 0 abre el primero.</param>
    private void LlenarLosDocumentos(long casoQueSigueAbierto = 0)
    {
        if (Servicios is null || _queGrupo.SelectedIndex < 0 || _queGrupo.SelectedIndex >= _grupos.Count) return;

        var cronometro = Stopwatch.StartNew();
        var grupo = _grupos[_queGrupo.SelectedIndex];
        var nombrePorId = new Dictionary<long, string?>();

        var entradas = new List<CasoEnElDesplegable>(grupo.CuantosDocumentos);
        foreach (var tarjeta in grupo.Documentos)
        {
            if (Servicios.Casos.Obtener(tarjeta.CasoId) is not Caso caso) continue;

            var gente = Servicios.Personas.DeCaso(caso.Id);
            var original = caso.DuplicadoDe is long queCaso
                ? Servicios.Casos.Obtener(queCaso)
                : null;

            entradas.Add(new CasoEnElDesplegable(
                caso.Id,
                TextoDelDesplegable.Componer(
                    caso, gente.FirstOrDefault()?.Nombre, gente.Count, original,
                    LoQueContestoElCompanero.CuantasContestadas(gente),
                    QuienMarcoEsteCaso(caso, nombrePorId),
                    _loQueLeFalta?.De(caso.Id) ?? string.Empty)));
        }

        cronometro.Stop();

        _documentos = entradas;
        _cambiandoDeCaso = true;
        _queCaso.ItemsSource = entradas;
        _queCaso.DisplayMemberPath = nameof(CasoEnElDesplegable.Como);
        _cambiandoDeCaso = false;

        Servicios.Registro.Anotar(string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "CORRECCION  el grupo «{0}» arma {1} documentos en {2:F0} ms",
            grupo.Etiqueta, entradas.Count, cronometro.Elapsed.TotalMilliseconds));

        _deQueVa.Text = TextoDeLaSalidaDeCorreccion.Denominador(
            entradas.Count, _grupos.Count, CuantosPidenAlgo(), _documentosSinArchivar);

        if (entradas.Count == 0) return;

        // ⚠️ La eleccion se hace con el suceso CALLADO y el documento se abre a mano, y es a
        // proposito: cambiar el ItemsSource de un ComboBox dispara SelectionChanged por su
        // cuenta —primero a −1 y luego al puesto que se le ponga—, asi que dejarlo hablar
        // abriria el mismo documento dos veces o ninguna segun el puesto que tuviera antes.
        // Asi se abre exactamente uno, siempre.
        var donde = entradas.FindIndex(entrada => entrada.Id == casoQueSigueAbierto);
        if (donde < 0) donde = 0;

        _cambiandoDeCaso = true;
        _queCaso.SelectedIndex = donde;
        _cambiandoDeCaso = false;

        // Elegir un grupo tiene que ABRIR algo: un desplegable lleno con la pantalla del
        // documento anterior detras se lee como que elegir el grupo no hizo nada.
        if (entradas[donde].Id != _casoAbierto) AbrirElCaso(entradas[donde].Id);
    }

    /// <summary>Se eligio otro grupo: se rehace la lista de documentos y se abre el primero.</summary>
    private void AlElegirUnGrupo(object quien, SelectionChangedEventArgs cuando)
    {
        if (_cambiandoDeGrupo) return;
        LlenarLosDocumentos();
    }

    /// <summary>
    /// Deja los dos desplegables apuntando al documento que se acaba de abrir.
    /// </summary>
    /// <remarks>
    /// <para>Hace falta porque a esta pantalla se llega DESDE FUERA —la pantalla del grupo, la
    /// ventana de lo que no esta completo, la cola de Completar— con un documento concreto, y
    /// esos caminos no pasan por los desplegables. Sin esto, la cabecera diria «Grupo del 17 de
    /// septiembre» con un documento de octubre delante: dos cosas de la misma pantalla que no
    /// encajan, que es justo lo que el dueno llama «el programa es confuso».</para>
    ///
    /// <para>Si el documento no esta en ningun grupo —esta archivado, y un archivado no entra
    /// en ninguno— no se toca nada: mover los desplegables a un grupo cualquiera seria peor que
    /// dejarlos donde estan.</para>
    /// </remarks>
    /// <param name="casoId">El documento que se acaba de abrir.</param>
    private void SituarLosDesplegablesEn(long casoId)
    {
        if (_documentos.Any(entrada => entrada.Id == casoId)) return;

        // ⛔ Se llega aqui con un documento que YA NO ESTA en la lista de trabajo cuando se
        // entra desde fuera a uno resuelto: desde el grupo del dia, o desde el flujo de
        // trabajo. Se rehace la lista con el dentro como invitado, porque si no la cabecera
        // diria un grupo y un documento que no son los que se tienen delante. Es la misma
        // queja del dueno que arreglo esta funcion el 2026-09-07, con la lista ya filtrada.
        if (GruposParaCorregir.DondeEsta(_todosLosGrupos, casoId) >= 0
            && GruposParaCorregir.DondeEsta(_grupos, casoId) < 0)
        {
            LlenarLosGrupos(casoId);
            return;
        }

        if (_grupos.Count == 0) return;

        var donde = GruposParaCorregir.DondeEsta(_grupos, casoId);
        if (donde < 0) return;

        _cambiandoDeGrupo = true;
        _queGrupo.SelectedIndex = donde;
        _cambiandoDeGrupo = false;

        // Se le pasa el documento, asi que lo encuentra en la lista nueva y NO lo vuelve a
        // abrir: ya esta abierto y volver a abrirlo tiraria lo que hubiera tecleado encima.
        LlenarLosDocumentos(casoId);
    }

    /// <summary>
    /// Rehace la linea del documento abierto, y solo la suya, con lo que le falta AHORA.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>Esta es la mitad que faltaba de la queja del dueno</b>, del 2026-09-07:
    /// <i>«Cuando guardo información ya corregida no cambia de estado, sigue igual»</i>. Lo
    /// medido con la ventana abierta antes de tocar nada fue que el veredicto estaba bien
    /// calculado —Inicio contaba 71 de 75, y el pie de Correccion decia al guardar «este
    /// documento ya está listo para asignar»— y lo que no cambiaba era la LISTA, porque su
    /// linea nunca llevo esa frase.</para>
    ///
    /// <para>⚠️ Se rehace SOLO la del documento abierto y no la pasada entera: releer la base
    /// completa en cada pulsacion de Guardar costaria las dos decimas de la pantalla por cada
    /// campo corregido. Aqui son unas pocas consultas cortas de ESE documento, que es lo mismo
    /// que ya hace la cola de Completar al avanzar.</para>
    /// </remarks>
    private void RehacerLaLineaDelDocumento()
    {
        if (Servicios is null || _casoAbierto == 0) return;

        var nuevas = _documentos.ToList();
        var donde = nuevas.FindIndex(entrada => entrada.Id == _casoAbierto);
        if (donde < 0) return;

        if (Servicios.Casos.Obtener(_casoAbierto) is not Caso caso) return;

        var gente = Servicios.Personas.DeCaso(caso.Id);
        var procedencias = Grupo.ProcedenciasDeUnaPasada.DeUnDocumento(Servicios.Procedencia, caso, gente);
        var frase = Grupo.LasDosPreguntas.LoQueLeFaltaAlDocumento(
            Grupo.LoQueLeFalta.DeUnDocumento(caso, gente, procedencias).Count, gente.Count == 0);

        var original = caso.DuplicadoDe is long queCaso ? Servicios.Casos.Obtener(queCaso) : null;
        nuevas[donde] = new CasoEnElDesplegable(
            caso.Id,
            TextoDelDesplegable.Componer(
                caso, gente.FirstOrDefault()?.Nombre, gente.Count, original,
                LoQueContestoElCompanero.CuantasContestadas(gente),
                QuienMarcoEsteCaso(caso, []),
                frase));

        _documentos = nuevas;
        _cambiandoDeCaso = true;
        _queCaso.ItemsSource = nuevas;
        _queCaso.SelectedIndex = donde;
        _cambiandoDeCaso = false;
    }

    /// <summary>
    /// El nombre de quien puso el estado de ese caso, o nulo si nadie lo puso.
    /// </summary>
    /// <remarks>
    /// Nulo y no un nombre de relleno: es la misma regla que en el modelo. Un hueco se dice
    /// callando el nombre, nunca poniendo el del primero de la lista.
    /// </remarks>
    private string? QuienMarcoEsteCaso(Caso caso, Dictionary<long, string?> nombrePorId)
    {
        if (Servicios is null || caso.EstadoMarcadoPor is not long id) return null;

        if (!nombrePorId.TryGetValue(id, out var nombre))
        {
            nombre = Servicios.Companeros.Obtener(id)?.Nombre;
            nombrePorId[id] = nombre;
        }

        return nombre;
    }

    /// <summary>Que caso hay detras de cada entrada del desplegable.</summary>
    /// <param name="Id">El numero interno del caso.</param>
    /// <param name="Como">Como se lee en el desplegable.</param>
    private sealed record CasoEnElDesplegable(long Id, string Como);
}

/// <summary>
/// Las palabras de los dos desplegables, fuera del XAML para poder leerlas en una prueba.
/// </summary>
public static class TextoDeLosGrupos
{
    /// <summary>Lo que se dice cuando no hay ni un documento en la base.</summary>
    public const string NoHayNingunDocumento = "no hay ningún documento en la base todavía";

    /// <summary>
    /// «8 en este grupo · 5 grupos · 75 documentos en total», y que significa «listo para
    /// asignar».
    /// </summary>
    /// <remarks>
    /// <para>Las tres cifras van juntas porque una sola no se puede comprobar (CLAUDE.md §8):
    /// sin el total, «8 documentos» no dice si falta algo por ver; sin el numero de grupos, no
    /// se sabe cuanto queda por recorrer.</para>
    ///
    /// <para>Y el significado del rotulo va aqui, UNA vez, en vez de repetirlo en cada entrada
    /// del desplegable: es el criterio C17-1 —«listo» a secas se lee como «listo para
    /// viajar»— resuelto sin gastar treinta caracteres por linea en una lista de 61.</para>
    /// </remarks>
    /// <remarks>
    /// ⚠️ <b>2026-09-09: quedo SIN USAR y NO se borra.</b> Desde que Correccion solo ensena lo
    /// que pide algo hacen falta CUATRO cifras y no tres —los que piden algo, y de cuantos—, y
    /// la compone <c>TextoDeLaSalidaDeCorreccion.Denominador</c>. Se queda porque la regla del
    /// dueno del 2026-08-19 es que durante el desarrollo «sin usar» y «sin terminar» se ven
    /// iguales desde fuera, y esto se decide al cerrar la fase, no ahora. Va nombrado en la
    /// entrega para que el planificador lo anote en <c>PENDIENTES.md</c>.
    /// </remarks>
    /// <param name="enEsteGrupo">Cuantos documentos trae el grupo elegido.</param>
    /// <param name="cuantosGrupos">Cuantos grupos hay.</param>
    /// <param name="enTotal">Cuantos documentos hay en total, sin los archivados.</param>
    public static string Denominador(int enEsteGrupo, int cuantosGrupos, int enTotal)
        => $"{Fichas.Reportes.Reglas.Plural.Con(enEsteGrupo, "documento", "documentos")} en este grupo"
           + $" · {Fichas.Reportes.Reglas.Plural.Con(cuantosGrupos, "grupo", "grupos")}"
           + $" · {enTotal} en total";
}
