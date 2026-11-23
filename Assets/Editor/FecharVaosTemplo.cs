#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu:  ISLAND > Decorar Templo_Yas > Fechar Vãos Embaixo das Paredes
///
/// Algumas paredes do Templo_Yas ficaram um pouco acima do piso e dá para ver o lado de fora pela fresta.
/// Para cada peça de parede, mede o piso dos dois lados e ESTICA a parede para baixo até encostar
/// no chão. O topo da parede continua no mesmo lugar (não abre vão em cima).
/// As mudanças ficam na cena (como "override" do prefab). Ctrl+Z desfaz.
/// </summary>
public static class FecharVaosTemplo
{
    const string NomeTemplo = "templo";
    const float VaoMinimo = 0.01f;     // menor que isso não precisa mexer
    const float VaoMaximo = 1.0f;      // maior que isso é porta/janela (parede em cima de passagem): não mexe
    const float Sobra = 0.05f;         // entra um pouquinho no chão para não sobrar risco de luz

    [MenuItem("ISLAND/Decorar Templo_Yas/Fechar Vãos Embaixo das Paredes")]
    static void Fechar()
    {
        Transform templo = AcharTemplo();
        if (templo == null)
        {
            EditorUtility.DisplayDialog("Fechar Vãos", "Não achei o objeto \"" + NomeTemplo + "\" na cena aberta.", "OK");
            return;
        }

        Physics.SyncTransforms();
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;

        Undo.SetCurrentGroupName("Fechar Vãos do Templo");
        int grupo = Undo.GetCurrentGroup();

        int esticadas = 0, analisadas = 0;
        var log = new StringBuilder();
        try
        {
            foreach (var r in templo.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.name.StartsWith("HD_Wall")) continue;
                Transform t = r.transform;
                if (Vector3.Dot(t.up, Vector3.up) < 0.99f) continue;   // só paredes em pé
                analisadas++;

                Bounds b = r.bounds;
                // o lado fino da parede (espessura) é para onde fica cada lado do piso
                Vector3 fora = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
                float meiaEspessura = (b.size.x < b.size.z ? b.extents.x : b.extents.z);
                Vector3 ao = b.size.x < b.size.z ? Vector3.forward : Vector3.right;
                float meioComprimento = (b.size.x < b.size.z ? b.extents.z : b.extents.x);

                float base_ = b.min.y;
                float piso = float.MaxValue;

                // procura o piso dos dois lados, em 3 pontos ao longo da parede
                for (int lado = -1; lado <= 1; lado += 2)
                for (int k = -1; k <= 1; k++)
                {
                    Vector3 origem = new Vector3(b.center.x, base_ + 0.4f, b.center.z)
                                   + fora * (lado * (meiaEspessura + 0.35f))
                                   + ao * (k * meioComprimento * 0.7f);
                    float y;
                    if (PisoDoTemplo(origem, templo, t, out y) && y < base_ - VaoMinimo && base_ - y < VaoMaximo)
                        piso = Mathf.Min(piso, y);
                }

                if (piso == float.MaxValue) continue;

                float vao = base_ - piso + Sobra;
                float altura = b.size.y;
                if (altura <= 0.01f) continue;

                float topo = b.max.y;
                Undo.RecordObject(t, "Fechar Vãos do Templo");
                Vector3 esc = t.localScale;
                esc.y *= (altura + vao) / altura;
                t.localScale = esc;

                // mantém o topo onde estava
                Bounds nb = r.bounds;
                t.position += Vector3.up * (topo - nb.max.y);

                esticadas++;
                log.AppendLine("  " + t.name + ": +" + vao.ToString("F2") + " m");
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = backfaces;
        }

        Undo.CollapseUndoOperations(grupo);
        if (esticadas > 0) EditorSceneManager.MarkSceneDirty(templo.gameObject.scene);
        Physics.SyncTransforms();

        Debug.Log("Fechar Vãos: " + esticadas + " de " + analisadas + " paredes esticadas até o chão.\n" + log);
        EditorUtility.DisplayDialog("Fechar Vãos",
            esticadas + " de " + analisadas + " peças de parede foram esticadas até o chão.\n\n" +
            (esticadas > 0 ? "Confira na Scene e salve a cena (Ctrl+S). Ctrl+Z desfaz." : "Nenhum vão encontrado."),
            "OK");
    }

    // Primeiro piso do templo logo abaixo do ponto (ignora a própria parede e coisas que não são do templo)
    static bool PisoDoTemplo(Vector3 origem, Transform templo, Transform parede, out float y)
    {
        y = 0f;
        var hits = Physics.RaycastAll(origem, Vector3.down, VaoMaximo + 1f, ~0, QueryTriggerInteraction.Ignore);
        float melhor = float.MinValue;
        foreach (var h in hits)
        {
            if (h.collider.transform == parede || h.collider.transform.IsChildOf(parede)) continue;
            if (!h.collider.transform.IsChildOf(templo)) continue;
            if (h.normal.y < 0.7f) continue;
            if (h.point.y > melhor) melhor = h.point.y;   // o mais alto abaixo do ponto
        }
        if (melhor == float.MinValue) return false;
        y = melhor;
        return true;
    }

    static Transform AcharTemplo()
    {
        return AcharNaCena.Objeto(NomeTemplo);
    }
}
#endif
