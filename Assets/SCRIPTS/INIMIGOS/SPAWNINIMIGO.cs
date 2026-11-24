using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Espalha esqueletos pelo mapa ao iniciar a fase.
///  - só no chão do TERRENO (nunca em cima de telhado, pedra ou árvore);
///  - longe do jogador (distMinJogador) e longe uns dos outros (distMinEntreEles), para não amontoar.
/// </summary>
public class SpawnInimigos : MonoBehaviour
{
    public GameObject prefab;
    public int quantidade = 10;
    public float raio = 100f;              // área ao redor deste objeto
    public float distMinJogador = 20f;     // não nascer colado no jogador
    [Tooltip("Distância mínima entre dois esqueletos sorteados (evita grupinhos).")]
    public float distMinEntreEles = 10f;

    void Start()
    {
        if (prefab == null) return;

        Vector3 jogador = Camera.main != null ? Camera.main.transform.position : transform.position;
        var usados = new List<Vector3>();
        float distEntre = distMinEntreEles;

        int tentativas = 0;
        while (usados.Count < quantidade && tentativas < quantidade * 60)
        {
            tentativas++;
            // se estiver difícil achar lugar, aceita ficar um pouco mais perto dos outros
            if (tentativas % (quantidade * 20) == 0) distEntre *= 0.7f;

            Vector3 xz = transform.position + new Vector3(Random.Range(-raio, raio), 0f, Random.Range(-raio, raio));
            Vector3 chao;
            if (!ChaoDoTerreno(xz, out chao)) continue;

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(chao, out hit, 1.5f, NavMesh.AllAreas)) continue;
            if (Mathf.Abs(hit.position.y - chao.y) > 0.5f) continue;

            if (Vector3.Distance(hit.position, jogador) < distMinJogador) continue;

            bool perto = false;
            foreach (var u in usados)
                if ((u - hit.position).sqrMagnitude < distEntre * distEntre) { perto = true; break; }
            if (perto) continue;

            Instantiate(prefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            usados.Add(hit.position);
        }
    }

    // A primeira coisa que o raio encontra, vindo de cima, precisa ser o terreno
    static bool ChaoDoTerreno(Vector3 xz, out Vector3 chao)
    {
        chao = Vector3.zero;
        RaycastHit h;
        if (!Physics.Raycast(new Vector3(xz.x, 1000f, xz.z), Vector3.down, out h, 2000f, ~0, QueryTriggerInteraction.Ignore))
            return false;
        if (!(h.collider is TerrainCollider)) return false;
        chao = h.point;
        return true;
    }
}
