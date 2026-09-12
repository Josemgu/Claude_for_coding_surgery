using System.Diagnostics;
using Fichas.App.Cascara;
using Fichas.Contratos.Consultas;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Correccion;

/// <summary>
/// La pantalla de correccion: el documento a un lado y los campos al otro.
/// </summary>
/// <remarks>
/// ⛔ Terreno del programador de Correccion (fase C4). Nadie mas escribe aqui.
/// <para>
/// Todo lo que decide esta pantalla vive en <see cref="ModeloDeCorreccion"/>, que se prueba
/// sin ventana. Aqui solo se pinta y se atan los sucesos. <b>No hay ni un cuadro modal en
/// toda la carpeta</b>, y se comprueba con un grep de los cuatro nombres con los que se abre
/// uno en WinUI: lo que hay que decir sale en la franja de la cascara y en el acuse del pie
/// (requisito 9 del dueno). El grep tiene que dar cero, asi que esos nombres no se escriben
/// ni en un comentario.
/// </para>
/// </remarks>
public sealed partial class PaginaDeCorreccion : PaginaDeFichas
{
    /// <summary>Ancho al que se rasteriza la hoja; el mismo tope que usa la lectura.</summary>
    private const int AnchoDeLaHoja = 1700;

    /// <summary>
    /// Quien firma mientras no haya sesion de usuario.
    /// </summary>
    /// <remarks>
    /// ⚠️ Provisional y dicho en la entrega: el programa todavia no sabe quien esta sentado
    /// delante. Se toma el primer companero activo, y la firma queda con SU nombre. Cuando
    /// exista la sesion, este campo desaparece; hasta entonces la regla permanente 5 se
    /// cumple en lo que importa —la firma la pulsa una persona— pero el nombre puede no ser
    /// el suyo. Vale 0 si no hay ningun companero activo, y entonces el almacen se niega a firmar.
    /// </remarks>
    private long _quienFirma;

    /// <summary>El modelo de la pantalla; nulo hasta que llegan los servicios.</summary>
    private ModeloDeCorreccion? _modelo;

    /// <summary>Las fichas que el repetidor ya creo y a las que se ataron los sucesos, para no atarlos dos veces.</summary>
    private readonly List<FichaDeCampo> _fichas = [];

    /// <summary>El id del caso que se tiene delante; 0 mientras no se abrio ninguno.</summary>
    /// <remarks>
    /// Es lo que se compara al volver de leer las bandas en otro hilo: si cambio, lo leido es
    /// de otro papel y se tira.
    /// </remarks>
    private long _casoAbierto;

    /// <summary>Guarda para que cambiar el segundo desplegable desde el codigo no dispare <see cref="AlElegirUnCaso"/>.</summary>
    private bool _cambiandoDeCaso;

    /// <summary>Monta la pantalla.</summary>
    public PaginaDeCorreccion() => InitializeComponent();

    /// <summary>Lo que costo abrir el ultimo caso, en milisegundos; lo lee la medicion.</summary>
    public double MilisegundosDelUltimoCaso { get; private set; }

    /// <summary>Monta el modelo, llena el desplegable y abre el primer caso.</summary>
    protected override void AlLlegar()
    {
        if (Servicios is null) return;

        _modelo = new ModeloDeCorreccion(
            Servicios.Casos, Servicios.Personas, Servicios.Procedencia,
            Servicios.LecturaDePdf, Servicios.Extraccion, Servicios.Reloj, Servicios.Companeros);

        _quienFirma = Servicios.Companeros
            .Listar(FiltroDeCompaneros.Activos, Pagina.Primera(1))
            .Elementos.FirstOrDefault()?.Id ?? 0;

        _visor.HojaPedida += AlPedirOtraHoja;
        _visor.ArrastreTerminado += AlTerminarUnArrastre;
        _visor.NoSePudoPintar += AlNoPoderPintar;
        LlenarLosGrupos();
    }

    /// <summary>Abre el caso elegido.</summary>
    private void AlElegirUnCaso(object quien, SelectionChangedEventArgs cuando)
    {
        if (_cambiandoDeCaso) return;
        if (_queCaso.SelectedItem is not CasoEnElDesplegable elegido) return;
        AbrirElCaso(elegido.Id);
    }

    /// <summary>
    /// Abre un caso y anota cuanto costo. Un caso que no se pueda abrir avisa y no tumba nada.
    /// </summary>
    /// <remarks>
    /// Es publica porque a esta pantalla se llega desde fuera con un documento concreto: la
    /// pantalla del grupo, la ventana de incompletos y la cola la llaman justo despues de
    /// navegar. Pinta todo en el hilo de la ventana y deja para otro hilo solo la lectura del
    /// escaneo, que es lo que congelaba la ventana 6,6 s.
    /// </remarks>
    /// <param name="casoId">El numero interno del caso; si ya no esta, la cabecera lo dice y no se lanza nada.</param>
    public void AbrirElCaso(long casoId)
    {
        if (_modelo is null || Servicios is null) return;

        var cronometro = Stopwatch.StartNew();
        var abrio = _modelo.Cargar(casoId);
        _casoAbierto = casoId;

        // A esta pantalla se llega tambien DESDE FUERA —la pantalla del grupo, la ventana de
        // lo que no esta completo, la cola—, y esos caminos no pasan por los desplegables. Sin
        // esto, la cabecera diria «61 documentos en este grupo» del grupo del 17 con un
        // documento de octubre delante. Medido con la ventana abierta el 2026-09-07.
        SituarLosDesplegablesEn(casoId);

        RepartirLasFichas();
        MostrarLoQueContestoElCompanero();
        MostrarComoQuedoLaMarca();
        MostrarElCuadroDeLaPersonaAMano();
        // ⛔ Un documento al que ya no le falta nada NO esta en la lista de trabajo, y si se
        // llega a el desde fuera hay que decirlo: sin esta banda se leeria como uno mas de la
        // cola de Correccion. Es la decision del dueno del 2026-09-07: «si voy a Correccion no
        // debe estar ahi, porque ya esta todo listo».
        MostrarSiYaEstaResuelto();
        Recontar();
        MostrarLaHoja(_modelo.Caso?.PaginaPdf ?? 1);
        cronometro.Stop();

        MilisegundosDelUltimoCaso = cronometro.Elapsed.TotalMilliseconds;
        Servicios.Registro.AnotarNavegacion($"Correccion abre el caso {casoId}", MilisegundosDelUltimoCaso);

        var plan = _modelo.PlanearLasBandas();
        Servicios.Avisos.CerrarTodos();
        Servicios.Avisos.Dejar([.. _modelo.AvisosDeLaCabecera, .. plan.Avisos]);
        if (!abrio) _deQueVa.Text = "ese caso ya no está";

        // Preguntar al documento donde estaba cada campo va DESPUES de pintar y EN OTRO HILO.
        // Medido con la ventana abierta sobre los siete escaneos del dueno: hacerlo aqui
        // mismo dejaba la ventana congelada 6,6 s de los 8,3 que costaba entrar. Son las
        // palabras del dueno sobre el programa viejo: «conmigo es lento, se corta».
        if (plan.Peticion is not null) _ = BuscarLasBandas(casoId, plan.Peticion);
    }

    /// <summary>
    /// Busca en el documento las bandas que faltan, sin congelar la ventana.
    /// </summary>
    /// <remarks>
    /// ⚠️ El <c>Task.Run</c> solo envuelve <see cref="ModeloDeCorreccion.LeerLasBandas"/>, que
    /// no toca ni las fichas ni el almacen —hay pruebas que lo fijan—. Repartir lo leido y
    /// pintar vuelven a este hilo, que es el de la ventana.
    /// <para>
    /// ⛔ Y el <c>catch</c> no calla: escribe la linea en el pie y en el cuaderno. Es la
    /// regla que dejo el defecto que QA midio en Importar.
    /// </para>
    /// </remarks>
    /// <param name="casoId">El caso para el que se leen; si al volver ya no es el abierto, lo leido se tira.</param>
    /// <param name="peticion">Que archivo y que campos, tal como lo planeo el modelo.</param>
    private async Task BuscarLasBandas(long casoId, PeticionDeBandas peticion)
    {
        try
        {
            var cronometro = Stopwatch.StartNew();
            var leidas = await Task.Run(() => _modelo!.LeerLasBandas(peticion)).ConfigureAwait(true);
            cronometro.Stop();

            // Mientras se leia se pudo cambiar de caso. Lo leido es de otro papel: se tira.
            if (_casoAbierto != casoId || _modelo is null) return;

            var avisos = _modelo.AplicarLasBandas(leidas);
            foreach (var ficha in _fichas) ficha.Refrescar();
            if (avisos.Count > 0) Servicios?.Avisos.Dejar(avisos);

            Servicios?.Registro.Anotar(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "VISOR  bandas del documento del caso {0}: {1} de {2} campos en {3:F0} ms",
                casoId, leidas.PorClave.Count, peticion.Faltantes.Count, cronometro.Elapsed.TotalMilliseconds));
        }
        catch (Exception fallo)
        {
            var linea = TextoDelVisor.LineaDeFallo(_visor.Hoja, fallo);
            Decir(linea);
            Servicios?.Registro.Anotar($"VISOR  buscando las bandas  {linea}");
        }
    }

    /// <summary>Reparte los campos en los dos grupos: lo dudoso arriba y lo demas cerrado.</summary>
    private void RepartirLasFichas()
    {
        if (_modelo is null) return;

        _fichas.Clear();
        var dudosos = _modelo.Dudosos;
        var resto = _modelo.Resto;

        _loDudoso.ItemsSource = dudosos;
        _elResto.ItemsSource = resto;

        // Los dos titulos los escribe Recontar, que corre justo detras y vuelve a correr
        // cada vez que algo cambia: aqui se decide QUE campo va en cada grupo, y alli QUE
        // cuenta lleva cada uno. Dos sitios escribiendo la misma frase es como acabaron
        // diciendo cosas distintas.
        _marcoDeLoDudoso.Visibility = dudosos.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _marcoDelResto.Visibility = resto.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Pone arriba lo que el companero contesto de cada persona, o esconde el marco entero.
    /// </summary>
    /// <remarks>
    /// ⛔ Solo se lee: aqui no hay ni un boton, y no se escribe en ninguna fila. Lo que dice
    /// el companero es suyo y no firma nada de Miguel (regla permanente 5).
    /// <para>
    /// El marco se esconde cuando nadie contesto: una seccion vacia titulada «Lo que
    /// contestó el compañero» se lee como «no contestó», y no es lo mismo que «todavia no le
    /// ha llegado el Excel de vuelta». Lo que dice si contesto o no <b>sin abrir el
    /// documento</b> es la linea del desplegable, que lo dice con palabras.
    /// </para>
    /// </remarks>
    private void MostrarLoQueContestoElCompanero()
    {
        if (_modelo is null) return;

        MostrarLaRecomendacionDeCadaPersona();

        var respuestas = _modelo.RespuestasDeLosCompaneros;
        _lasRespuestas.ItemsSource = respuestas;
        _marcoDeLasRespuestas.Visibility = respuestas.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        _tituloDeLasRespuestas.Text = respuestas.Count == 1
            ? "Lo que hay contestado de cada persona · 1 persona"
            : $"Lo que hay contestado de cada persona · {respuestas.Count} personas";
    }

    /// <summary>
    /// Pone, para CADA persona del documento, si su recomendacion esta confirmada en el
    /// sistema del obispo.
    /// </summary>
    /// <remarks>
    /// ⛔ Salen todas, tambien las que nadie miro (criterio C17-3): «sin mirar» no es «no», y
    /// callar a las que nadie miro es hacer invisible justo lo que falta por hacer. Un
    /// documento sin ninguna persona leida esconde el marco entero: una lista vacia bajo ese
    /// titulo se leeria como «no hay nadie que verificar», que es otra cosa.
    /// <para>Aqui no se escribe nada: solo se lee (regla permanente 5).</para>
    /// </remarks>
    private void MostrarLaRecomendacionDeCadaPersona()
    {
        if (_modelo is null) return;

        var porPersona = _modelo.LaRecomendacionDeCadaPersona;
        _laRecomendacion.ItemsSource = porPersona;
        _marcoDeLaRecomendacion.Visibility = porPersona.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _lineaDeLaRecomendacion.Text = _modelo.LineaDeLaRecomendacion;
    }

    /// <summary>Ata cada ficha a su campo en cuanto el repetidor la crea.</summary>
    private void AlPrepararUnaFicha(ItemsRepeater quien, ItemsRepeaterElementPreparedEventArgs cuando)
    {
        if (_modelo is null) return;
        if (cuando.Element is not FichaDeCampo ficha) return;
        if (quien.ItemsSourceView.GetAt(cuando.Index) is not CampoEnPantalla campo) return;

        ficha.Mostrar(_modelo, campo);
        if (!_fichas.Contains(ficha))
        {
            ficha.Tecleo += AlTeclearEnUnCampo;
            ficha.TomoElFoco += AlEnfocarUnCampo;
            ficha.PidioFirmar += AlPedirLaFirma;
            ficha.PidioMarcarQueNoEstaEnElPapel += AlPedirMarcarQueNoEstaEnElPapel;
            _fichas.Add(ficha);
        }
    }

    /// <summary>Vuelve a contar en el pie al soltar cada tecla.</summary>
    private void AlTeclearEnUnCampo(object? quien, EventArgs cuando) => Recontar();

    /// <summary>
    /// Ilumina en el documento la banda del campo que acaba de recibir el foco.
    /// </summary>
    /// <remarks>
    /// La funcion que el planificador encontro en Rossum. Si el campo es de otra hoja, se
    /// cambia de hoja: un caso de doce personas ocupa seis, y tabular a alguien de la
    /// cuarta tiene que ensenar la cuarta.
    /// </remarks>
    private void AlEnfocarUnCampo(object? quien, EventArgs cuando)
    {
        if (quien is not FichaDeCampo ficha || ficha.Campo is null) return;
        var hoja = ficha.Campo.PaginaPdf ?? _visor.Hoja;
        if (hoja != _visor.Hoja) MostrarLaHoja(hoja);
        _visor.Enfocar(ficha.Campo.Banda);
    }

    /// <summary>Firma el campo. Regla permanente 5: esto solo pasa porque Miguel lo pulso.</summary>
    /// <remarks>
    /// Se refrescan TODAS las fichas y no solo la suya: firmar un campo puede haberlo
    /// escrito antes, y eso cambia la cuenta del pie y la frase del documento.
    /// </remarks>
    private void AlPedirLaFirma(object? quien, EventArgs cuando)
    {
        if (_modelo is null || Servicios is null) return;
        if (quien is not FichaDeCampo ficha || ficha.Campo is null) return;

        var resultado = _modelo.Firmar(ficha.Campo, _quienFirma);
        Servicios.Avisos.CerrarTodos();
        if (resultado.HayAvisos) Servicios.Avisos.Dejar(resultado.Avisos);

        foreach (var otra in _fichas) otra.Refrescar();
        Recontar();
    }

    /// <summary>
    /// Marca o desmarca «no está en el papel». NO es una firma y no pone verificado.
    /// </summary>
    private void AlPedirMarcarQueNoEstaEnElPapel(object? quien, bool marcado)
    {
        if (_modelo is null || Servicios is null) return;
        if (quien is not FichaDeCampo ficha || ficha.Campo is null) return;

        var resultado = _modelo.MarcarQueNoEstaEnElPapel(ficha.Campo, marcado);
        Servicios.Avisos.CerrarTodos();
        if (resultado.HayAvisos) Servicios.Avisos.Dejar(resultado.Avisos);

        // Se repinta pase lo que pase: si no entro, la casilla tiene que volver sola a como
        // esta la base. Una casilla marcada sobre una base que dice lo contrario es la
        // mentira que mas cuesta descubrir.
        ficha.Refrescar();
        Recontar();
    }

    /// <summary>
    /// Rasteriza una hoja y se la da al visor. Que no se pueda leer NO tumba nada, y NO se calla.
    /// </summary>
    /// <remarks>
    /// ⛔ El <c>try</c> no esta para silenciar: esta para que el fallo salga como una linea en
    /// el pie en vez de tumbar la pantalla. Es la regla que dejo el defecto que QA midio en
    /// Importar, donde el manejador se tragaba la excepcion y los botones parecian muertos.
    /// <para>
    /// Se atrapa <see cref="Exception"/> a secas por lo mismo que en <c>MotorDeImportacion</c>:
    /// por debajo estan PDFium y PdfPig, que levantan cada una lo suyo, y una lista de tipos
    /// seria la lista de los fallos que se me ocurrieron.
    /// </para>
    /// </remarks>
    /// <param name="hoja">Que hoja del PDF se pide, base 1; el visor la acota antes de pedirla.</param>
    private void MostrarLaHoja(int hoja)
    {
        if (Servicios is null || _modelo?.Caso is null) return;

        var ruta = _modelo.Caso.RutaPdf;
        if (string.IsNullOrWhiteSpace(ruta))
        {
            _visor.MostrarHoja(null, 0, TextoDelVisor.SinEscaneo);
            return;
        }

        try
        {
            var imagen = Servicios.LecturaDePdf.RasterizarPagina(ruta, hoja, AnchoDeLaHoja);
            var total = Servicios.LecturaDePdf.ContarPaginas(ruta);
            _visor.MostrarHoja(imagen, total, TextoDelVisor.HojaQueNoSePudoAbrir(hoja, ruta));
        }
        catch (Exception fallo)
        {
            var linea = TextoDelVisor.LineaDeFallo(hoja, fallo);
            _visor.MostrarHoja(null, 0, linea + " Los campos se corrigen igual, sin la imagen al lado.");
            Decir(linea);
            Servicios.Registro.Anotar($"VISOR  {linea}");
        }
    }

    /// <summary>Dice una linea en el pie. Un fallo que no se ve es un fallo que no existe.</summary>
    /// <remarks>Sin ventana principal —en una prueba— no dice nada y no lanza.</remarks>
    /// <param name="linea">Lo que se lee en el acuse del pie; se apaga solo a los pocos segundos.</param>
    private static void Decir(string linea)
    {
        if (App.Ventana is VentanaPrincipal ventana) ventana.AcuseDelPie.Decir(linea);
    }

    /// <summary>El visor no pudo pintar: se dice en el pie y se anota, nunca se calla.</summary>
    private void AlNoPoderPintar(object? quien, string linea)
    {
        Decir(linea);
        Servicios?.Registro.Anotar($"VISOR  {linea}");
    }

    /// <summary>El visor pidio otra hoja.</summary>
    private void AlPedirOtraHoja(object? quien, int hoja) => MostrarLaHoja(hoja);

    /// <summary>
    /// Anota en el cuaderno lo que costo el arrastre. Es de donde sale la cifra del informe.
    /// </summary>
    /// <remarks>
    /// En Tk el mismo arrastre costaba 26,8 ms y bajo a 1,4 cacheando la hoja reescalada. Sin
    /// esta linea, aqui habria que decir «va fluido», que no es una medicion.
    /// </remarks>
    private void AlTerminarUnArrastre(object? quien, MedidaDelArrastre medida)
        => Servicios?.Registro.Anotar(string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "VISOR  arrastre de {0} tramos  media {1:F2} ms  peor {2:F2} ms  "
            + "pidio {3:F0}x{4:F0} px  movio {5:F0}x{6:F0} px  zoom {7:P0}",
            medida.Tramos, medida.MediaMs, medida.MaximoMs,
            medida.PedidoX, medida.PedidoY, medida.MovidoX, medida.MovidoY, _visor.Zoom));

    /// <summary>
    /// Da el documento por completo sin firmar ni un campo. Lo pulsa el administrador.
    /// </summary>
    /// <remarks>
    /// Palabras del dueno: <i>«Yo puedo completarlos también, los paquetes, desde el sistema
    /// sin pasar la verificación, y cuando pase eso debe decir "el administrador lo hizo"»</i>.
    /// <para>
    /// ⛔ Aqui no se decide nada: quien es el administrador y que se escribe lo decide
    /// <see cref="ModeloDeCorreccion.DarPorCompletoComoAdministrador"/>, que se prueba sin
    /// ventana. Esto pinta lo que salio.
    /// </para>
    /// <para>
    /// Se repinta la cabecera pase lo que pase, incluso cuando no se escribio: la frase de la
    /// marca sale de la base, y si se dejara la de antes diria algo que la base no dice.
    /// </para>
    /// </remarks>
    private void AlPulsarDarPorCompleto(object quien, RoutedEventArgs cuando)
    {
        if (_modelo is null || Servicios is null) return;

        var resultado = _modelo.DarPorCompletoComoAdministrador();
        Servicios.Avisos.CerrarTodos();
        if (resultado.HayAvisos) Servicios.Avisos.Dejar(resultado.Avisos);

        MostrarComoQuedoLaMarca();
        if (App.Ventana is VentanaPrincipal ventana && resultado.Avisos.Count > 0)
            ventana.AcuseDelPie.Decir(resultado.Avisos[0].Linea);

        // La linea del desplegable tambien lleva la marca: si no se rehiciera, la lista
        // seguiria diciendo «sin contestar» de un documento que se acaba de dar por completo.
        LlenarLosGrupos(_casoAbierto);
    }

    /// <summary>
    /// Ensena como quedo marcado el documento, y esconde el marco cuando nadie lo marco.
    /// </summary>
    /// <remarks>
    /// El boton del atajo se vuelve a decidir aqui y no solo al llegar: los companeros se
    /// dan de alta mientras el programa esta abierto, y un boton que solo aparece al
    /// reiniciar obliga a reiniciar para descubrirlo.
    /// </remarks>
    private void MostrarComoQuedoLaMarca()
    {
        if (_modelo is null) return;

        var marca = _modelo.ComoQuedoLaMarca;
        _laMarcaDelEstado.Text = marca;
        _marcoDeLaMarca.Visibility = marca.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        // El sitio del pie o lleva el boton o lleva el motivo, nunca los dos ni ninguno: un
        // hueco en blanco se lee como que la funcion no existe.
        var hayAtajo = _modelo.HayAtajoDeAdministrador;
        _botonDelAdministrador.Visibility = hayAtajo ? Visibility.Visible : Visibility.Collapsed;
        _porQueNoHayAtajo.Text = _modelo.PorQueNoHayAtajo;
        _porQueNoHayAtajo.Visibility =
            _porQueNoHayAtajo.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Guarda. Un campo que no valida NO tumba el guardado de los demas.</summary>
    private void AlPulsarGuardar(object quien, RoutedEventArgs cuando)
    {
        if (_modelo is null || Servicios is null) return;

        // Se pregunta ANTES de guardar: lo que hay que saber es si este guardado es el que
        // saca al documento de Correccion, y despues de escribir esa pregunta ya no se puede
        // hacer. Sale de la pasada que armo la lista, sin volver a la base.
        var leFaltabaAntes = _loQueLeFalta?.LeFaltaAlgo(_casoAbierto) ?? false;

        var resultado = _modelo.Guardar();
        foreach (var ficha in _fichas) ficha.Refrescar();
        Recontar();

        // El acuse va SIEMPRE, guarde lo que guarde: un boton que hace su trabajo en
        // silencio es, para quien lo mira, un boton roto (dueno, 2026-09-04).
        //
        // ⚠️ Dentro de la cola lo dice la cola, y no se escribe dos veces: su linea contesta
        // lo mismo que esta y ademas dice si el documento sale y cuantos quedan, que es lo
        // que el dueno pidio ver el 2026-09-06. El motivo entero esta en
        // PaginaDeCorreccion.Cola.cs.
        if (_cola is null && App.Ventana is VentanaPrincipal ventana)
            ventana.AcuseDelPie.Decir(resultado.LineaDelAcuse);

        Servicios.Avisos.CerrarTodos();
        Servicios.Avisos.Dejar([.. resultado.Avisos, .. _modelo.AvisosDeLaCabecera]);

        // ⛔ La linea del documento en la lista se rehace SIEMPRE al guardar, y esa es la queja
        // del dueno del 2026-09-07: «cuando guardo información ya corregida no cambia de
        // estado, sigue igual». El veredicto ya estaba bien —lo dicen el pie y el acuse—; lo
        // que no cambiaba era lo que se ve en la lista. Va antes de encadenar, para que sea la
        // linea de ESTE documento la que se rehaga y no la del siguiente.
        RehacerLaLineaDelDocumento();

        // Y lo ultimo, en este orden: si se entro desde la cola, guardar encadena; si no, y el
        // documento acaba de dejar de tener huecos, sale de Correccion y se dice a que grupo
        // paso. Cada uno mira si le toca a el, y nunca hablan los dos: la cola ya dice lo mismo
        // y ademas cuantos quedan.
        SeguirLaCola(resultado);
        SeguirElFlujoDeTrabajo(resultado, leFaltabaAntes);
    }

    /// <summary>
    /// Escribe la cuenta del pie y la de los dos grupos: lo que queda y lo que no vale.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Los titulos de los dos grupos se recuentan aqui, y antes no.</b> Medido con la
    /// ventana abierta el 2026-09-05: tras guardar, el titulo seguia diciendo «Por comprobar
    /// · 1 campo» mientras el pie decia «Quedan 0 campos por comprobar». Dos cifras que se
    /// contradicen en la misma pantalla, y ninguna forma de saber cual creer.
    /// <para>
    /// Las fichas NO se reparten otra vez: mover un campo de grupo mientras Miguel trabaja
    /// en el se lo quitaria de delante. Cambian las cuentas, no los sitios.
    /// </para>
    /// </remarks>
    private void Recontar()
    {
        if (_modelo is null) return;
        var porComprobar = _modelo.Dudosos.Count;
        var noValen = _modelo.Campos.Count(campo => _modelo.MotivoDe(campo) is not null);
        var firmados = _modelo.CuantosFirmados;

        // ⛔ El veredicto va aqui y no solo en el acuse de Guardar, y esa es la mitad que
        // faltaba: el dueno lo dijo el 2026-09-05 —«si el sistema escanea y verifica todos
        // los campos SIN MI INTERVENCION, debe decir listo para asignar»—. Si hubiera que
        // pulsar Guardar para leerlo, pulsar seria otra vez un peaje. NO firma nada: la
        // cuenta de «dados por buenos» sigue saliendo del almacen, aparte y sin tocarla.
        // «Quedan 1 campo» salio en la medicion con la ventana abierta del 2026-09-05, y
        // delata lo mismo que un «1 campos»: que nadie leyo la pantalla.
        // El significado va pegado al rótulo (C17-1): «listo» a secas se lee como «listo
        // para viajar», que es otra pregunta y la contesta otra persona.
        // ⚠️ Y la tercera frase, del 2026-09-06: un documento sin ninguna persona leida NO
        // esta listo, y con los cinco campos del caso bien la cuenta de campos es 0. Sin este
        // caso, la cabecera diria «Quedan 0 campos por comprobar» sobre un documento que la
        // pantalla no da por listo: otra vez dos cifras de la misma pantalla que no encajan.
        // ⛔ 2026-09-07: las tres frases de aquí —«Listo para asignar · el sistema llenó
        // todos los campos», «Sin ninguna persona leída · …» y «Quedan N campos por
        // comprobar»— se sustituyen por UNA lectura, que es la misma que leen Inicio, Asignar,
        // Revisar y el grupo del día. Lo que decían sigue dicho, en su detalle.
        var lectura = Fichas.App.Vocabulario.LoQueSeLeeDeUnDocumento.De(
            Fichas.Contratos.Modelos.EstadoDeRecomendacion.SinMarcar,
            archivado: false,
            cuantoLeFalta: _modelo.ListoParaAsignar ? 0 : porComprobar,
            _modelo.SinNingunaPersonaLeida,
            quienLoLleva: string.Empty,
            firma: string.Empty);
        var estado = $"{lectura.PalabraEnCabecera} · {lectura.Detalle}";
        var buenos = $"{firmados} {Dados(firmados)} por {Buenos(firmados)}";
        _cuenta.Text = noValen == 0
            ? $"{estado} · {buenos}"
            : $"{estado} · {noValen} no {(noValen == 1 ? "vale" : "valen")} · {buenos}";

        RecontarLosTitulos();
    }

    /// <summary>Pone en cada titulo cuantos de SUS campos siguen pendientes.</summary>
    private void RecontarLosTitulos()
    {
        if (_modelo is null) return;

        var dudosos = _modelo.Dudosos;
        var enLoDudoso = ContarPendientes(_loDudoso.ItemsSource, dudosos);
        var enElResto = ContarPendientes(_elResto.ItemsSource, dudosos);
        var totalDelResto = (_elResto.ItemsSource as IReadOnlyList<CampoEnPantalla>)?.Count ?? 0;

        _tituloDeLoDudoso.Text = $"Por comprobar · {enLoDudoso} {Campos(enLoDudoso)}";
        _tituloDelResto.Text = enElResto == 0
            ? $"Lo demás · {totalDelResto} {Campos(totalDelResto)} ya {(totalDelResto == 1 ? "leído" : "leídos")}"
            : $"Lo demás · {enElResto} {Campos(enElResto)} por comprobar";
    }

    /// <summary>Cuantos de esa lista siguen estando entre los dudosos.</summary>
    /// <param name="lista">El <c>ItemsSource</c> de uno de los dos repetidores; otra cosa cuenta cero.</param>
    /// <param name="dudosos">Los dudosos de AHORA, con lo tecleado incluido.</param>
    private static int ContarPendientes(object? lista, IReadOnlyList<CampoEnPantalla> dudosos)
        => lista is IReadOnlyList<CampoEnPantalla> campos ? campos.Count(dudosos.Contains) : 0;

    /// <summary>«campo» o «campos», que un «1 campos» delata que nadie leyo la pantalla.</summary>
    /// <param name="cuantos">La cifra que va delante.</param>
    private static string Campos(int cuantos) => cuantos == 1 ? "campo" : "campos";

    /// <summary>«dado» o «dados», por lo mismo.</summary>
    /// <param name="cuantos">La cifra que va delante.</param>
    private static string Dados(int cuantos) => cuantos == 1 ? "dado" : "dados";

    /// <summary>«bueno» o «buenos», por lo mismo.</summary>
    /// <param name="cuantos">La cifra que va delante.</param>
    private static string Buenos(int cuantos) => cuantos == 1 ? "bueno" : "buenos";

}
