#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Menu:  ISLAND > Inimigos > Diagnosticar TEMPLO 3
/// Mostra em que etapa os pontos de dentro do templo são descartados na hora de colocar esqueletos.
/// Não muda nada.
/// </summary>
public static class DiagnosticarTemplo
{
    [MenuItem("ISLAND/Inimigos/Diagnosticar TEMPLO 3")]
    static void Diagnosticar()
    {
        var alvo = AcharNaCena.Objeto("TEMPLO 3");
        if (alvo == null) { EditorUtility.DisplayDialog("Diagnóstico", "Não achei TEMPLO 3.", "OK"); return; }

        Bounds b = new Bounds(alvo.position, Vector3.one);
        bool tem = false;
        foreach (var r in alvo.GetComponentsInChildren<Renderer>()) { if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds); }

        int colunas = 0, pisoDoTemplo = 0, comTeto = 0, altura = 0, livre = 0, comNav = 0, navPerto = 0;
        var colisoresBloqueando = new Dictionary<string, int>();
        var difs = new List<float>();
        int semColisao = 0, comColisao = 0;
        foreach (var r in alvo.GetComponentsInChildren<MeshRenderer>())
            if (r.GetComponent<Collider>() != null) comColisao++; else semColisao++;

        bool bf = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        try
        {
            for (float x = b.min.x; x <= b.max.x; x += 1.5f)
            for (float z = b.min.z; z <= b.max.z; z += 1.5f)
            {
                colunas++;
                foreach (var h in Physics.RaycastAll(new Vector3(x, b.max.y + 2f, z), Vector3.down, b.size.y + 10f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.normal.y < 0.8f || !h.collider.transform.IsChildOf(alvo)) continue;
                    pisoDoTemplo++;
                    RaycastHit cima;
                    if (!Physics.Raycast(h.point + Vector3.up * 0.05f, Vector3.up, out cima, 30f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    comTeto++;
                    if (cima.distance < 2.2f) continue;
                    altura++;
                    var cols = Physics.OverlapCapsule(h.point + Vector3.up * 0.6f, h.point + Vector3.up * 1.7f, 0.5f, ~0, QueryTriggerInteraction.Ignore);
                    if (cols.Length > 0)
                    {
                        foreach (var c in cols) { string n = c.name; colisoresBloqueando[n] = colisoresBloqueando.TryGetValue(n, out int k) ? k + 1 : 1; }
                        continue;
                    }
                    livre++;
                    NavMeshHit nh;
                    if (!NavMesh.SamplePosition(h.point, out nh, 3f, NavMesh.AllAreas)) continue;
                    comNav++;
                    difs.Add(nh.position.y - h.point.y);
                    if (Mathf.Abs(nh.position.y - h.point.y) <= 0.4f && Vector3.Distance(nh.position, h.point) <= 0.6f) navPerto++;
                }
            }
        }
        finally { Physics.queriesHitBackfaces = bf; }

        var sb = new StringBuilder();
        sb.AppendLine("Peças com colisão: " + comColisao + " | sem colisão: " + semColisao);
        sb.AppendLine("Colunas testadas: " + colunas);
        sb.AppendLine("Piso do templo: " + pisoDoTemplo);
        sb.AppendLine("  com teto: " + comTeto);
        sb.AppendLine("  com altura ≥ 2,2 m: " + altura);
        sb.AppendLine("  sem nada no caminho: " + livre);
        sb.AppendLine("  com NavMesh a até 3 m: " + comNav);
        sb.AppendLine("  com NavMesh colado no piso: " + navPerto);
        if (difs.Count > 0) { difs.Sort(); sb.AppendLine("Diferença NavMesh−piso (mediana): " + difs[difs.Count / 2].ToString("F2") + " m"); }
        sb.AppendLine("Bloqueando (top): ");
        var lista = new List<KeyValuePair<string, int>>(colisoresBloqueando);
        lista.Sort((a, c) => c.Value.CompareTo(a.Value));
        for (int i = 0; i < Mathf.Min(6, lista.Count); i++) sb.AppendLine("  " + lista[i].Key + " ×" + lista[i].Value);
        sb.AppendLine("Tamanho: " + b.size.ToString("F0") + " em " + b.center.ToString("F0"));

        Debug.Log("Diagnóstico TEMPLO 3:\n" + sb);
        EditorUtility.DisplayDialog("Diagnóstico TEMPLO 3", sb.ToString(), "OK");
    }
}
#endif
