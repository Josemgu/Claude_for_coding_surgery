using System.Net;
using Fichas.App.Actualizacion;
using Fichas.App.Cascara;

namespace Fichas.Pruebas.App.Actualizacion;

/// <summary>
/// Buscar si hay una versión nueva: qué contesta el programa a cada cosa que GitHub puede
/// decir, y cuándo NO pregunta.
/// </summary>
/// <remarks>
/// <para><b>De dónde sale el criterio.</b> DECISIONES.md, 2026-09-11, «EL DUEÑO PIDE QUE EL
/// PROGRAMA SE ACTUALICE SOLO», y el pase del supervisor del mismo día:</para>
/// <list type="bullet">
/// <item>etiqueta mayor → hay actualización; igual o menor → no;</item>
/// <item>404 sin clave → aviso de la clave y dónde va; 401/403 con clave → la clave no vale;</item>
/// <item>sin red o tiempo agotado → nada en pantalla, una línea en el registro;</item>
/// <item><c>--falso</c> y <c>--sin-actualizacion</c> → cero llamadas al servidor;</item>
/// <item>la clave nunca aparece en el registro.</item>
/// </list>
/// <para>Sin red en las pruebas: GitHub es <see cref="ServidorDeMentirijilla"/>, que además
/// cuenta las peticiones, y ese contador es el que hace medibles los «cero».</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeBuscarActualizacion
{
    /// <summary>El montaje de cada prueba; se tira al terminar.</summary>
    private MontajeDelActualizador _montaje = null!;

    /// <summary>Un montaje limpio por prueba.</summary>
    [TestInitialize]
    public void Preparar() => _montaje = new MontajeDelActualizador();

    /// <summary>Borra las carpetas de la prueba.</summary>
    [TestCleanup]
    public void Recoger() => _montaje.Dispose();

    // ---- lo que contesta GitHub -------------------------------------------------

    /// <summary>Dado un Release «v12» y el programa en 11, hay versión nueva y se sabe qué instalador bajar.</summary>
    [TestMethod]
    public async Task UnaEtiquetaMayorEsUnaActualizacion()
    {
        var url = _montaje.ConElUltimoRelease("v12", [1, 2, 3]);

        var resultado = await _montaje.Montar("11").Buscar();

        Assert.AreEqual(QueSeEncontro.HayVersionNueva, resultado.Que, resultado.Motivo);
        Assert.AreEqual("v12", resultado.Etiqueta);
        Assert.IsNotNull(resultado.Instalador);
        Assert.AreEqual("Instalar-Fichas-v12.exe", resultado.Instalador.Nombre);
        Assert.AreEqual(url, resultado.Instalador.Url, "Se baja por la url de la API, no por browser_download_url.");
        Assert.AreEqual(3, resultado.Instalador.Tamano);
        Assert.AreEqual(MontajeDelActualizador.HuellaDe([1, 2, 3]), resultado.Instalador.Huella);
    }

    /// <summary>Dado un Release igual o menor que la versión abierta, no hay nada nuevo y se dice cuál es la última.</summary>
    [TestMethod]
    [DataRow("v11", "11")]
    [DataRow("v10", "11")]
    public async Task UnaEtiquetaIgualOMenorNoEsNada(string etiqueta, string actual)
    {
        _montaje.ConElUltimoRelease(etiqueta, [1, 2, 3]);

        var resultado = await _montaje.Montar(actual).Buscar();

        Assert.AreEqual(QueSeEncontro.EsLaUltima, resultado.Que, resultado.Motivo);
        Assert.AreEqual(etiqueta, resultado.Etiqueta);
        Assert.IsNull(resultado.Instalador);
    }

    /// <summary>Dado un Release nuevo que no trae instalador, no se puede actualizar y se dice por qué.</summary>
    [TestMethod]
    public async Task UnReleaseSinInstaladorNoSePuedeInstalar()
    {
        _montaje.Servidor.AlPedir(
            MontajeDelActualizador.LaDireccionDelUltimoRelease,
            HttpStatusCode.OK,
            MontajeDelActualizador.ReleaseComoLoDaGitHub("v12", ("Fichas-v12.zip", "https://api.github.com/x/1", 10, null)));

        var resultado = await _montaje.Montar("11").Buscar();

        Assert.AreEqual(QueSeEncontro.NoSePudo, resultado.Que);
        Assert.Contains("instalador", resultado.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Dado un 404 sin clave, lo que falta es la clave, y se dice la ruta completa donde pegarla.</summary>
    /// <remarks>Medido por el supervisor: con repositorio privado y sin cabecera, GitHub contesta 404.</remarks>
    [TestMethod]
    public async Task Un404SinClaveEsQueFaltaLaClave()
    {
        var resultado = await _montaje.Montar("11").Buscar();

        Assert.AreEqual(QueSeEncontro.FaltaLaClave, resultado.Que);
        Assert.AreEqual(ClaveDeActualizacion.RutaEn(_montaje.CarpetaDeDatos), resultado.RutaDeLaClave);
        Assert.AreEqual(1, _montaje.Servidor.CuantasVecesSeLlamo);
        Assert.IsNull(_montaje.Servidor.Peticiones[0].Autorizacion, "Sin archivo de clave no se manda cabecera.");
    }

    /// <summary>Dado un 404 CON clave, no es que falte: la clave no abre ese repositorio, y se dice.</summary>
    /// <remarks>GitHub contesta 404 y no 403 a una clave que existe pero no tiene permiso sobre el repositorio.</remarks>
    [TestMethod]
    public async Task Un404ConClaveEsQueLaClaveNoVale()
    {
        _montaje.ConLaClavePegada();

        var resultado = await _montaje.Montar("11").Buscar();

        Assert.AreEqual(QueSeEncontro.LaClaveNoVale, resultado.Que);
    }

    /// <summary>Dado un 401 o un 403 con clave, la clave no vale.</summary>
    [TestMethod]
    [DataRow(HttpStatusCode.Unauthorized)]
    [DataRow(HttpStatusCode.Forbidden)]
    public async Task Un401OUn403ConClaveEsQueLaClaveNoVale(HttpStatusCode codigo)
    {
        _montaje.ConLaClavePegada();
        _montaje.Servidor.AlPedir(MontajeDelActualizador.LaDireccionDelUltimoRelease, codigo, "{\"message\":\"Bad credentials\"}");

        var resultado = await _montaje.Montar("11").Buscar();

        Assert.AreEqual(QueSeEncontro.LaClaveNoVale, resultado.Que);
    }

    /// <summary>Dado un 401 o un 403 SIN clave, no es que la clave no valga (no hay clave): es que no se pudo, y se dice el código.</summary>
    /// <remarks>GitHub contesta 403 sin clave cuando falta el User-Agent o se pasó el límite de peticiones; decir «la clave no vale» mandaría al dueño a cambiar una clave que no existe.</remarks>
    /// <param name="codigo">El código que contesta GitHub.</param>
    [TestMethod]
    [DataRow(HttpStatusCode.Unauthorized)]
    [DataRow(HttpStatusCode.Forbidden)]
    public async Task Un401OUn403SinClaveEsNoSePudo(HttpStatusCode codigo)
    {
        _montaje.Servidor.AlPedir(MontajeDelActualizador.LaDireccionDelUltimoRelease, codigo, "{\"message\":\"rate limit\"}");

        var resultado = await _montaje.Montar("11").Buscar();

        Assert.AreEqual(QueSeEncontro.NoSePudo, resultado.Que);
        Assert.Contains(((int)codigo).ToString(System.Globalization.CultureInfo.InvariantCulture), resultado.Motivo);
    }

    /// <summary>Dado que no hay red, no se dice nada en pantalla: solo una línea en el registro.</summary>
    [TestMethod]
    public async Task SinRedNoHayAvisoPeroSiUnaLineaEnElRegistro()
    {
        _montaje.Servidor.AlPedirFallaLaRed(MontajeDelActualizador.LaDireccionDelUltimoRelease, "No such host is known");

        var resultado = await _montaje.Montar("11").Buscar();

        Assert.AreEqual(QueSeEncontro.SinRed, resultado.Que);
        Assert.IsTrue(
            _montaje.Cuaderno.Any(l => l.Contains("ACTUALIZACION", StringComparison.Ordinal) && l.Contains("sin red", StringComparison.OrdinalIgnoreCase)),
            "Falta la línea del registro. Cuaderno: " + string.Join(" | ", _montaje.Cuaderno));
    }

    /// <summary>Dado que GitHub no contesta, el programa se rinde a los 10 segundos (aquí, a 200 ms) y lo trata como sin red.</summary>
    [TestMethod]
    public async Task SiNoContestaSeRindeYEsComoSinRed()
    {
        _montaje.Servidor.AlPedirNoContestaNunca(MontajeDelActualizador.LaDireccionDelUltimoRelease);

        var resultado = await _montaje.Montar("11", TimeSpan.FromMilliseconds(200)).Buscar();

        Assert.AreEqual(QueSeEncontro.SinRed, resultado.Que);
        Assert.Contains("tiempo", resultado.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>El tiempo máximo de espera de la consulta es corto: 10 segundos.</summary>
    [TestMethod]
    public void ElTiempoMaximoDeConsultaEsDeDiezSegundos()
        => Assert.AreEqual(TimeSpan.FromSeconds(10), Actualizador.TiempoMaximoDeConsultaPorDefecto);

    /// <summary>Dado un 500 o un JSON que no se entiende, no se pudo y se dice el motivo.</summary>
    [TestMethod]
    public async Task UnaRespuestaRaraEsNoSePudo()
    {
        _montaje.Servidor.AlPedir(MontajeDelActualizador.LaDireccionDelUltimoRelease, HttpStatusCode.OK, "esto no es JSON");

        var resultado = await _montaje.Montar("11").Buscar();

        Assert.AreEqual(QueSeEncontro.NoSePudo, resultado.Que);
        Assert.IsNotEmpty(resultado.Motivo);
    }

    // ---- la clave -----------------------------------------------------------------

    /// <summary>Dada la clave pegada, se manda como «Authorization: Bearer»; sin ella, no se manda nada.</summary>
    [TestMethod]
    public async Task LaClaveSeMandaComoBearerSoloSiEsta()
    {
        _montaje.ConElUltimoRelease("v12", [1]);
        await _montaje.Montar("11").Buscar();
        Assert.IsNull(_montaje.Servidor.Peticiones[0].Autorizacion);

        _montaje.ConLaClavePegada();
        await _montaje.Montar("11").Buscar();
        Assert.AreEqual("Bearer " + MontajeDelActualizador.LaClaveInventada, _montaje.Servidor.Peticiones[1].Autorizacion);
    }

    /// <summary>
    /// Dada una clave inventada y un fallo de red cuyo texto la contiene, ni el registro ni el
    /// resultado la enseñan.
    /// </summary>
    /// <remarks>
    /// Es la prueba que pide el pase: «el registro escrito con una clave falsa no la contiene».
    /// El fallo se fabrica adrede con la clave dentro, que es el peor caso: si el programa
    /// copiara el mensaje tal cual, la clave acabaría en <c>fichas.log</c>.
    /// </remarks>
    [TestMethod]
    public async Task LaClaveNoAcabaEnElRegistroNiEnElResultado()
    {
        _montaje.ConLaClavePegada();
        _montaje.Servidor.AlPedirFallaLaRed(
            MontajeDelActualizador.LaDireccionDelUltimoRelease,
            $"fallo con Authorization: Bearer {MontajeDelActualizador.LaClaveInventada}");

        var resultado = await _montaje.Montar("11").Buscar();

        var todoLoEscrito = string.Join(Environment.NewLine, _montaje.Cuaderno);
        Console.WriteLine($"Líneas en el cuaderno: {_montaje.Cuaderno.Count}. Motivo: «{resultado.Motivo}»");
        Assert.IsNotEmpty(_montaje.Cuaderno, "Un cuaderno vacío no demuestra nada.");
        Assert.DoesNotContain(MontajeDelActualizador.LaClaveInventada, todoLoEscrito);
        Assert.DoesNotContain(MontajeDelActualizador.LaClaveInventada, resultado.Motivo);
    }

    // ---- cuándo NO se pregunta ----------------------------------------------------

    /// <summary>Dado <c>--falso</c>, al arrancar no se sale a internet: cero llamadas.</summary>
    /// <remarks>Los agentes arrancan el programa cien veces al día con <c>--falso</c>.</remarks>
    [TestMethod]
    public async Task ConFalsoNoSePreguntaAlArrancar()
    {
        _montaje.ConElUltimoRelease("v12", [1]);
        var argumentos = ArgumentosDeArranque.Leer(["--falso", "0", "--carpeta-de-datos", _montaje.CarpetaDeDatos]);

        var resultado = await _montaje.Montar("11").BuscarAlArrancar(argumentos);

        Assert.AreEqual(QueSeEncontro.NoSeBusco, resultado.Que);
        Assert.AreEqual(0, _montaje.Servidor.CuantasVecesSeLlamo);
    }

    /// <summary>Dado <c>--sin-actualizacion</c>, al arrancar tampoco: cero llamadas.</summary>
    [TestMethod]
    public async Task ConSinActualizacionNoSePreguntaAlArrancar()
    {
        _montaje.ConElUltimoRelease("v12", [1]);
        var argumentos = ArgumentosDeArranque.Leer(["--sin-actualizacion", "--carpeta-de-datos", _montaje.CarpetaDeDatos]);

        var resultado = await _montaje.Montar("11").BuscarAlArrancar(argumentos);

        Assert.AreEqual(QueSeEncontro.NoSeBusco, resultado.Que);
        Assert.AreEqual(0, _montaje.Servidor.CuantasVecesSeLlamo);
    }

    /// <summary>Dado un arranque normal, sí se pregunta: una llamada.</summary>
    [TestMethod]
    public async Task EnUnArranqueNormalSePreguntaUnaVez()
    {
        _montaje.ConElUltimoRelease("v12", [1]);
        var argumentos = ArgumentosDeArranque.Leer(["--carpeta-de-datos", _montaje.CarpetaDeDatos]);

        var resultado = await _montaje.Montar("11").BuscarAlArrancar(argumentos);

        Assert.AreEqual(QueSeEncontro.HayVersionNueva, resultado.Que);
        Assert.AreEqual(1, _montaje.Servidor.CuantasVecesSeLlamo);
    }
}
