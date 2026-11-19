#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Menu  ISLAND > Criar Perfil Gráfico Leve
/// Cria uma cópia mais leve da configuração gráfica do jogo (para computadores fracos):
/// renderização Forward simples, sem SSAO, sem HDR, sem cópias de profundidade/cor,
/// sombras mais baratas e menos efeitos. O jogo usa esse perfil nas qualidades Média e Baixa.
/// Os arquivos originais (PC_RPAsset e PC_Renderer) NÃO são alterados.
/// </summary>
public static class CriarPerfilLeve
{
    const string AssetOriginal = "Assets/Settings/PC_RPAsset.asset";
    const string RendererOriginal = "Assets/Settings/PC_Renderer.asset";

    const string Pasta = "Assets/Resources/PerfilLeve";
    const string AssetLeve = Pasta + "/RP_Leve.asset";
    const string RendererLeve = Pasta + "/Renderer_Leve.asset";

    [MenuItem("ISLAND/Criar Perfil Gráfico Leve")]
    static void Criar()
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(AssetOriginal) == null ||
            AssetDatabase.LoadAssetAtPath<Object>(RendererOriginal) == null)
        {
            EditorUtility.DisplayDialog("Perfil Leve",
                "Não achei " + AssetOriginal + " ou " + RendererOriginal + ".", "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<Object>(AssetLeve) != null)
        {
            if (!EditorUtility.DisplayDialog("Perfil Leve",
                    "O perfil leve já existe. Recriar do zero?", "Recriar", "Cancelar"))
                return;

            AssetDatabase.DeleteAsset(AssetLeve);
            AssetDatabase.DeleteAsset(RendererLeve);
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Pasta))
            AssetDatabase.CreateFolder("Assets/Resources", "PerfilLeve");

        // ---------- Renderer leve ----------
        AssetDatabase.CopyAsset(RendererOriginal, RendererLeve);
        AssetDatabase.ImportAsset(RendererLeve);

        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererLeve);
        if (renderer == null)
        {
            Debug.LogError("Não consegui criar o renderer leve.");
            return;
        }

        renderer.renderingMode = RenderingMode.Forward; // mais simples que Forward+

        foreach (var efeito in renderer.rendererFeatures)
        {
            if (efeito != null) efeito.SetActive(false); // desliga SSAO e afins
        }

        EditorUtility.SetDirty(renderer);

        // ---------- Asset de pipeline leve ----------
        AssetDatabase.CopyAsset(AssetOriginal, AssetLeve);
        AssetDatabase.ImportAsset(AssetLeve);

        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetLeve);
        if (asset == null)
        {
            Debug.LogError("Não consegui criar o asset de pipeline leve.");
            return;
        }

        var so = new SerializedObject(asset);

        // Usa o renderer leve
        var lista = so.FindProperty("m_RendererDataList");
        if (lista != null && lista.arraySize > 0)
        {
            lista.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            var indice = so.FindProperty("m_DefaultRendererIndex");
            if (indice != null) indice.intValue = 0;
        }
        else
        {
            Debug.LogWarning("Não achei a lista de renderers no asset leve.");
        }

        Definir(so, "m_RequireDepthTexture", 0);          // só o SSAO usava
        Definir(so, "m_RequireOpaqueTexture", 0);
        Definir(so, "m_SupportsHDR", 0);
        Definir(so, "m_EnableLODCrossFade", 0);
        Definir(so, "m_SupportsLightLayers", 0);
        Definir(so, "m_SupportsLightCookies", 0);
        Definir(so, "m_UseAdaptivePerformance", 0);
        Definir(so, "m_SupportDataDrivenLensFlare", 0);
        Definir(so, "m_SupportScreenSpaceLensFlare", 0);

        Definir(so, "m_MainLightShadowmapResolution", 1024);
        Definir(so, "m_ShadowCascadeCount", 1);
        Definir(so, "m_SoftShadowsSupported", 0);
        Definir(so, "m_AdditionalLightShadowsSupported", 0);
        Definir(so, "m_AdditionalLightsPerObjectLimit", 2);
        Definir(so, "m_ReflectionProbeBlending", 0);
        Definir(so, "m_ReflectionProbeBoxProjection", 0);

        Definir(so, "m_ColorGradingMode", 0);              // LDR
        Definir(so, "m_ColorGradingLutSize", 16);

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Perfil gráfico leve criado em " + Pasta + ". As qualidades Média e Baixa do jogo agora usam esse perfil.");
        EditorUtility.DisplayDialog("Perfil Leve",
            "Perfil gráfico leve criado!\n\nAs qualidades Média e Baixa passam a usar esse perfil (sem SSAO, sem HDR, renderização mais simples).",
            "OK");
    }

    static void Definir(SerializedObject so, string nome, int valor)
    {
        var p = so.FindProperty(nome);
        if (p == null)
        {
            Debug.LogWarning("Propriedade não encontrada no perfil leve: " + nome);
            return;
        }

        switch (p.propertyType)
        {
            case SerializedPropertyType.Boolean: p.boolValue = valor != 0; break;
            default: p.intValue = valor; break;
        }
    }
}
#endif
