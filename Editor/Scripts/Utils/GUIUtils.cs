/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using UnityEngine;

namespace BinaryEgo.Voxelizer.Editor
{
    public class GUIUtils
    {
        public static GUISkin Skin => (GUISkin)Resources.Load("Skins/VoxelizerEditorSkin");
        
        // public static bool DrawMinimizableSectionTitle(string p_title, ref bool p_minimized)
        // {
        //     GUILayout.Label(p_title, Skin.GetStyle("section_title"), GUILayout.Height(26));
        //     var rect = GUILayoutUtility.GetLastRect();
        //     GUI.Label(new Rect(rect.x+rect.width- (p_minimized ? 24 : 21), rect.y, 24, 24), p_minimized ? "+" : "-", Skin.GetStyle("minimizebuttonbig"));
        //     
        //     if (GUI.Button(new Rect(rect.x, rect.y, rect.width, rect.height), "", GUIStyle.none))
        //     {
        //         p_minimized = !p_minimized;
        //     }
        //
        //     return !p_minimized;
        // }

        public static bool DrawMinimizableSectionTitle(string p_title, ref bool p_minimized, int? p_size = null, Color? p_color = null, TextAnchor? p_alignment = null)
        {
            
            var style = new GUIStyle();
            style.normal.textColor = p_color.HasValue ? p_color.Value : new Color(0.9f, 0.5f, 0);
            style.alignment = p_alignment.HasValue ? p_alignment.Value : TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;
            style.normal.background = Texture2D.whiteTexture;
            style.fontSize = p_size.HasValue ? p_size.Value : 12;
            GUI.backgroundColor = new Color(0, 0, 0, .5f);
            GUILayout.Label(p_title, style, GUILayout.Height(26));
            GUI.backgroundColor = Color.white;
            
            var rect = GUILayoutUtility.GetLastRect();

            style = new GUIStyle();
            style.fontSize = p_size.HasValue ? p_size.Value + 6 : 20;
            style.normal.textColor = p_color.HasValue ? p_color.Value*2/3 : new Color(.6f, 0.4f, 0);
            //style.normal.textColor = Color.white;

            GUI.Label(new Rect(rect.x + 6 + (p_minimized ? 0 : 2), rect.y + (p_size.HasValue ? 14 - p_size.Value : 0), 24, 24), p_minimized ? "+" : "-", style);
            
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