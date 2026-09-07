using System.Runtime.InteropServices;

namespace Fichas.App.Cascara;

/// <summary>
/// Los cuadros de «abrir», «abrir varios», «elegir carpeta» y «guardar como» de Windows,
/// pedidos directamente a Win32. El UNICO selector de la aplicacion.
/// </summary>
/// <remarks>
/// <para>⚠️ <b>Vive en la cascara y no en una pantalla a proposito.</b> El 2026-09-04 dos
/// programadores en paralelo se toparon con el mismo fallo —el boton que abre un cuadro y no
/// abre nada— y lo arreglaron de dos formas incompatibles, una de ellas falsa. Cuando el
/// fallo es de la plataforma y no de una pantalla, el arreglo se encarga UNA vez y se pone
/// aqui. Importar, Reportes y Paquetes usan este y solo este.</para>
///
/// <para><b>Por que NO se usan los selectores de WinRT ni los del Windows App SDK.</b> Se
/// probaron las dos familias en esta maquina el 2026-09-04, con la ventana abierta y pulsando
/// el boton de verdad:</para>
/// <list type="bullet">
/// <item><description><c>Windows.Storage.Pickers.FileOpenPicker</c> atado con
/// <c>WinRT.Interop.InitializeWithWindow.Initialize</c>: <c>PickMultipleFilesAsync</c> <b>no
/// vuelve nunca y tampoco lanza nada</b>. La sonda escribio en <c>fichas.log</c> «atado a la
/// ventana, voy a PickMultipleFilesAsync» y ocho segundos despues no habia ni una linea mas,
/// ni ventana nueva en el proceso, ni excepcion recogida por un <c>try/catch</c> puesto
/// alrededor. No habia excepcion que tragarse: la tarea se queda colgada, que es peor, porque
/// ni un manejador bien escrito la ve.</description></item>
/// <item><description><c>Microsoft.Windows.Storage.Pickers</c> —la familia del Windows App
/// SDK, que recibe el <c>WindowId</c> en el constructor y no necesita
/// <c>InitializeWithWindow</c>—: <b>lo mismo, exactamente</b>. Microsoft Learn la recomienda
/// para apps de escritorio nuevas («Open files and folders with Windows App SDK pickers»,
/// consultada el 2026-09-04) y su programador midio que le abria en su arbol de compilacion.
/// <b>En el paquete publicado no abre</b>, y esa es la unica medicion que
/// cuenta.</description></item>
/// </list>
///
/// <para><b>La medicion que zanja el asunto</b>, hecha por el supervisor sobre el paquete
/// publicado —no sobre <c>bin\Release</c>—, arrancando <c>Fichas.exe</c> con
/// <c>--carpeta-de-datos</c> y pulsando los dos botones de Importar por automatizacion de
/// interfaz:</para>
/// <code>
/// botones en la pantalla de Importar: 4 -> 'Open Navigation'  ''  ''  'Detener'
/// pulso el boton de elegir archivos  -> ventanas del proceso tras pulsar: 1
/// pulso el boton de la carpeta       -> ventanas del proceso tras pulsar: 1
/// proceso vivo: True
/// </code>
/// <para>Una ventana, o sea solo la del programa: <b>ningun cuadro se abre</b>. La causa es
/// que los selectores de WinRT necesitan el intermediario del Windows App Runtime, y este
/// programa se publica DESEMPAQUETADO y autocontenido (<c>WindowsPackageType=None</c>,
/// <c>WindowsAppSDKSelfContained=true</c>), que es lo que permite el doble clic sin instalar
/// (regla permanente 2). <b>Si alguien vuelve a proponerlo dentro de un mes, esta es la
/// respuesta, y la valla que lo impide son las pruebas de
/// <c>PruebasDelSelectorDeArchivos</c>.</b></para>
///
/// <para><c>comdlg32</c> y <c>shell32</c> son Win32 puro: no necesitan identidad de paquete,
/// ni intermediario, ni nada registrado. Es lo que usa cualquier programa de escritorio de
/// siempre, y el dueno lo reconoce.</para>
///
/// <para>Esto NO es un cuadro modal de los que prohibe el requisito 4: aquel habla de los
/// avisos del programa —<c>ContentDialog</c>, <c>MessageBox</c>— que interrumpen para decir
/// algo. Este es el cuadro del sistema para elegir un archivo.</para>
/// </remarks>
public static class SelectorDeArchivos
{
    // Las banderas de OPENFILENAME que se usan, con su nombre de Windows al lado.
    private const int OfnSobrescribirPregunta = 0x00000002;     // OFN_OVERWRITEPROMPT
    private const int OfnNoCambiarDeCarpeta = 0x00000008;       // OFN_NOCHANGEDIR
    private const int OfnVariosALaVez = 0x00000200;             // OFN_ALLOWMULTISELECT
    private const int OfnLaRutaTieneQueExistir = 0x00000800;    // OFN_PATHMUSTEXIST
    private const int OfnElArchivoTieneQueExistir = 0x00001000; // OFN_FILEMUSTEXIST
    private const int OfnExplorador = 0x00080000;               // OFN_EXPLORER

    // Las banderas de SHBrowseForFolder, igual.
    private const int SoloCarpetasDeVerdad = 0x00000001; // BIF_RETURNONLYFSDIRS
    private const int ConCasillaParaEscribir = 0x00000010; // BIF_EDITBOX
    private const int CuadroModerno = 0x00000040;        // BIF_NEWDIALOGSTYLE

    // Los dos mensajes del enganche de SHBrowseForFolder, con su nombre de Windows.
    private const int CuadroYaAbierto = 1;                // BFFM_INITIALIZED
    private const int PonerLaSeleccion = 0x400 + 103;     // BFFM_SETSELECTIONW

    /// <summary>Lo mas largo que puede medir una ruta suelta que se devuelva.</summary>
    private const int LargoDeUnaRuta = 4096;

    /// <summary>
    /// El hueco de la seleccion multiple: 96 000 letras.
    /// </summary>
    /// <remarks>
    /// El dueno lo dijo con un numero: «necesito cargar 500 pdf». Con la carpeta delante y
    /// nombres de unas 40 letras, 500 archivos ocupan unas 21 000; se deja mas de cuatro veces
    /// eso porque si el hueco se queda corto <c>comdlg32</c> NO recorta: devuelve falso y el
    /// cuadro se lee como «cancelado», que es un fallo mudo.
    /// </remarks>
    private const int LargoDeVariasRutas = 96_000;

    /// <summary>Lo que cabe en una ruta para <c>SHGetPathFromIDList</c>: <c>MAX_PATH</c>.</summary>
    private const int LargoDeUnaCarpeta = 260;

    // ---- lo que pide cada pantalla ------------------------------------------

    /// <summary>Pide donde guardar un archivo; devuelve la ruta, o nulo si se cerro sin elegir.</summary>
    /// <param name="ventana">El HWND de la ventana de la que cuelga el cuadro.</param>
    /// <param name="titulo">Lo que sale en la barra del cuadro.</param>
    /// <param name="nombrePropuesto">El nombre que aparece ya escrito.</param>
    /// <param name="descripcionDelTipo">Como se llama el tipo, por ejemplo «PDF».</param>
    /// <param name="extension">La extension con su punto, por ejemplo «.pdf».</param>
    /// <param name="carpetaDeSalida">Por donde empieza el cuadro; nulo para que decida Windows.</param>
    public static string? DondeGuardar(
        nint ventana, string titulo, string nombrePropuesto,
        string descripcionDelTipo, string extension, string? carpetaDeSalida = null)
        => Pedir(
            ventana, titulo, nombrePropuesto, descripcionDelTipo, extension, carpetaDeSalida,
            OfnExplorador | OfnSobrescribirPregunta | OfnLaRutaTieneQueExistir | OfnNoCambiarDeCarpeta,
            guardar: true, LargoDeUnaRuta)?.FirstOrDefault();

    /// <summary>Pide un archivo que ya existe; devuelve la ruta, o nulo si se cerro sin elegir.</summary>
    public static string? CualAbrir(
        nint ventana, string titulo, string descripcionDelTipo, string extension,
        string? carpetaDeSalida = null)
        => Pedir(
            ventana, titulo, string.Empty, descripcionDelTipo, extension, carpetaDeSalida,
            OfnExplorador | OfnElArchivoTieneQueExistir | OfnLaRutaTieneQueExistir | OfnNoCambiarDeCarpeta,
            guardar: false, LargoDeUnaRuta)?.FirstOrDefault();

    /// <summary>Pide varios archivos a la vez; devuelve vacio si se cerro sin elegir.</summary>
    /// <remarks>Es por donde entran los PDF escaneados en la pantalla de Importar.</remarks>
    public static IReadOnlyList<string> CualesAbrir(
        nint ventana, string titulo, string descripcionDelTipo, string extension,
        string? carpetaDeSalida = null)
        => RutasDeLaSeleccion(
            Pedir(
                ventana, titulo, string.Empty, descripcionDelTipo, extension, carpetaDeSalida,
                OfnExplorador | OfnVariosALaVez | OfnElArchivoTieneQueExistir
                    | OfnLaRutaTieneQueExistir | OfnNoCambiarDeCarpeta,
                guardar: false, LargoDeVariasRutas)
            ?? []);

    /// <summary>Pide una carpeta; devuelve la ruta, o nulo si se cerro sin elegir.</summary>
    /// <remarks>
    /// <para><c>comdlg32</c> no tiene cuadro de carpeta: el de Windows para esto es
    /// <c>SHBrowseForFolder</c>, de <c>shell32</c>, que es igual de Win32 puro y tampoco pide
    /// identidad de paquete. Con <c>BIF_NEWDIALOGSTYLE</c> sale el cuadro con arbol
    /// redimensionable y boton de «Nueva carpeta», y con <c>BIF_EDITBOX</c> una casilla donde
    /// se puede PEGAR la ruta: bajar a mano nueve carpetas hasta donde el escaner deja los
    /// documentos es lo que hace que alguien deje de usar el boton.</para>
    /// <para>⚠️ Lo que este cuadro NO deja es poner su propio titulo de barra: el
    /// «Browse For Folder» de arriba lo pone Windows y sale en el idioma del sistema. Nuestro
    /// texto —<paramref name="titulo"/>— sale DENTRO, encima del arbol. El cuadro que si deja
    /// poner el titulo es el moderno <c>IFileOpenDialog</c> con <c>FOS_PICKFOLDERS</c>, que
    /// son unas treinta ranuras de COM declaradas a mano. Se deja ANOTADO como decision
    /// abierta, no se toma aqui.</para>
    /// <para>El identificador que devuelve Windows es memoria suya: se suelta SIEMPRE con
    /// <c>CoTaskMemFree</c>, tambien cuando la ruta no se pudo sacar.</para>
    ///
    /// <para>⚠️ <b><paramref name="carpetaDeSalida"/> no es un campo de la estructura.</b> Los
    /// otros tres cuadros llevan la carpeta de partida escrita en <c>OPENFILENAME</c>;
    /// <c>BROWSEINFO</c> no tiene ese campo, y la unica via que da Windows es el enganche:
    /// cuando el cuadro avisa de que ya esta abierto se le manda <c>BFFM_SETSELECTIONW</c>
    /// con la ruta. Por eso este metodo tiene diez lineas mas que los otros y no porque
    /// sobren. Lo pedido por otro programador el 2026-09-05: <c>DondeGuardar</c> ya admitia
    /// carpeta y este no, asi que el cuadro de la carpeta abria en la raiz del perfil.</para>
    /// </remarks>
    /// <param name="ventana">El HWND de la ventana de la que cuelga el cuadro.</param>
    /// <param name="titulo">El texto que sale DENTRO, encima del arbol.</param>
    /// <param name="carpetaDeSalida">Por donde empieza el cuadro; nulo para que decida Windows.</param>
    public static string? QueCarpeta(nint ventana, string titulo, string? carpetaDeSalida = null)
    {
        var hueco = Marshal.AllocHGlobal(LargoDeUnaCarpeta * sizeof(char));
        var punteroDelTitulo = Marshal.StringToHGlobalUni(titulo);
        var punteroDeLaCarpeta = string.IsNullOrWhiteSpace(carpetaDeSalida)
            ? nint.Zero
            : Marshal.StringToHGlobalUni(carpetaDeSalida);

        // Sin carpeta de partida NO se pone enganche: asi el camino de siempre queda igual
        // que estaba y una carpeta que no se pide no puede romper el cuadro de nadie.
        var enganche = punteroDeLaCarpeta == nint.Zero
            ? null
            : new EngancheDelCuadroDeCarpeta(AlAbrirseElCuadroDeCarpeta);

        var elegida = nint.Zero;

        try
        {
            var datos = new DatosDeLaCarpeta
            {
                Ventana = ventana,
                NombreQueSeVe = hueco,
                Titulo = punteroDelTitulo,
                Banderas = SoloCarpetasDeVerdad | CuadroModerno | ConCasillaParaEscribir,
                Enganche = enganche is null ? nint.Zero : Marshal.GetFunctionPointerForDelegate(enganche),
                DatoDeQuienLlama = punteroDeLaCarpeta,
            };

            elegida = SHBrowseForFolderW(ref datos);
            if (elegida == nint.Zero) return null;

            return SHGetPathFromIDListW(elegida, hueco) ? Marshal.PtrToStringUni(hueco) : null;
        }
        finally
        {
            // ⛔ El recolector no sabe que Windows tiene el puntero de este delegado. Si lo
            // recogiera mientras el cuadro esta abierto, la llamada de vuelta caeria en
            // memoria que ya no es suya y el programa se cierra sin decir nada.
            GC.KeepAlive(enganche);

            if (elegida != nint.Zero) CoTaskMemFree(elegida);
            Marshal.FreeHGlobal(hueco);
            Marshal.FreeHGlobal(punteroDelTitulo);
            if (punteroDeLaCarpeta != nint.Zero) Marshal.FreeHGlobal(punteroDeLaCarpeta);
        }
    }

    /// <summary>Lo que Windows llama cuando pasa algo en el cuadro de la carpeta.</summary>
    private delegate int EngancheDelCuadroDeCarpeta(nint ventana, int mensaje, nint parametro, nint dato);

    /// <summary>En cuanto el cuadro esta abierto, se le dice por donde empezar.</summary>
    /// <remarks>
    /// Devuelve cero siempre, que es lo que Windows espera de este enganche. No se hace nada
    /// mas: cuanto menos codigo corra dentro de una llamada de vuelta del sistema, mejor.
    /// </remarks>
    private static int AlAbrirseElCuadroDeCarpeta(nint ventana, int mensaje, nint parametro, nint dato)
    {
        if (mensaje == CuadroYaAbierto && dato != nint.Zero)
            SendMessageW(ventana, PonerLaSeleccion, 1, dato);

        return 0;
    }

    // ---- lo que devuelve el cuadro de varios --------------------------------

    /// <summary>
    /// Convierte en rutas lo que deja <c>comdlg32</c> cuando se eligen varios archivos.
    /// </summary>
    /// <remarks>
    /// <para>El formato lo fija Windows y tiene dos formas segun cuantos se elijan: con UNO,
    /// un solo trozo que ya es la ruta entera; con VARIOS, el primer trozo es la carpeta y los
    /// demas son los nombres sueltos. Confundir las dos formas deja la lista vacia justo
    /// cuando se elige un archivo, que es el caso mas comun.</para>
    /// <para>Es publica y separada del <c>P/Invoke</c> para poder probarla sin abrir ninguna
    /// ventana: lo que se puede equivocar aqui es el pegado, no la llamada a Windows.</para>
    /// </remarks>
    /// <param name="trozos">Los pedazos separados por ceros que dejo el cuadro.</param>
    public static IReadOnlyList<string> RutasDeLaSeleccion(IReadOnlyList<string> trozos)
    {
        ArgumentNullException.ThrowIfNull(trozos);

        if (trozos.Count == 0) return [];
        if (trozos.Count == 1) return [trozos[0]];

        var carpeta = trozos[0];
        return [.. trozos.Skip(1).Select(nombre => Path.Combine(carpeta, nombre))];
    }

    // ---- la llamada a Windows -----------------------------------------------

    /// <summary>Arma la estructura, llama a Windows y devuelve los trozos que dejo.</summary>
    /// <remarks>
    /// Los punteros de texto se reservan a mano y se sueltan SIEMPRE en el <c>finally</c>: son
    /// memoria nativa, y el recolector de .NET no la mira.
    /// </remarks>
    private static string[]? Pedir(
        nint ventana, string titulo, string nombrePropuesto, string descripcionDelTipo,
        string extension, string? carpetaDeSalida, int banderas, bool guardar, int largoDelHueco)
    {
        // El filtro de comdlg32 son parejas terminadas en nulo, y la lista acaba en dos nulos.
        var filtro = $"{descripcionDelTipo} (*{extension})\0*{extension}\0Todos los archivos\0*.*\0\0";

        var hueco = Marshal.AllocHGlobal(largoDelHueco * sizeof(char));
        var punteroDelFiltro = Marshal.StringToHGlobalUni(filtro);
        var punteroDelTitulo = Marshal.StringToHGlobalUni(titulo);
        var punteroDeLaExtension = Marshal.StringToHGlobalUni(extension.TrimStart('.'));
        var punteroDeLaCarpeta = string.IsNullOrWhiteSpace(carpetaDeSalida)
            ? nint.Zero
            : Marshal.StringToHGlobalUni(carpetaDeSalida);

        try
        {
            EscribirEnElHueco(hueco, nombrePropuesto, largoDelHueco);

            var datos = new NombreDeArchivo
            {
                TamanoDeLaEstructura = Marshal.SizeOf<NombreDeArchivo>(),
                Ventana = ventana,
                Filtro = punteroDelFiltro,
                IndiceDelFiltro = 1,
                Archivo = hueco,
                LargoDelArchivo = largoDelHueco,
                CarpetaInicial = punteroDeLaCarpeta,
                Titulo = punteroDelTitulo,
                Banderas = banderas,
                ExtensionPorDefecto = punteroDeLaExtension,
            };

            var eligio = guardar ? GetSaveFileNameW(ref datos) : GetOpenFileNameW(ref datos);
            return eligio ? TrozosDelHueco(hueco, largoDelHueco) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(hueco);
            Marshal.FreeHGlobal(punteroDelFiltro);
            Marshal.FreeHGlobal(punteroDelTitulo);
            Marshal.FreeHGlobal(punteroDeLaExtension);
            if (punteroDeLaCarpeta != nint.Zero) Marshal.FreeHGlobal(punteroDeLaCarpeta);
        }
    }

    /// <summary>Deja el nombre propuesto dentro del hueco, con el resto a ceros.</summary>
    /// <remarks>
    /// Se copia el hueco ENTERO y no solo el nombre: <c>AllocHGlobal</c> no limpia lo que
    /// reserva, y si detras del nombre quedara basura, Windows leeria hasta el primer cero que
    /// encontrase por casualidad.
    /// </remarks>
    private static void EscribirEnElHueco(nint hueco, string texto, int largoDelHueco)
    {
        var recortado = texto.Length > largoDelHueco - 1 ? texto[..(largoDelHueco - 1)] : texto;
        var letras = new char[largoDelHueco];
        recortado.CopyTo(0, letras, 0, recortado.Length);
        Marshal.Copy(letras, 0, hueco, letras.Length);
    }

    /// <summary>Saca del hueco los pedazos separados por ceros, hasta el cero doble.</summary>
    private static string[] TrozosDelHueco(nint hueco, int largoDelHueco)
    {
        var letras = new char[largoDelHueco];
        Marshal.Copy(hueco, letras, 0, largoDelHueco);

        var trozos = new List<string>();
        var desde = 0;
        for (var i = 0; i < largoDelHueco; i++)
        {
            if (letras[i] != '\0') continue;
            if (i == desde) break; // Dos ceros seguidos: se acabo la lista.
            trozos.Add(new string(letras, desde, i - desde));
            desde = i + 1;
        }

        return [.. trozos];
    }

    /// <summary>
    /// La estructura <c>OPENFILENAMEW</c> de <c>comdlg32</c>, campo por campo y en su orden.
    /// </summary>
    /// <remarks>
    /// El orden NO se puede tocar: es memoria que lee Windows. Los dos campos de 16 bits van
    /// juntos a proposito, porque asi estan en la estructura de C.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NombreDeArchivo
    {
        public int TamanoDeLaEstructura;
        public nint Ventana;
        public nint Instancia;
        public nint Filtro;
        public nint FiltroAMedida;
        public int LargoDelFiltroAMedida;
        public int IndiceDelFiltro;
        public nint Archivo;
        public int LargoDelArchivo;
        public nint SoloElNombre;
        public int LargoDeSoloElNombre;
        public nint CarpetaInicial;
        public nint Titulo;
        public int Banderas;
        public short DondeEmpiezaElNombre;
        public short DondeEmpiezaLaExtension;
        public nint ExtensionPorDefecto;
        public nint DatoDeQuienLlama;
        public nint Enganche;
        public nint Plantilla;
        public nint Reservado;
        public int TambienReservado;
        public int BanderasDeMas;
    }

    /// <summary>La estructura <c>BROWSEINFOW</c> de <c>shell32</c>, en su orden.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DatosDeLaCarpeta
    {
        public nint Ventana;
        public nint CarpetaRaiz;
        public nint NombreQueSeVe;
        public nint Titulo;
        public int Banderas;
        public nint Enganche;
        public nint DatoDeQuienLlama;
        public int Icono;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSaveFileNameW(ref NombreDeArchivo datos);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileNameW(ref NombreDeArchivo datos);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHBrowseForFolderW(ref DatosDeLaCarpeta datos);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SHGetPathFromIDListW(nint identificador, nint hueco);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(nint memoria);

    /// <summary>Para decirle al cuadro de la carpeta por donde empezar, y para nada mas.</summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessageW(nint ventana, int mensaje, nint parametro, nint dato);
}
