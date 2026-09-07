using Fichas.Contratos.Modelos;
using Fichas.Lectura;

namespace Fichas.App.Importar;

/// <summary>
/// El reintento: cuando la base rechaza la fila, se retira lo que sobra y NUNCA la hoja.
/// </summary>
/// <remarks>
/// <para>⛔ <b>Un documento NUNCA se rechaza.</b> Es una de las primeras reglas del dueño, y
/// aquí es donde se cumple o se rompe: la base es lo último que puede decir que no, y si su
/// «no» se acepta tal cual, la hoja se pierde entera con sus personas dentro.</para>
///
/// <para>⚠️ <b>Medido el 2026-09-05</b> sobre los dos formularios de grupo del dueño:
/// <b>4 de 20 hojas no entraban</b>. Sus páginas 5 y 6 vienen del revés en el escaneo, así que
/// el OCR devolvía <c>«WANLCA Bzeench»</c> en el número de unidad y
/// <c>«eptomber 2026 Currency:»</c> en la fecha. La lectura las devuelve tal cual y eso está
/// bien —requisito 9: se enseña lo que el papel decía—, pero el esquema lleva
/// <c>CHECK (unidad_numero GLOB '[0-9]…')</c> y otro igual para <c>fecha_viaje</c>, así que
/// SQLite rechazaba la fila. El reintento que había solo sabía retirar <c>numero_caso</c>, que
/// no era el culpable, y por eso fallaba dos veces y se rendía.</para>
///
/// <para><b>Qué se retira y qué no.</b> Solo se toca después de un rechazo: una hoja sana no
/// pierde nada. Y se retira lo MENOS posible, en dos pasos: primero los campos cuyo valor no
/// tiene la forma que su propia regla pide —que son los únicos que un <c>GLOB</c> del esquema
/// puede rechazar—, y solo si la base sigue diciendo que no, los tres. Un número de caso bien
/// formado sobrevive a que la unidad venga mal, que es lo que antes no pasaba.</para>
///
/// <para>⛔ <b>Nada se corrige y nada se pierde</b> (regla permanente 1). El valor retirado NO
/// se «arregla»: viaja entero a <c>procedencia_campo.valor_ocr</c> —que es de donde la pantalla
/// de corrección lo lee— y deja su aviso y su renglón diciendo cuál era. Miguel lo teclea.</para>
/// </remarks>
public sealed partial class GuardadoDeHojas
{
    /// <summary>Un campo que hubo que retirar para que la fila entrara, con lo que decía.</summary>
    /// <param name="Campo">La columna, tal como se llama en la base.</param>
    /// <param name="Rotulo">Cómo se llama en la pantalla, para poder nombrarlo en el aviso.</param>
    /// <param name="Valor">Lo que traía, que es lo que hay que teclear a mano.</param>
    private readonly record struct CampoRetirado(string Campo, string Rotulo, string Valor);

    /// <summary>
    /// Los tres campos del caso que la base guarda con una FORMA fija, y la regla que la dice.
    /// </summary>
    /// <remarks>
    /// <para>Son exactamente los que el esquema protege con un <c>CHECK … GLOB</c>: 6 o 7 dígitos
    /// para <c>unidad_numero</c>, <c>AAAA-MM-DD</c> para <c>fecha_viaje</c>, y cuatro letras más
    /// cuatro dígitos para <c>numero_caso</c> —este último ya no en una base nueva, pero sí en la
    /// del dueño, que pasó por la migración 12 del Python.</para>
    ///
    /// <para>La regla que se consulta es la de <see cref="Normalizacion"/> y no la del motor a
    /// propósito: <b>la lectura y el guardado tienen que estar de acuerdo en qué forma tiene un
    /// campo</b>. Si el guardado se inventara su propio criterio, retiraría valores que la base sí
    /// acepta, o dejaría pasar los que no.</para>
    ///
    /// <para>El orden importa y va de menos a más valioso: el número de caso es el último porque
    /// es lo que identifica al documento, y retirarlo obliga a teclearlo antes de poder cruzar
    /// nada con el Excel de los compañeros.</para>
    /// </remarks>
    private static readonly (string Campo, string Rotulo, Func<Caso, string?> Leer, Func<string?, string?> DarForma)[]
        CamposConFormaFija =
        [
            (CamposDeLaHoja.CampoUnidadNumero, "N.º de unidad",
                caso => caso.UnidadNumero, Normalizacion.NormalizarNumeroDeUnidad),
            (CamposDeLaHoja.CampoFechaViaje, "Fecha de viaje",
                caso => caso.FechaViaje, Normalizacion.NormalizarFecha),
            (CamposDeLaHoja.CampoNumeroCaso, "N.º de caso",
                caso => caso.NumeroCaso, Normalizacion.NormalizarNumeroDeCaso),
        ];

    /// <summary>
    /// Los recortes que se prueban, de menos a más, hasta que la base acepte la fila.
    /// </summary>
    /// <remarks>
    /// Devuelve como mucho dos: los campos sin su forma, y luego los tres. El segundo solo sale
    /// si de verdad retira más que el primero; ofrecer dos recortes iguales sería preguntarle
    /// dos veces lo mismo a la base.
    /// </remarks>
    private static IEnumerable<IReadOnlyList<CampoRetirado>> RecortesQueSeIntentan(Caso caso)
    {
        var traidos = LosCamposConFormaQueTraeElCaso(caso);
        var sinSuForma = traidos.Where(cual => !cual.TieneSuForma).Select(cual => cual.Retirado).ToArray();

        if (sinSuForma.Length > 0) yield return sinSuForma;
        if (traidos.Count > sinSuForma.Length) yield return [.. traidos.Select(cual => cual.Retirado)];
    }

    /// <summary>Los campos con forma fija que ESTE caso trae, y si su valor tiene esa forma.</summary>
    private static IReadOnlyList<(CampoRetirado Retirado, bool TieneSuForma)> LosCamposConFormaQueTraeElCaso(Caso caso)
    {
        var salida = new List<(CampoRetirado, bool)>(CamposConFormaFija.Length);
        foreach (var (campo, rotulo, leer, darForma) in CamposConFormaFija)
        {
            var valor = leer(caso);
            if (valor is null) continue;
            salida.Add((new CampoRetirado(campo, rotulo, valor), string.Equals(darForma(valor), valor, StringComparison.Ordinal)));
        }

        return salida;
    }

    /// <summary>El mismo caso con esos campos a nulo. No toca ningún otro.</summary>
    private static Caso SinLosCampos(Caso caso, IReadOnlyList<CampoRetirado> retirados)
    {
        foreach (var retirado in retirados)
        {
            caso = retirado.Campo switch
            {
                CamposDeLaHoja.CampoUnidadNumero => caso with { UnidadNumero = null },
                CamposDeLaHoja.CampoFechaViaje => caso with { FechaViaje = null },
                CamposDeLaHoja.CampoNumeroCaso => caso with { NumeroCaso = null },
                _ => caso,
            };
        }

        return caso;
    }

    /// <summary>El aviso de un campo que la base no aceptó, con su valor crudo dentro.</summary>
    /// <remarks>
    /// El del número de caso se dice con sus propias palabras porque su consecuencia es otra: sin
    /// número, el caso no se puede cruzar con el Excel que devuelven los compañeros. Los demás
    /// solo dejan un hueco que rellenar.
    /// </remarks>
    private static Aviso AvisoDeLoRetirado(CampoRetirado retirado)
        => retirado.Campo == CamposDeLaHoja.CampoNumeroCaso
            ? Aviso.Advierte(
                $"El número de caso leído, «{retirado.Valor}», no tiene la forma que la base acepta: el caso entró sin él.",
                retirado.Campo,
                "Todo lo demás que se leyó está dentro y no hay que volver a teclearlo. Abra el caso, " +
                "mire el PDF y escriba el número; el programa NO lo corrige solo porque inventaría un dato.")
            : Aviso.Advierte(
                $"«{retirado.Rotulo}» no entró: la base no acepta «{retirado.Valor}». El documento entró igual.",
                retirado.Campo,
                "Lo que el papel decía NO se ha perdido: está guardado en la procedencia de ese campo y se ve " +
                "al abrir el documento. Ábralo, mire el PDF y escríbalo; el programa NO lo corrige solo porque " +
                "inventaría un dato.");
}
