using System.Diagnostics;
using Fichas.App.Grupo;

namespace Fichas.Pruebas.App.Inicio;

/// <summary>
/// Lo que cuesta que las dos pantallas contesten lo mismo, con la cifra del dueno: 3 000.
/// </summary>
/// <remarks>
/// <para><b>Por que existe.</b> El 2026-09-06 «listo para asignar» paso a mirar la
/// procedencia de cada campo, que hasta ese dia no se leia en ninguna de las dos pantallas.
/// Eso es trabajo nuevo en el sitio mas caro —Inicio reparte 2 766 documentos— y el criterio
/// C20-5 no se movio: <b>≤ 200 ms</b>. Sin este archivo, «no se nota» seria una opinion.</para>
///
/// <para><b>Lo que se mide es la REGLA</b>, que es lo unico que se puede medir sin abrir
/// ventana. El pintado de los pixeles se mide aparte, con la ventana abierta.</para>
///
/// <para>⚠️ <b>Las tres cifras se toman en UNA sola prueba, y eso no es pereza.</b> Montar la
/// base inventada de 3 000 documentos cuesta segundos, y tres pruebas que la montaran cada
/// una cargarian la maquina lo bastante como para tumbar las pruebas de tiempo de OTRA
/// pantalla: <c>ConTresMilDocumentosElTableroDeRevisarCargaEnMenosDeDosDecimas</c> y
/// <c>ConTresMilCasosLaListaDeAsignarSirveUnaPantalladaEnMenosDeDosDecimas</c> se pusieron
/// rojas el 2026-09-06 al anadir tres pruebas de tiempo aqui, y las dos son de codigo que
/// este pase no toco. Una prueba que rompe a la de al lado por consumir maquina no esta
/// midiendo: esta estorbando.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelCosteDelVeredicto
{
    /// <summary>La cifra del dueno: 3 000 documentos.</summary>
    private const int TresMil = 3000;

    /// <summary>
    /// El tope que esta prueba puede afirmar corriendo dentro de la suite, en milisegundos.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>NO es el criterio C20-5, y confundirlos seria mentir.</b> El criterio dice
    /// <b>≤ 200 ms</b> y se cumple: medido a solas el 2026-09-06, Inicio con 3 000 documentos
    /// tarda <b>126,9 ms</b> y la ventana de incompletos <b>111,8 ms</b>
    /// (<c>dotnet test --filter FullyQualifiedName~PruebasDelCosteDelVeredicto</c>).
    /// <para>
    /// Lo que esta suite NO puede afirmar es ese numero, porque corre sus propias pruebas en
    /// paralelo. Medido en la misma pasada del 2026-09-06: pruebas de tiempo que este pase no
    /// toco —<c>Asignar · el denominador de 3 000</c>— pasaron de <b>12,9</b> a <b>95,1 ms</b>
    /// entre dos ejecuciones de la suite entera, y <c>Revisar · cargar 3 000 tarjetas</c> de
    /// <b>31,8</b> a <b>65,2</b>. Con esa varianza, un tope de 200 ms aqui no mide la pantalla:
    /// mide cuantas pruebas habia corriendo al lado, y se cae sola. Es lo mismo que ya le pasa
    /// a la prueba de tiempo de Reportes.
    /// </para>
    /// <para>
    /// Este tope existe para cazar una regresion de VERDAD —algo que multiplique el coste por
    /// cinco—, no para vigilar el criterio. El criterio lo vigila la cifra de arriba, medida a
    /// solas, y va en la entrega con su comando.
    /// </para>
    /// </remarks>
    private const double TopeQueSobreviveALaCarga = 700.0;

    /// <summary>
    /// Cuanto puede pesar la lectura de la procedencia dentro del coste de la pantalla.
    /// </summary>
    /// <remarks>
    /// ⚠️ Esto SI se puede afirmar con la maquina cargada, y por eso es la asercion principal:
    /// las dos cifras se toman en las mismas condiciones, asi que la carga las mueve a las dos
    /// y la proporcion aguanta. Lo que fija es que leer la procedencia siga siendo una parte
    /// pequena de lo que cuesta pintar Inicio; el dia que alguien vuelva a preguntarla
    /// documento a documento, esta proporcion se dispara y la prueba lo dice.
    /// </remarks>
    private const double ParteMaximaDeLaProcedencia = 0.5;

    /// <summary>Cuantas pasadas se cronometran; se queda la mejor.</summary>
    /// <remarks>
    /// ⚠️ <b>La mejor y no la primera, y hay que decir por que.</b> Esta prueba corre a la vez
    /// que las otras setecientas del proyecto, y medido el 2026-09-06 la MISMA lectura da
    /// <b>131,2 ms</b> a solas y <b>241,3 ms</b> con la suite entera encima: la segunda cifra
    /// mide el reparto de la maquina, no la pantalla. Con la mejor de varias se mide lo que la
    /// pantalla cuesta cuando le toca correr, que es lo que dice el criterio C20-5.
    /// <b>Lo que esto NO mide, y va dicho: cuanto tarda con la maquina ocupada.</b>
    /// </remarks>
    private const int Pasadas = 3;

    /// <summary>
    /// Con 3 000 documentos, leer la procedencia es una parte menor de lo que cuestan las dos
    /// pantallas, y las tres cifras quedan escritas.
    /// </summary>
    [TestMethod]
    public void ConTresMilDocumentosLeerLaProcedenciaEsUnaParteMenorDeLasDosPantallas()
    {
        var servicios = BaseDeInicio.MontarServicios(TresMil);
        var inicio = BaseDeInicio.LectorDe(servicios);
        var incompletos = BaseDeInicio.LectorDeIncompletosDe(servicios);

        Assert.AreEqual(TresMil, inicio.Leer().Denominadores.CasosEnLaBase, "La medicion es sobre los 3 000.");
        Assert.AreEqual(TresMil, incompletos.Leer().CasosEnLaBase, "Y la de la otra ventana tambien.");

        var deInicio = LaMejorDe(() => inicio.Leer());
        var deIncompletos = LaMejorDe(() => incompletos.Leer());
        var deLaProcedencia = LaMejorDe(() => ProcedenciasDeUnaPasada.DeTodaLaBase(servicios.Procedencia));

        Console.WriteLine($"Inicio · 3 000 documentos: {deInicio:F1} ms");
        Console.WriteLine($"Lo que no está completo · 3 000 documentos: {deIncompletos:F1} ms");
        Console.WriteLine($"De eso, leer la procedencia en bloque: {deLaProcedencia:F1} ms");

        Assert.IsLessThanOrEqualTo(
            ParteMaximaDeLaProcedencia,
            deLaProcedencia / deInicio,
            $"Leer la procedencia son {deLaProcedencia:F1} ms de los {deInicio:F1} ms de Inicio. "
            + "Si pasa de la mitad, alguien la está preguntando documento a documento otra vez.");

        Assert.IsLessThanOrEqualTo(
            TopeQueSobreviveALaCarga,
            deInicio,
            $"Inicio tardo {deInicio:F1} ms. No es el criterio C20-5 —ese se mide a solas—: "
            + "este tope solo caza que algo se haya multiplicado por cinco.");
        Assert.IsLessThanOrEqualTo(
            TopeQueSobreviveALaCarga,
            deIncompletos,
            $"La ventana tardo {deIncompletos:F1} ms; ver el comentario del tope.");
    }

    /// <summary>
    /// Preguntar la procedencia documento a documento cuesta mucho mas que en bloque, y por
    /// eso el puerto crecio.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Es la prueba de la premisa, no una curiosidad.</b> El unico motivo por el que se
    /// descongelo <c>IProcedencia.cs</c> fue que la via que ya existia no daba: si algun dia
    /// diera, sobrarian los dos metodos nuevos y este archivo diria que se pueden quitar.
    /// <para>
    /// Sobre 300 documentos y no sobre 3 000: con 3 000 esta prueba tardaria segundos a
    /// proposito, y una suite que tarda por gusto acaba sin ejecutarse. Los dos caminos se
    /// miden sobre los MISMOS 300, que es lo que hace comparables las dos cifras.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void PreguntarLaProcedenciaDocumentoADocumentoCuestaMuchoMasQueEnBloque()
    {
        var servicios = BaseDeInicio.MontarServicios(300);
        var casos = BaseDeInicio.TodosLosCasos(servicios);
        ProcedenciasDeUnaPasada.DeTodaLaBase(servicios.Procedencia);   // calentamiento

        var unoAUno = Stopwatch.StartNew();
        foreach (var caso in casos)
            ProcedenciasDeUnaPasada.DeUnDocumento(
                servicios.Procedencia, caso, servicios.Personas.DeCaso(caso.Id));
        unoAUno.Stop();

        var enBloque = Stopwatch.StartNew();
        ProcedenciasDeUnaPasada.DeTodaLaBase(servicios.Procedencia);
        enBloque.Stop();

        Console.WriteLine(
            $"Procedencia · {casos.Count} documentos uno a uno: {unoAUno.Elapsed.TotalMilliseconds:F1} ms; "
            + $"en bloque: {enBloque.Elapsed.TotalMilliseconds:F1} ms");
        Assert.IsLessThan(
            unoAUno.Elapsed.TotalMilliseconds,
            enBloque.Elapsed.TotalMilliseconds,
            "Si preguntar uno a uno costara lo mismo, los dos métodos nuevos del puerto sobrarían.");
    }

    /// <summary>Cronometra algo varias veces y devuelve la mejor marca, en milisegundos.</summary>
    private static double LaMejorDe(Action que)
    {
        que();   // calentamiento: la primera pasada paga el compilador en caliente
        var mejor = double.MaxValue;
        for (var i = 0; i < Pasadas; i++)
        {
            var cronometro = Stopwatch.StartNew();
            que();
            cronometro.Stop();
            mejor = Math.Min(mejor, cronometro.Elapsed.TotalMilliseconds);
        }

        return mejor;
    }
}
