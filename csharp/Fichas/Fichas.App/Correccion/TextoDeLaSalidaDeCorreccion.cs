using Fichas.Reportes.Reglas;

namespace Fichas.App.Correccion;

/// <summary>
/// Lo que se lee cuando un documento SALE de Correccion, fuera del XAML para poder leerlo en
/// una prueba.
/// </summary>
/// <remarks>
/// <para><b>Por que estas frases existen.</b> El dueno pidio el 2026-09-07 que un documento
/// corregido salga de Correccion y pase al grupo de su fecha. La mitad peligrosa de eso es
/// que salga y el no vea a donde fue: si un documento se va de una lista y el no sabe si
/// llego a la otra, lo pierde, y en este programa perder un documento significa que alguien
/// llegue al templo y no pueda entrar. Estas frases son las que le dejan verlo pasar.</para>
///
/// <para>⛔ <b>El idioma es el de dos palabras</b> (<c>DECISIONES.md</c>, 2026-09-07, «DOS
/// ESTADOS Y NO CUATRO»): «resuelto» y «me falta». Ninguna de estas frases dice «listo para
/// asignar», «completa» ni «confirmada», y eso lo vigila
/// <c>PruebasDeQueCorreccionEsUnSitioDePaso.SoloSeLeenLasDosPalabras</c>.</para>
///
/// <para>⛔ Aqui no se decide nada ni se escribe nada: son palabras.</para>
/// </remarks>
public static class TextoDeLaSalidaDeCorreccion
{
    /// <summary>Lo que se dice cuando ya no queda ni un documento con algo que falte.</summary>
    public const string NoQuedaNadaQueCorregir = "No queda ningún documento con algo que falte.";

    /// <summary>
    /// «CASP2609 · resuelto · pasa al Grupo del 12 de septiembre · 325535 · Rama San Juan ·
    /// quedan 3 documentos con algo que falta».
    /// </summary>
    /// <remarks>
    /// Las tres partes van juntas y ninguna sobra: CUAL era —o no se sabe de que documento
    /// habla—, A DONDE pasa —o es una desaparicion— y CUANTOS quedan, que es el denominador que
    /// pide <c>CLAUDE.md</c> §8 y lo que le dice si ha terminado.
    /// </remarks>
    /// <param name="numeroDeCaso">El numero del documento que acaba de salir.</param>
    /// <param name="aDondePasa">Como se llama el grupo al que pasa; vacio si no se sabe.</param>
    /// <param name="cuantosQuedan">Cuantos documentos siguen con algo que falta.</param>
    public static string YaNoEstaEnCorreccion(string numeroDeCaso, string aDondePasa, int cuantosQuedan)
    {
        // «quedan 1 documento» se leyo tal cual en la medicion con la ventana abierta del
        // 2026-09-09. Delata lo mismo que un «1 campos»: que nadie leyo la frase.
        var quedan = Plural.Palabra(cuantosQuedan, "queda", "quedan")
                     + " " + Plural.Con(cuantosQuedan, "documento", "documentos")
                     + " con algo que falta";
        var destino = string.IsNullOrWhiteSpace(aDondePasa)
            // Sin grupo al que pasar no se inventa uno. Pasa con un documento archivado, que
            // no entra en ninguno; decir un destino cualquiera seria peor que no decir ninguno.
            ? "ya no sale en esta lista"
            : $"pasa al {aDondePasa}";

        return $"{numeroDeCaso} · {Vocabulario.DosEstados.Resuelto} · {destino} · {quedan}.";
    }

    /// <summary>
    /// Lo que dice la banda de un documento abierto al que ya no le falta nada.
    /// </summary>
    /// <remarks>
    /// Se queda en pantalla mientras lo tiene delante —sacarlo en el acto le pondria otro
    /// documento sin haberlo pedido— y la banda es lo que evita que crea que sigue en la lista.
    /// </remarks>
    /// <param name="aDondePertenece">Como se llama su grupo; vacio si no se sabe.</param>
    public static string YaEstabaResuelto(string aDondePertenece)
    {
        var donde = string.IsNullOrWhiteSpace(aDondePertenece)
            ? "Ya no sale en esta lista."
            : $"Está en el {aDondePertenece}.";

        return $"Este documento está {Vocabulario.DosEstados.Resuelto}. {donde} La próxima vez que entre aquí no lo verá.";
    }

    /// <summary>«Ver el Grupo del 12 de septiembre · 325535 · Rama San Juan».</summary>
    /// <remarks>
    /// El boton nombra el sitio al que lleva y no dice «Ver el grupo» a secas, por lo mismo que
    /// el de volver: un boton que no dice a donde va obliga a pulsarlo para averiguarlo. Es la
    /// misma regla que se anoto en <c>PaginaDeCorreccion.Vuelta.cs</c> el 2026-09-07.
    /// </remarks>
    /// <param name="aDondePasa">Como se llama el grupo.</param>
    public static string BotonDeVerElGrupo(string aDondePasa)
        => string.IsNullOrWhiteSpace(aDondePasa) ? "Ver su grupo" : $"Ver el {aDondePasa}";

    /// <summary>
    /// «8 documentos en este grupo · 5 grupos · 23 con algo que falta, de 75».
    /// </summary>
    /// <remarks>
    /// <para>⚠️ <b>Sustituye a <c>TextoDeLosGrupos.Denominador</c> y anade una cifra, y esa
    /// cifra es la que hace honesta la pantalla.</b> Desde este pase Correccion no ensena todos
    /// los documentos sino solo los que piden algo; con el denominador viejo —«75 en total»— la
    /// suma de los grupos no cuadraria con el total y no habria forma de saber si faltaba algo
    /// por ver o si la pantalla se estaba comiendo documentos.</para>
    /// </remarks>
    /// <param name="enEsteGrupo">Cuantos documentos trae el grupo elegido.</param>
    /// <param name="cuantosGrupos">Cuantos grupos de trabajo hay.</param>
    /// <param name="conAlgoQueFalta">Cuantos documentos piden algo en toda la base.</param>
    /// <param name="enTotal">Cuantos documentos hay sin archivar.</param>
    public static string Denominador(int enEsteGrupo, int cuantosGrupos, int conAlgoQueFalta, int enTotal)
        => $"{Plural.Con(enEsteGrupo, "documento", "documentos")} en este grupo"
           + $" · {Plural.Con(cuantosGrupos, "grupo", "grupos")}"
           + $" · {conAlgoQueFalta} con algo que falta, de {enTotal}";
}
