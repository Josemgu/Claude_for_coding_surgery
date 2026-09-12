using Fichas.App.Cascara;
using Fichas.App.Revisar;
using Fichas.Contratos.Modelos;
using Fichas.Reportes.Reglas;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Importar;

/// <summary>
/// La mitad de la pantalla de importar que se ocupa de lo que queda DESPUES de la tanda:
/// corregir las carpetas que se veran en Revisar, y borrar lo que entro sin nadie dentro.
/// </summary>
/// <remarks>
/// <para>Va en su propio archivo por el mismo reparto que <c>PaginaDeRevisar.Carpetas.cs</c>
/// y <c>GuardadoDeHojas.Filas.cs</c>: la otra mitad es la tanda —elegir, leer, la barra y el
/// resumen— y esto es lo que el dueno pidio el 2026-09-07 en sus peticiones 6 y la que
/// repitio dos veces.</para>
///
/// <para>⛔ Aqui no hay ninguna regla: agrupar y corregir es
/// <see cref="CarpetasDeLaTanda"/>, decidir que entro sin nadie es
/// <see cref="LoQueEntroSinInformacion"/>, y borrar es
/// <see cref="OperacionDeBorrar"/> —el unico camino del programa autorizado a borrar, el
/// mismo que usan Revisar y Asignar—. Los tres se prueban sin abrir ninguna ventana.</para>
/// </remarks>
public sealed partial class PaginaDeImportar
{
    /// <summary>El resumen de la última tanda importada, de donde salen los casos y las rutas; nulo hasta que acabe una.</summary>
    private ResumenDeLaTanda? _ultimaTanda;
    /// <summary>El camino único de borrar documentos, el mismo de Revisar y Asignar; se crea al pintar la primera vez.</summary>
    private OperacionDeBorrar? _borrar;
    /// <summary>El borrado de renglones de PDF ilegibles; nulo mientras no haya base de verdad (con «--falso» se queda apagado).</summary>
    private OperacionDeBorrarLosPdfIlegibles? _borrarLosPdfIlegibles;
    /// <summary>El cerrojo contra dos cuadros sobre la misma raíz visual, que tumban la ventana.</summary>
    private bool _hayUnCuadroAbierto;

    /// <summary>
    /// Rehace las dos zonas con lo que acaba de entrar. Si no entro nada, se esconden.
    /// </summary>
    /// <remarks>
    /// Se rehacen enteras y no se van tocando: despues de borrar, una lista que conservara
    /// renglones de documentos que ya no estan dejaria pulsar «borrar» sobre un caso muerto.
    /// </remarks>
    /// <param name="servicios">Los puertos de la ventana, ya montados.</param>
    private void PintarLoDeDespuesDeLaTanda(Cascara.Servicios servicios)
    {
        _borrar ??= new OperacionDeBorrar(servicios.Mantenimiento, servicios.Avisos, servicios.Registro);

        // Va atado a que HAYA base de verdad, igual que el otro: con «--falso» el puerto
        // falso devuelve un plan sin permiso —no hay base que copiar— y el boton se apaga
        // en vez de prometer un borrado que no puede hacer.
        if (_borrarLosPdfIlegibles is null && servicios.Mantenimiento is not null)
        {
            _borrarLosPdfIlegibles = new OperacionDeBorrarLosPdfIlegibles(
                servicios.Ilegibles, servicios.Avisos, servicios.Registro);
        }

        PintarLasCarpetas(servicios);
        PintarLoQueNoTraeNadie(servicios);
    }

    /// <summary>Las carpetas que la tanda va a formar en Revisar, con su cifra.</summary>
    /// <remarks>
    /// Es la petición 6 del dueño del 2026-09-07: editar en Importar las carpetas que se
    /// verán en Revisar, «porque a veces el sistema no pone los nombres de manera correcta».
    /// </remarks>
    /// <param name="servicios">Por donde se releen los casos de la tanda.</param>
    private void PintarLasCarpetas(Cascara.Servicios servicios)
    {
        var carpetas = CarpetasDeLaTanda.Componer(CasosDeLaTanda(servicios));

        _listaDeCarpetas.ItemsSource = carpetas;
        _listaDeCarpetas.SelectedItem = null;
        _corrector.Visibility = Visibility.Collapsed;
        _zonaDeLasCarpetas.Visibility = carpetas.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        _lineaDeLasCarpetas.Text = carpetas.Count == 0
            ? string.Empty
            : $"{Plural.Con(carpetas.Count, "carpeta", "carpetas")}. "
              + "Elija una para corregir su fecha o su unidad si el nombre no quedó bien: "
              + "en Revisar se llamará como quede aquí.";
    }

    /// <summary>Lo que entro sin ninguna persona, en dos listas separadas.</summary>
    /// <param name="servicios">Por donde se cuentan las personas y se leen los renglones de ilegibles.</param>
    private void PintarLoQueNoTraeNadie(Cascara.Servicios servicios)
    {
        var sinInformacion = LoQueEntroSinInformacion.Ver(
            servicios.Casos,
            servicios.Ilegibles,
            _ultimaTanda?.CasosDeLaTanda ?? [],
            _ultimaTanda?.RutasDeLaTanda ?? []);

        _listaSinInformacion.ItemsSource = sinInformacion.CasosSinPersonas;
        _listaSinInformacion.Visibility =
            sinInformacion.CasosSinPersonas.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _botonDeBorrarLoVacio.Visibility = _listaSinInformacion.Visibility;
        _botonDeBorrarLoVacio.IsEnabled = _borrar?.SePuedeBorrarAqui == true;

        _lineaSinInformacion.Text = sinInformacion.Linea;
        _zonaSinInformacion.Visibility = sinInformacion.HayAlgo ? Visibility.Visible : Visibility.Collapsed;

        PintarLosPdfQueNoDejaronNada(sinInformacion);
    }

    /// <summary>
    /// Los PDF que no se pudieron leer y no dejaron documento, con su ruta, su motivo y su
    /// boton de borrar.
    /// </summary>
    /// <remarks>
    /// <para>⚠️ <b>Primero verlos, despues poder borrarlos.</b> Hasta el 2026-09-09 estos
    /// renglones no se veian en NINGUNA pantalla —medido ese dia: <c>IIlegibles.Listar</c>
    /// y <c>Contar</c> no tenian ni un consumidor fuera de las pruebas— y el resumen de la
    /// tanda remitia a una pantalla que para ellos no existia. Un boton de borrar sobre una
    /// lista que nadie ve no habria servido de nada.</para>
    ///
    /// <para>Se enseña la ruta ENTERA y no solo el nombre del archivo: es lo que hace falta
    /// para ir a abrir el papel y mirarlo antes de decidir.</para>
    ///
    /// <para>⛔ Y se dice, antes de marcar nada, que el PDF del disco se queda:
    /// <see cref="TextosDeBorrarLosPdfIlegibles.LoQueNoSeBorra"/>.</para>
    /// </remarks>
    /// <param name="sinInformacion">Las dos listas ya calculadas; aquí solo se usa la de renglones sin caso.</param>
    private void PintarLosPdfQueNoDejaronNada(LoQueNoTraeAnadie sinInformacion)
    {
        _listaDeLosPdfIlegibles.ItemsSource = sinInformacion.RenglonesSinCaso;
        _listaDeLosPdfIlegibles.SelectedItem = null;

        _zonaDeLosPdfIlegibles.Visibility =
            sinInformacion.RenglonesSinCaso.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (sinInformacion.RenglonesSinCaso.Count == 0) return;

        _lineaDeLosPdfIlegibles.Text = Plural.Con(
            sinInformacion.RenglonesSinCaso.Count,
            "PDF no se pudo leer y no dejó ningún documento detrás",
            "PDF no se pudieron leer y no dejaron ningún documento detrás")
            + ". Marque los que quiera quitar de la lista:";

        _loQueNoSeBorraDeLosPdf.Text = TextosDeBorrarLosPdfIlegibles.LoQueNoSeBorra;
        _botonDeBorrarLosPdfIlegibles.IsEnabled = _borrarLosPdfIlegibles is not null;
    }

    /// <summary>Los casos de la tanda, leidos de la base para tener lo que hay AHORA.</summary>
    /// <remarks>
    /// Se releen en vez de guardarlos al importar porque entre la tanda y este momento el
    /// dueno ya puede haber corregido una carpeta: la lista tiene que decir lo que hay, no
    /// lo que habia.
    /// </remarks>
    /// <param name="servicios">Por donde se lee cada caso.</param>
    /// <returns>Los que siguen en la base; vacía si no hubo tanda o ya se borraron todos.</returns>
    private IReadOnlyList<Caso> CasosDeLaTanda(Cascara.Servicios servicios)
        => [.. (_ultimaTanda?.CasosDeLaTanda ?? [])
                .Select(servicios.Casos.Obtener)
                .Where(caso => caso is not null)
                .Select(caso => caso!)];

    /// <summary>Llena las tres cajas con lo que la carpeta elegida tiene hoy.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlElegirUnaCarpeta(object quien, SelectionChangedEventArgs cuando)
        => SinTragarseNada("Elegir una carpeta", () =>
        {
            if (_listaDeCarpetas.SelectedItem is not CarpetaDeLaTanda carpeta)
            {
                _corrector.Visibility = Visibility.Collapsed;
                return;
            }

            _carpetaElegida.Text =
                $"{carpeta.Ruta} · {Plural.Con(carpeta.CuantosDocumentos, "documento", "documentos")}";
            _fechaDeLaCarpeta.Date = FechaDelCalendario.Leer(carpeta.FechaDeViaje);
            _numeroDeLaUnidad.Text = carpeta.UnidadNumero;
            _nombreDeLaUnidad.Text = carpeta.UnidadNombre;
            _corrector.Visibility = Visibility.Visible;
        });

    /// <summary>Quita la fecha del calendario; la carpeta pasa a la de «sin fecha de viaje».</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlQuitarLaFechaDeLaCarpeta(object quien, RoutedEventArgs cuando)
        => SinTragarseNada("Dejarla sin fecha", () => _fechaDeLaCarpeta.Date = null);

    /// <summary>Escribe la fecha y la unidad en todos los documentos de la carpeta elegida.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlCorregirLaCarpeta(object quien, RoutedEventArgs cuando)
        => SinTragarseNada("Guardar el nombre de la carpeta", () =>
        {
            var servicios = Servicios;
            if (servicios is null || _listaDeCarpetas.SelectedItem is not CarpetaDeLaTanda carpeta) return;

            var resultado = CarpetasDeLaTanda.Corregir(
                servicios.Casos,
                carpeta,
                FechaDelCalendario.Escribir(_fechaDeLaCarpeta.Date),
                _numeroDeLaUnidad.Text,
                _nombreDeLaUnidad.Text);

            Decir(servicios, resultado, carpeta);
            if (resultado.SeEscribio) PintarLoDeDespuesDeLaTanda(servicios);
        });

    /// <summary>Deja en la franja, en el pie y en el cuaderno lo que paso al corregir.</summary>
    /// <param name="servicios">Por donde se llega a la franja y al cuaderno.</param>
    /// <param name="resultado">Lo que devolvió <see cref="CarpetasDeLaTanda.Corregir"/>.</param>
    /// <param name="carpeta">La carpeta que se corrigió, para nombrarla en la línea.</param>
    private static void Decir(
        Cascara.Servicios servicios, ResultadoDeLaCorreccion resultado, CarpetaDeLaTanda carpeta)
    {
        if (!resultado.SeEscribio)
        {
            servicios.Avisos.Dejar(resultado.Avisos);
            var motivo = resultado.Avisos.Count == 0
                ? "No se cambió nada."
                : resultado.Avisos[0].Linea;
            (App.Ventana as VentanaPrincipal)?.AcuseDelPie.Decir(motivo);
            servicios.Registro.Anotar("CARPETAS  no se escribió nada: " + motivo);
            return;
        }

        var linea =
            $"«{carpeta.Ruta}»: corregidos {Plural.Con(resultado.Documentos, "documento", "documentos")}.";

        servicios.Avisos.Dejar(Aviso.Informa(linea, string.Empty, resultado.Avisos.Count == 0
            ? "En Revisar aparecerán con la carpeta ya corregida."
            : string.Join(" ", resultado.Avisos.Select(aviso => aviso.Linea))));
        (App.Ventana as VentanaPrincipal)?.AcuseDelPie.Decir(linea);
        servicios.Registro.Anotar("CARPETAS  " + linea);
    }

    /// <summary>
    /// Borra los documentos marcados que no traen a nadie, por el camino de siempre.
    /// </summary>
    /// <remarks>
    /// El cerrojo no es un adorno: dos pulsaciones seguidas levantan dos cuadros sobre la
    /// misma raiz visual y eso tumba la ventana. Es lo mismo que hace Revisar.
    /// </remarks>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private async void AlBorrarLoQueNoTraeNadie(object quien, RoutedEventArgs cuando)
        => await SinTragarseNadaAsync("Borrar los marcados", async () =>
        {
            var servicios = Servicios;
            if (servicios is null || _borrar is null || _hayUnCuadroAbierto) return;

            var marcados = _listaSinInformacion.SelectedItems
                .OfType<DocumentoSinNadie>()
                .Select(documento => documento.CasoId)
                .ToArray();
            if (marcados.Length == 0) return;

            _hayUnCuadroAbierto = true;
            try
            {
                var linea = await _borrar
                    .PreguntarYBorrar(mantenimiento => mantenimiento.PlanearDocumentos(marcados), XamlRoot)
                    .ConfigureAwait(true);
                if (linea is not null) (App.Ventana as VentanaPrincipal)?.AcuseDelPie.Decir(linea);
            }
            finally
            {
                _hayUnCuadroAbierto = false;
            }

            PintarLoDeDespuesDeLaTanda(servicios);
        });

    /// <summary>
    /// Borra los renglones marcados de los PDF que no se pudieron leer.
    /// </summary>
    /// <remarks>
    /// El mismo cerrojo que el borrado de arriba, y por el mismo motivo: dos cuadros sobre
    /// la misma raíz visual tumban la ventana. Aquí importa más todavía, porque ahora hay
    /// dos botones en la misma zona que pueden abrir uno.
    /// </remarks>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private async void AlBorrarLosPdfIlegibles(object quien, RoutedEventArgs cuando)
        => await SinTragarseNadaAsync("Borrar los PDF que no se pudieron leer", async () =>
        {
            var servicios = Servicios;
            if (servicios is null || _borrarLosPdfIlegibles is null || _hayUnCuadroAbierto) return;

            var marcados = _listaDeLosPdfIlegibles.SelectedItems
                .OfType<PdfSinDocumento>()
                .Select(renglon => renglon.RenglonId)
                .ToArray();
            if (marcados.Length == 0) return;

            _hayUnCuadroAbierto = true;
            try
            {
                var linea = await _borrarLosPdfIlegibles
                    .PreguntarYBorrar(marcados, XamlRoot)
                    .ConfigureAwait(true);
                if (linea is not null) (App.Ventana as VentanaPrincipal)?.AcuseDelPie.Decir(linea);
            }
            finally
            {
                _hayUnCuadroAbierto = false;
            }

            PintarLoDeDespuesDeLaTanda(servicios);
        });
}
