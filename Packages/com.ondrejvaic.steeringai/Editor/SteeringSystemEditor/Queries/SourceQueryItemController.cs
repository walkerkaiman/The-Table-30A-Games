using System;
using System.Collections.Generic;
using System.Reflection;
using SteeringAI.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteeringAI.Editor
{
    public class SourceQueryItemController
    {
        private SteeringSystemEditor editor;
        private readonly Label typeNameLabel;
        private readonly Button removeButton;
        private readonly Button addButton;
        private Type currentAuthoringType;
        private bool isTag;

        public SourceQueryItemController(VisualElement visualElement, SourceQueryController queriesController, SteeringSystemEditor editor) 
        {
            this.editor = editor;
            typeNameLabel = visualElement.Q<Label>("type-name-label");
            
            removeButton = visualElement.Q<Button>("remove-button");
            removeButton.clickable.clicked += () => queriesController.OnRemoveComponent(currentAuthoringType, isTag);
            
            addButton = visualElement.Q<Button>("add-button");
            addButton.clickable.clicked += () => queriesController.OnAddComponent(currentAuthoringType, isTag);
        }
        
        public void SetData(bool prefabIsNull, TypeDescription typeDescription, List<Type> authoringsList)
        {
            typeNameLabel.text = typeDescription.ToString();
            
            var componentType = Type.GetType(typeDescription.AssemblyQualifiedName);
            if(componentType == null || prefabIsNull)
            {
                removeButton.SetEnabled(false);
                addButton.SetEnabled(false);
                return;
            };
            
            isTag = componentType.GetCustomAttribute<SteeringEntityTagAttribute>() != null;
            if (isTag && editor.SteeringSystemAsset.Prefab != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(editor.SteeringSystemAsset.Prefab.gameObject);
                GameObject contentsRoot = PrefabUtility.LoadPrefabContents(assetPath);

                var tags = contentsRoot.GetComponent<SteeringEntityAuthoring>().Tags;
                var hasTag = tags.Contains(typeDescription);

                PrefabUtility.SaveAsPrefabAsset(contentsRoot, assetPath);
                PrefabUtility.UnloadPrefabContents(contentsRoot);
                
                currentAuthoringType = componentType;
                removeButton.SetEnabled(hasTag);
                addButton.SetEnabled(!hasTag);
                return;
            }
            
            currentAuthoringType = null;
            bool hasComponent = false;
            foreach (var authoringType in authoringsList)
            {
                var attribute = authoringType.GetCustomAttribute<ComponentAuthoringAttribute>();
                if (attribute == null) continue;
                if (attribute.TargetComponentType.AssemblyQualifiedName != typeDescription.AssemblyQualifiedName) continue;
                
                currentAuthoringType = authoringType;
                hasComponent = true;
                break;
            }
            
            removeButton.SetEnabled(hasComponent);
            addButton.SetEnabled(!hasComponent);
            
            if (!hasComponent)
            {
                var componentsToAuthorings = editor.ReflectionModel.GetComponentsToAuthorings();
                if (componentsToAuthorings.TryGetValue(componentType, out var authoring))
                {
                    currentAuthoringType = authoring;
                }
                else
                {
                    typeNameLabel.text += " (No Authoring)";
                    addButton.SetEnabled(false);
                }
            }
            
        }
    }
}