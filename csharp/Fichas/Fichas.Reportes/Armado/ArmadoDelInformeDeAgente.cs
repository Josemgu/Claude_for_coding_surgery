using System.Globalization;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;

namespace Fichas.Reportes.Armado;

/// <summary>
/// La seccion «Lo que hizo», que es la respuesta a la pregunta que hizo el dueno.
/// </summary>
/// <remarks>
/// <para><b>Sus palabras, del 2026-09-05</b> (<c>DECISIONES.md</c>): <i>«Es importante tener un
/// informe por agente también: lo que hicieron los agentes en ese mes y lo que hicieron en esa
/// semana, qué hicieron»</i>.</para>
///
/// <para><b>Va DELANTE del informe de companero que ya existia, y no lo sustituye.</b> Lo que
/// habia —el informe de los jefes recortado a los casos de ese companero— contesta <i>«¿cómo
/// están sus casos?»</i>, que es otra pregunta buena. Esta seccion contesta <i>«¿qué hizo?»</i>,
/// que es la que faltaba. Se leen seguidas.</para>
///
/// <para><b>Cada cifra sale con su denominador en la ultima columna.</b> «Contestó 4» no se
/// puede leer; «4 de los 6 que se le asignaron» sí. Es la parte del criterio que se olvida
/// cuando una tabla de metricas se escribe deprisa.</para>
/// </remarks>
public static class ArmadoDelInformeDeAgente
{
    /// <summary>Cuantos dias mira la columna de «la semana».</summary>
    /// <remarks>
    /// Siete, y contando el ultimo dia del periodo como uno de los siete. Nadie lo definio: el
    /// dueno dijo «esa semana» y no dijo cual, asi que se declara aqui y se dice dentro del
    /// informe, en una nota. Una ventana sin declarar hace que dos personas lean la misma cifra
    /// de dos maneras.
    /// </remarks>
    public const int DiasDeLaSemana = 7;

    /// <summary>El titulo de la seccion; lo mira la prueba y lo mira el indice del informe.</summary>
    public const string TituloDeLaSeccion = "Lo que hizo";

    /// <summary>La seccion con las dos ventanas, una en cada columna.</summary>
    /// <param name="mes">La ventana larga: el periodo que se pidio.</param>
    /// <param name="semana">La ventana corta: la cola del mismo periodo.</param>
    /// <param name="enElMes">Lo que hizo en la larga.</param>
    /// <param name="enLaSemana">Lo que hizo en la corta.</param>
    /// <param name="llevaEncimaAhora">Cuantos documentos tiene asignados y vivos ahora mismo.</param>
    public static Seccion LoQueHizo(
        Periodo mes,
        Periodo semana,
        TrabajoDeUnAgente enElMes,
        TrabajoDeUnAgente enLaSemana,
        int llevaEncimaAhora)
    {
        ArgumentNullException.ThrowIfNull(mes);
        ArgumentNullException.ThrowIfNull(semana);
        ArgumentNullException.ThrowIfNull(enElMes);
        ArgumentNullException.ThrowIfNull(enLaSemana);

        Columna[] columnas =
        [
            new("Qué hizo", ClaseDeColumna.Crudo, 34),
            new($"En el mes ({mes.EnTexto()})", ClaseDeColumna.Crudo, 16),
            new($"En la semana ({semana.EnTexto()})", ClaseDeColumna.Crudo, 16),
            new("Cómo se cuenta", ClaseDeColumna.Crudo, 44),
        ];

        return new Seccion(
            TituloDeLaSeccion,
            [
                $"«La semana» son los últimos {DiasDeLaSemana} días del período que se pidió, "
                + $"contando el último como uno de los {DiasDeLaSemana}: "
                + $"{semana.EnTexto()}. Se dice porque nadie lo definió, y una ventana sin "
                + "declarar hace que dos personas lean la misma cifra de dos maneras.",
                "Lo que cuenta como «hecho» es lo que este agente CONTESTÓ en su hoja, no lo que "
                + "se le mandó: lo que se le mandó es trabajo de quien reparte. Por eso «se le "
                + "asignaron» y «contestó» son dos renglones y no uno.",
                "Los tres motivos son los del dueño, y suman los documentos que contestó como no "
                + "completos. Si no suman, es que alguna hoja volvió sin decir por qué.",
            ],
            columnas,
            [
                Fila("Documentos que se le asignaron",
                    enElMes.SeLeAsignaron, enLaSemana.SeLeAsignaron,
                    "Asignaciones cuya fecha cae dentro de la ventana, vivas o retiradas, sin "
                    + "repetir el mismo documento."),

                Fila("Documentos que contestó",
                    enElMes.Contesto, enLaSemana.Contesto,
                    $"De los {enElMes.SeLeAsignaron} que se le asignaron en el mes. Cuenta la fecha "
                    + "en que su hoja dejó escrito el estado, no la de la asignación."),

                Fila("… y dijo que estaban completos",
                    enElMes.Completas, enLaSemana.Completas,
                    $"De los {enElMes.Contesto} que contestó en el mes."),

                Fila("… y dijo que NO estaban completos",
                    enElMes.NoCompletas, enLaSemana.NoCompletas,
                    $"De los {enElMes.Contesto} que contestó en el mes. Estos son los que pueden "
                    + "subir un peldaño de la escalera."),

                Fila("Porque no se pudo comunicar con el líder",
                    enElMes.NoSePudoComunicar, enLaSemana.NoSePudoComunicar,
                    $"De los {enElMes.NoCompletas} no completos del mes."),

                Fila("Porque el líder no lo hizo",
                    enElMes.ElLiderNoLoHizo, enLaSemana.ElLiderNoLoHizo,
                    $"De los {enElMes.NoCompletas} no completos del mes."),

                Fila("Por otra razón",
                    enElMes.OtraRazon, enLaSemana.OtraRazon,
                    $"De los {enElMes.NoCompletas} no completos del mes. Lo explica su comentario, "
                    + "que no tiene lista cerrada."),

                Fila("Sin decir por qué",
                    enElMes.SinMotivoEscrito, enLaSemana.SinMotivoEscrito,
                    $"De los {enElMes.NoCompletas} no completos del mes. Una hoja que vuelve sin "
                    + "motivo no puede subir de peldaño: nadie sabe qué pasó."),
            ],
            $"Lleva {llevaEncimaAhora} "
            + Plural.Palabra(llevaEncimaAhora, "documento asignado", "documentos asignados")
            + " ahora mismo. Esa cifra es de hoy, no del período.");
    }

    private static IReadOnlyList<string?> Fila(string que, int enElMes, int enLaSemana, string comoSeCuenta)
        => [que, Numero(enElMes), Numero(enLaSemana), comoSeCuenta];

    private static string Numero(int valor) => valor.ToString(CultureInfo.InvariantCulture);
}
