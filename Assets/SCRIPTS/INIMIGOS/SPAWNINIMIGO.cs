using UnityEngine;
using UnityEngine.AI;

public class SpawnInimigos : MonoBehaviour
{
    public GameObject prefab;
    public int quantidade = 10;
    public float raio = 100f;          // área ao redor deste objeto
    public float distMinJogador = 20f; // não nascer colado no jogador

    void Start()
    {
        int criados = 0, tentativas = 0;
        while (criados < quantidade && tentativas < quantidade * 30)
        {
            tentativas++;
            Vector3 p = transform.position + new Vector3(
                Random.Range(-raio, raio), 0, Random.Range(-raio, raio));

            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 100f, NavMesh.AllAreas))
            {
                if (Vector3.Distance(hit.position, Camera.main.transform.position) < distMinJogador)
                    continue;

                Instantiate(prefab, hit.position, Quaternion.identity);
                criados++;
            }
        }
    }
}