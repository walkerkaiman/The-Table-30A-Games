using System;
using System.Collections.Generic;
using SteeringAI.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SteeringAI.Editor
{
    public class SourceQueryController
    {
        private readonly SteeringSystemEditor editor;
        private readonly ListView allRequiredListView;

        private readonly List<Type> authoringList = new();

        public SourceQueryController(
            SteeringSystemEditor editor,
            VisualElement rootVisualElement)
        {
            this.editor = editor;
            var queriesRoot = rootVisualElement.Q("queries-parent").Q("all-parent");

            allRequiredListView = queriesRoot.Q<ListView>("all-required-list-view");
            allRequiredListView.makeItem = () =>
            {
                var queryItem = editor.RequiredQueryItemTemplate.Instantiate();
                var controller = new SourceQueryItemController(queryItem, this, editor);
                queryItem.userData = controller;
                return queryItem;
            };
            
            allRequiredListView.bindItem = (queryItem, queryIndex) =>
            {
                bool prefabIsNull = editor.SteeringSystemAsset.Prefab == null;
                ((SourceQueryItemController)queryItem.userData).SetData(prefabIsNull, editor.SteeringSystemAsset.RequiredSourceTypesList[queryIndex], authoringList);
            };
            
            allRequiredListView.itemsSource = editor.SteeringSystemAsset.RequiredSourceTypesList;
            PrefabUtility.prefabInstanceUpdated += OnPrefabChanged;
            RefreshRequiredQueries();
        }

        private void OnPrefabChanged(GameObject instance)
        {
            if (instance == editor.SteeringSystemAsset.Prefab.gameObject)
            {
                RefreshRequiredQueries();
            }
        }

        public void RefreshRequiredQueries()
        {
            authoringList.Clear();
            if(editor.SteeringSystemAsset.Prefab != null)
            {
                var components = editor.SteeringSystemAsset.Prefab.GetComponents<Component>();
                if (components != null)
                {
                    foreach (var component in components)
                    {
                        if (component != null)
                        {
                            authoringList.Add(component.GetType());   
                        }
                    }
                }
            }
            editor.SteeringSystemAsset.UpdateRequiredTypesList();
            allRequiredListView.Rebuild();
        }

        public void OnRemoveComponent(Type currentAuthoringType, bool isTag)
        {
            string assetPath = AssetDatabase.GetAssetPath(editor.SteeringSystemAsset.Prefab.gameObject);
            GameObject contentsRoot = PrefabUtility.LoadPrefabContents(assetPath);

            if (isTag)
            {
                var steeringEntity = contentsRoot.GetComponent<SteeringEntityAuthoring>();
                steeringEntity.Tags.Remove(new TypeDescription(currentAuthoringType));
                EditorUtility.SetDirty(steeringEntity);
            }
            else
            {
                Object.DestroyImmediate(contentsRoot.GetComponent(currentAuthoringType));   
            }

            PrefabUtility.SaveAsPrefabAsset(contentsRoot, assetPath);
            PrefabUtility.UnloadPrefabContents(contentsRoot);
            
            RefreshRequiredQueries();
        }

        public void OnAddComponent(Type currentAuthoringType, bool isTag)
        {;
            string assetPath = AssetDatabase.GetAssetPath(editor.SteeringSystemAsset.Prefab.gameObject);
            GameObject contentsRoot = PrefabUtility.LoadPrefabContents(assetPath);
            
            if (isTag)
            {
                contentsRoot.GetComponent<SteeringEntityAuthoring>().Tags.Add(new TypeDescription(currentAuthoringType));
            }
            else
            {
                ObjectFactory.AddComponent(contentsRoot, currentAuthoringType);
            }

            PrefabUtility.SaveAsPrefabAsset(contentsRoot, assetPath);
            PrefabUtility.UnloadPrefabContents(contentsRoot);
            
            RefreshRequiredQueries();
        }
    }
}