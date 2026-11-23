#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu:  ISLAND > Decorar Templo_Yas > Testar Escada (Simular Jogador)
///
/// Cria um jogador de teste com a MESMA colisão (CharacterController) do PLAYER, coloca no pé da
/// ESCADA_2_ANDAR e faz ele andar escada acima pela física. Diz se chegou lá em cima ou onde travou.
/// Não muda nada na cena (o jogador de teste é apagado no fim).
/// </summary>
public static class TestarEscada
{
    [MenuItem("ISLAND/Decorar Templo_Yas/Testar Escada (Simular Jogador)")]
    static void Testar()
    {
        var escada = GameObject.Find("ESCADA_2_ANDAR");
        if (escada == null) { EditorUtility.DisplayDialog("Testar Escada", "Não achei ESCADA_2_ANDAR. Coloque a escada antes.", "OK"); return; }

        var rampa = escada.GetComponentInChildren<BoxCollider>();
        float topo = rampa != null ? rampa.bounds.max.y : escada.transform.position.y + 8f;
        float altura = topo - escada.transform.position.y;

        Vector3 d = escada.transform.forward; d.y = 0; d.Normalize();
        // comprimento real da rampa (no chão)
        if (rampa != null)
        {
            float cos = new Vector2(rampa.transform.forward.x, rampa.transform.forward.z).magnitude;
            compReal = (rampa.size.z - 0.6f) * cos;
        }
        Vector3 pe = escada.transform.position;

        // copia a colisão do jogador de verdade
        var real = Object.FindFirstObjectByType<PlayerMovement>();
        var ccReal = real != null ? real.GetComponent<CharacterController>() : null;
        float skinReal = ccReal != null ? ccReal.skinWidth : 0.08f;
        float raio = ccReal != null ? ccReal.radius : 0.5f;

        string comoEsta = Simular(ccReal, skinReal, pe, d, topo, altura);
        string resultado = "COM O PLAYER COMO ESTÁ (Skin Width " + skinReal + "):\n" + comoEsta;
        if (!comoEsta.StartsWith("✓"))
        {
            float skinBom = Mathf.Max(0.01f, raio * 0.1f);
            resultado += "\n\nCOM Skin Width " + skinBom + " (recomendado pela Unity: 10% do raio):\n" +
                         Simular(ccReal, skinBom, pe, d, topo, altura);
        }
        Debug.Log("Testar Escada:\n" + resultado);

        EditorUtility.DisplayDialog("Testar Escada", resultado, "OK");
    }

    static float compReal = 12f;

    static string Simular(CharacterController ccReal, float skin, Vector3 pe, Vector3 d, float topo, float altura)
    {
        var go = new GameObject("TesteJogador (temporário)");
        go.hideFlags = HideFlags.DontSave;
        var cc = go.AddComponent<CharacterController>();
        if (ccReal != null)
        {
            go.transform.localScale = ccReal.transform.lossyScale;
            cc.radius = ccReal.radius; cc.height = ccReal.height; cc.center = ccReal.center;
            cc.slopeLimit = ccReal.slopeLimit; cc.stepOffset = ccReal.stepOffset;
            cc.minMoveDistance = ccReal.minMoveDistance;
        }
        cc.skinWidth = skin;

        try
        {
            float baseCC = (cc.center.y - Mathf.Max(cc.height * 0.5f, cc.radius)) * go.transform.lossyScale.y;
            go.transform.position = pe - d * 0.4f + Vector3.up * (0.2f - baseCC);
            go.transform.rotation = Quaternion.LookRotation(d);
            Physics.SyncTransforms();

            float pes0 = go.transform.position.y + baseCC;
            float maisAlto = pes0, andou = 0f;
            Vector3 inicio = go.transform.position, anterior = inicio;
            int travado = 0;

            // como o PlayerMovement: anda ~5 m/s a 60 fps, com gravidade puxando para baixo
            for (int i = 0; i < 1500; i++)
            {
                cc.Move(d * 0.083f + Vector3.down * 0.035f);
                float pes = go.transform.position.y + baseCC;
                if (pes > maisAlto) maisAlto = pes;
                andou = Vector3.Dot(go.transform.position - inicio, d);
                if ((go.transform.position - anterior).sqrMagnitude < 0.0001f) travado++; else travado = 0;
                anterior = go.transform.position;
                if (travado > 40) break;
                if (andou > compReal + 2f) break;   // já andou no piso de cima
            }

            // chegou = passou da borda e está andando no piso de cima
            float compRampa = compReal;
            bool chegou = andou > compRampa + 1.0f && maisAlto >= pe.y + altura - 0.5f;
            string bloqueio = "";
            if (!chegou)
            {
                Vector3 centro = go.transform.TransformPoint(cc.center);
                RaycastHit h;
                if (Physics.SphereCast(centro - d * 0.05f, cc.radius * go.transform.lossyScale.x, d, out h, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                    bloqueio = " Bloqueado por " + Caminho(h.collider.transform) + " a " + (h.point.y - pe.y).ToString("F2") + " m de altura, normal " + h.normal.ToString("F2") + ".";
            }
            return (chegou ? "✓ CHEGOU NO 2º ANDAR (andando, sem pular)!" : "✗ não chegou") +
                   "  Subiu " + (maisAlto - pes0).ToString("F1") + " de " + altura.ToString("F1") + " m, andou " + andou.ToString("F1") + " m." + bloqueio;
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    // No Play: coloca o PLAYER no pé da escada, virado para subir (para testar andando de verdade)
    [MenuItem("ISLAND/Decorar Templo_Yas/Levar Jogador ao Pé da Escada (no Play)")]
    static void LevarJogador()
    {
        if (!EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Levar Jogador", "Aperte Play primeiro.", "OK"); return; }
        var escada = GameObject.Find("ESCADA_2_ANDAR");
        var jogador = Object.FindFirstObjectByType<PlayerMovement>();
        if (escada == null || jogador == null) { EditorUtility.DisplayDialog("Levar Jogador", "Não achei a escada ou o PLAYER.", "OK"); return; }

        Vector3 d = escada.transform.forward; d.y = 0; d.Normalize();
        var cc = jogador.GetComponent<CharacterController>();
        float baseCC = cc != null ? (cc.center.y - Mathf.Max(cc.height * 0.5f, cc.radius)) * jogador.transform.lossyScale.y : 0f;

        if (cc != null) cc.enabled = false;   // senão o CharacterController desfaz o teletransporte
        jogador.transform.position = escada.transform.position - d * 1.2f + Vector3.up * (0.3f - baseCC);
        jogador.transform.rotation = Quaternion.LookRotation(d);
        if (cc != null) cc.enabled = true;
        Selection.activeGameObject = jogador.gameObject;
        Debug.Log("Jogador levado ao pé da escada. Clique no Game e segure W para subir.");
    }

    static string Caminho(Transform t)
    {
        string c = t.name;
        for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; c = t.name + "/" + c; }
        return c;
    }
}
#endif
