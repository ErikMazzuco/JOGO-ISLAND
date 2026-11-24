#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Menu:  ISLAND > Inimigos > Conferir Esqueletos no Chão
/// Para cada esqueleto colocado nos templos, mostra a distância até o piso embaixo dele e até o NavMesh
/// (onde o NavMeshAgent vai "grudar" quando o jogo começar). Não muda nada.
/// </summary>
public static class ConferirEsqueletos
{
    [MenuItem("ISLAND/Inimigos/Conferir Esqueletos no Chão")]
    static void Conferir()
    {
        var grupo = GameObject.Find("ESQUELETOS_TEMPLOS");
        if (grupo == null) { EditorUtility.DisplayDialog("Conferir", "Não achei ESQUELETOS_TEMPLOS.", "OK"); return; }

        var sb = new StringBuilder();
        int ok = 0, ruins = 0;
        foreach (Transform sub in grupo.transform)
        {
            int okSub = 0, ruimSub = 0;
            float piorPiso = 0f, piorNav = 0f;
            foreach (Transform e in sub)
            {
                Vector3 p = e.position;

                // piso: primeira superfície logo abaixo (ou um pouco acima) dos pés, ignorando o próprio esqueleto
                float dPiso = float.NaN;
                foreach (var h in Physics.RaycastAll(p + Vector3.up * 1.0f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.collider.transform.IsChildOf(e)) continue;
                    if (h.normal.y < 0.6f) continue;
                    float d = p.y - h.point.y;
                    if (float.IsNaN(dPiso) || Mathf.Abs(d) < Mathf.Abs(dPiso)) dPiso = d;
                }

                float dNav = float.NaN;
                NavMeshHit nh;
                if (NavMesh.SamplePosition(p, out nh, 3f, NavMesh.AllAreas)) dNav = p.y - nh.position.y;

                bool bom = !float.IsNaN(dPiso) && !float.IsNaN(dNav) && Mathf.Abs(dPiso) < 0.35f && Mathf.Abs(dNav) < 0.35f;
                if (bom) { ok++; okSub++; } else { ruins++; ruimSub++; }
                if (!float.IsNaN(dPiso) && Mathf.Abs(dPiso) > Mathf.Abs(piorPiso)) piorPiso = dPiso;
                if (!float.IsNaN(dNav) && Mathf.Abs(dNav) > Mathf.Abs(piorNav)) piorNav = dNav;
            }
            sb.AppendLine(sub.name + ": " + okSub + " no chão, " + ruimSub + " com problema (pior: piso " + piorPiso.ToString("F2") + " m, NavMesh " + piorNav.ToString("F2") + " m)");
        }
        Debug.Log("Conferir esqueletos:\n" + sb);
        EditorUtility.DisplayDialog("Conferir Esqueletos", ok + " no chão certinho, " + ruins + " com problema.\n\n" + sb, "OK");
    }
}
#endif
