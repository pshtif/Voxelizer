/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using UnityEngine;

namespace Plugins.Voxelizer.Editor.Scripts
{
    public class GUIUtils
    {
        public static GUISkin Skin => (GUISkin)Resources.Load("Skins/VoxelizerEditorSkin");
        
        public static bool DrawMinimizableSectionTitle(string p_title, ref bool p_minimized)
        {
            GUILayout.Label(p_title, Skin.GetStyle("section_title"), GUILayout.Height(26));
            var rect = GUILayoutUtility.GetLastRect();
            GUI.Label(new Rect(rect.x+rect.width- (p_minimized ? 24 : 21), rect.y, 24, 24), p_minimized ? "+" : "-", Skin.GetStyle("minimizebuttonbig"));
            
            if (GUI.Button(new Rect(rect.x, rect.y, rect.width, rect.height), "", GUIStyle.none))
            {
                p_minimized = !p_minimized;
            }

            return !p_minimized;
        }

        public static bool DrawButton(string p_string)
        {
            bool clicked = false;
            GUIStyle style = new GUIStyle("button");
            style.fontSize = 12;
            style.fontStyle = FontStyle.Bold;
            
            GUI.color = new Color(0.9f, .5f, 0);
            
            if (GUILayout.Button(p_string, style, GUILayout.Height(32)))
            {
                clicked = true;
            }
            
            GUI.color = Color.white;

            return clicked;
        }
    }
}