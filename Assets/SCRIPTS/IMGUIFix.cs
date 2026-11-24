using UnityEngine;

/// <summary>
/// Conserto para textos desenhados com OnGUI: sem isso, quando o mouse passa em cima de um texto,
/// o Unity troca a cor dele (usa a cor do estado "hover"). Esta função pinta TODOS os estados da mesma cor.
/// </summary>
public static class IMGUIFix
{
    public static void Cor(GUIStyle e, Color c)
    {
        e.normal.textColor = c;
        e.hover.textColor = c;
        e.active.textColor = c;
        e.focused.textColor = c;
        e.onNormal.textColor = c;
        e.onHover.textColor = c;
        e.onActive.textColor = c;
        e.onFocused.textColor = c;
    }
}
