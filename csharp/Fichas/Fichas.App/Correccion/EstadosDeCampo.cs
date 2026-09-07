using System.Globalization;
using Fichas.Contratos.Modelos;

namespace Fichas.App.Correccion;

/// <summary>
/// En que estado esta un campo de la correccion. Portado de <c>interfaz/tema.py</c>.
/// </summary>
/// <remarks>
/// El quinto —<see cref="Tachado"/>— no es «no valido» y no es «vacio» sin mas: es un dato
/// que una persona marco como equivocado a proposito. Metido en cualquiera de los otros
/// cuatro cubos, Miguel pierde la unica pista de que el papel ya decia que estaba mal.
/// <para>
/// Y el sexto —<see cref="NoEstaEnElPapel"/>— existe por lo mismo, y lo dijo la migracion
/// 14 del Python al crear la columna: «sin ella, "no esta en el papel" se ve exactamente
/// igual que "el OCR no supo leerlo"». Uno pide teclear algo; el otro dice que no hay nada
/// que teclear.
/// </para>
/// </remarks>
public enum EstadoDeCampo
{
    /// <summary>Lo escribio una anotacion del propio PDF: dato exacto.</summary>
    Anotacion = 0,

    /// <summary>Lo leyo el OCR con confianza suficiente, o lo tecleo una mano.</summary>
    Ocr = 1,

    /// <summary>Hay que mirarlo: poca confianza, o el lector no leyo nada.</summary>
    Revisar = 2,

    /// <summary>No pasa su regla de formato. Se guarda igual y queda senalado.</summary>
    NoValido = 3,

    /// <summary>El papel llevaba un tachon encima y nadie escribio la correccion.</summary>
    Tachado = 4,

    /// <summary>Miguel marco que el formulario NO trae este campo: no hay dato que buscar.</summary>
    NoEstaEnElPapel = 5,
}

/// <summary>
/// Que estado le toca a un campo, con que palabra se dice y que se cuenta de el.
/// </summary>
/// <remarks>
/// Todo esto vive fuera de cualquier control de XAML a proposito: asi las frases que ve
/// Miguel se pueden leer en una prueba sin abrir una ventana, que es la unica forma de que
/// alguien las mire de verdad (<c>interfaz/acuse_de_guardado.py</c> dice lo mismo).
/// </remarks>
public static class EstadosDeCampo
{
    /// <summary>Por debajo de esta confianza el campo sale marcado para revisar.</summary>
    public const double UmbralDeConfianzaBaja = 0.6;

    /// <summary>
    /// Cual de los cinco estados le toca, mirando su procedencia y si lo escrito vale.
    /// </summary>
    /// <remarks>
    /// El orden importa y es este: primero lo que no valida, porque un valor mal formado hay
    /// que arreglarlo venga de donde venga; despues lo que Miguel marco como que no está en
    /// el papel, que es la marca mas reciente y la que dice que ahi no hay nada que buscar;
    /// despues el tachon, que es una marca del propio papel; despues el origen y la confianza.
    /// </remarks>
    public static EstadoDeCampo Decidir(
        ProcedenciaDeCampo? procedencia,
        bool esValido,
        double umbral = UmbralDeConfianzaBaja)
    {
        if (!esValido) return EstadoDeCampo.NoValido;
        if (procedencia is null) return EstadoDeCampo.Revisar;
        if (procedencia.AusenteEnElPapel) return EstadoDeCampo.NoEstaEnElPapel;
        if (procedencia.AnuladoPorTachon) return EstadoDeCampo.Tachado;
        if (procedencia.Origen == OrigenDeCampo.Anotacion) return EstadoDeCampo.Anotacion;
        if (procedencia.Origen == OrigenDeCampo.Manual) return EstadoDeCampo.Ocr;
        return procedencia.Origen == OrigenDeCampo.Ocr && procedencia.Confianza >= umbral
            ? EstadoDeCampo.Ocr
            : EstadoDeCampo.Revisar;
    }

    /// <summary>La palabra escrita de cada estado; ninguna se repite y ninguna es un color.</summary>
    public static string Palabra(EstadoDeCampo estado) => estado switch
    {
        EstadoDeCampo.Anotacion => "anotación",
        EstadoDeCampo.Ocr => "OCR",
        EstadoDeCampo.Revisar => "revisar",
        EstadoDeCampo.NoValido => "no válido",
        EstadoDeCampo.NoEstaEnElPapel => "no está en el papel",
        _ => "tachado, sin corrección",
    };

    /// <summary>
    /// La palabra que se pinta: la del estado, salvo que Miguel ya lo diera por bueno.
    /// </summary>
    /// <remarks>
    /// Un campo firmado no puede seguir diciendo «revisar» al lado de un boton que dice
    /// «firmado»: son dos frases de la misma ficha que se contradicen. Pasa de verdad con
    /// <c>templo_nombre</c>, que se firma sin que nadie sepa de donde salio su valor.
    /// </remarks>
    public static string PalabraEnPantalla(EstadoDeCampo estado, ProcedenciaDeCampo? procedencia)
        => procedencia?.Verificado == true ? "dado por bueno" : Palabra(estado);

    /// <summary>La linea pequena bajo el campo: de donde salio y con que confianza.</summary>
    public static string Descripcion(EstadoDeCampo estado, ProcedenciaDeCampo? procedencia)
    {
        if (estado == EstadoDeCampo.Anotacion) return "confianza 1,00 · escrito en el PDF";
        if (estado == EstadoDeCampo.NoEstaEnElPapel) return "usted marcó que el formulario no trae este campo";
        if (estado == EstadoDeCampo.Tachado) return "tachado en el papel, sin corrección escrita";
        if (procedencia is null) return "sin lectura guardada";
        if (procedencia.Origen == OrigenDeCampo.Manual) return "corregido a mano";
        if (procedencia.Confianza is not double confianza) return "el lector no leyó nada aquí";
        return string.Format(CultureInfo.GetCultureInfo("es-ES"), "confianza {0:F2}", confianza);
    }

    /// <summary>
    /// Lo que el lector vio cuando el campo se quedo vacio. Cadena vacia si no aplica.
    /// </summary>
    /// <remarks>
    /// Las cuatro veces que devuelve vacio, y por que cada una:
    /// el campo TIENE valor —senalar lo que esta bien ensena a ignorar la senal—;
    /// el lector no leyo nada —no hay nada que ensenar—;
    /// el campo esta anulado por un tachon —alguien lo tacho por algo, y ensenar lo de
    /// debajo invita a copiarlo—;
    /// y el campo lo vacio una mano —no se perdio nada—.
    /// <para>
    /// Y la quinta, del 2026-09-05: el campo esta marcado como que no está en el papel.
    /// Ensenar ahi lo que el lector creyo ver invita a copiarlo justo despues de que Miguel
    /// dijera que ese dato no existe en la hoja.
    /// </para>
    /// </remarks>
    public static string LineaDeLoQueSeLeyo(string? valor, ProcedenciaDeCampo? procedencia)
    {
        if (ReglasDeCampo.Limpiar(valor) is not null) return string.Empty;
        if (procedencia is null) return string.Empty;
        if (procedencia.AusenteEnElPapel) return string.Empty;
        if (procedencia.Origen != OrigenDeCampo.Vacio) return string.Empty;
        if (procedencia.AnuladoPorTachon) return string.Empty;
        var leido = ReglasDeCampo.Limpiar(procedencia.ValorOcr);
        return leido is null ? string.Empty : $"El lector leyó aquí «{leido}» y no encajó: no se guardó.";
    }

    /// <summary>
    /// Si este campo es de los que hay que mirar primero. Criterio C4-13.
    /// </summary>
    /// <remarks>
    /// Lo dudoso es lo vacio y lo de poca confianza, que es lo que Rossum ensena primero
    /// («prompts the user to inspect empty fields and review data with low confidence
    /// scores»). Que Miguel no lea 26 campos buenos para encontrar el malo.
    /// <para>
    /// ⚠️ <b>Lo ya resuelto sale de aqui, y antes no salia.</b> Medido con la ventana
    /// abierta el 2026-09-05: despues de firmar el unico campo pendiente, el pie seguia
    /// diciendo «Quedan 1 campos por comprobar · 1 dados por buenos» sobre el MISMO campo.
    /// Un contador que no baja al trabajar no guia a nadie. Resuelto es firmado —Miguel lo
    /// dio por bueno— o marcado como que no está en el papel —no hay nada que comprobar—.
    /// </para>
    /// </remarks>
    public static bool EsDudoso(string? valor, ProcedenciaDeCampo? procedencia, bool esValido)
    {
        if (!esValido) return true;
        if (procedencia is not null && procedencia.AusenteEnElPapel) return false;
        if (procedencia is not null && procedencia.Verificado) return false;
        if (ReglasDeCampo.Limpiar(valor) is null) return true;
        if (procedencia is null) return true;
        if (procedencia.AnuladoPorTachon) return true;
        if (procedencia.Origen is OrigenDeCampo.Anotacion or OrigenDeCampo.Manual) return false;
        return procedencia.Confianza is not double confianza || confianza < UmbralDeConfianzaBaja;
    }
}
