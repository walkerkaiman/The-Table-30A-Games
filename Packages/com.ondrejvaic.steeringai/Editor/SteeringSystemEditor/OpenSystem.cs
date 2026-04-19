using SteeringAI.Core;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace SteeringAI.Editor
{
    public class OpenSystem : UnityEditor.Editor
    {
        [MenuItem("Assets/Open Steering System Editor", true)]
        private static bool CanOpenInEditor()
        {
            string[] assetPaths = Selection.assetGUIDs;
            if (assetPaths.Length == 1)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetPaths[0]);
                var steeringSystemAsset = AssetDatabase.LoadAssetAtPath<SteeringSystemAsset>(assetPath);
                return steeringSystemAsset != null;
            }
            return false;
        }

        [MenuItem("Assets/Open Steering System Editor")]
        private static void OpenInEditor()
        {
            if(Selection.assetGUIDs.Length != 1) return;
            
            string assetPath = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
            
            var window = EditorWindow.GetWindow<SteeringSystemEditor>();
            window.titleContent = new GUIContent("Steering System Creator");
            window.minSize = new Vector2(980, 600);
            window.currentSteeringSystemPath = assetPath;
            window.Init();
        }
        
        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            Object target = EditorUtility.InstanceIDToObject(instanceID);

            if (target is SteeringSystemAsset)
            {
                Selection.activeObject = target;
                OpenInEditor();
                return true;
            }
 
            return false;
        }
    }
}
