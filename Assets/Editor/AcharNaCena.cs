#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Acha um objeto da cena pelo nome. Se houver vários com o mesmo nome (ex.: o templo de verdade e um
/// grupo vazio chamado "templo"), fica com o que tem mais peças desenhadas — o objeto de verdade.
/// Usado pelas ferramentas do menu ISLAND.
/// </summary>
public static class AcharNaCena
{
    public static Transform Objeto(string nome)
    {
        Transform melhor = null;
        int melhorPecas = -1, melhorProf = int.MaxValue;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (EditorUtility.IsPersistent(t) || t.name != nome) continue;
            if (DentroDeGrupoDeFerramenta(t)) continue;

            int pecas = t.GetComponentsInChildren<MeshRenderer>().Length;
            int prof = 0;
            for (var a = t.parent; a != null; a = a.parent) prof++;

            if (pecas > melhorPecas || (pecas == melhorPecas && prof < melhorProf))
            {
                melhor = t; melhorPecas = pecas; melhorProf = prof;
            }
        }
        return melhor;
    }

    // Grupos criados pelas próprias ferramentas nunca são o "lugar" procurado
    static bool DentroDeGrupoDeFerramenta(Transform t)
    {
        for (var a = t.parent; a != null; a = a.parent)
            if (a.name == "ESQUELETOS_TEMPLOS" || a.name == "DECORACAO_TEMPLO_YAS" || a.name == "BAUS" || a.name == "ESCADA_2_ANDAR")
                return true;
        return false;
    }
}
#endif
