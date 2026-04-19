using System;
using System.Collections.Generic;
using SteeringAI.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteeringAI.Editor
{
    public class RayBehaviorsController
    {
        public readonly VisualElement RootVisualElement;
        private readonly SteeringSystemEditor editor;
        private readonly ListView listView;

        private SerializedProperty arrayProperty;
        
        public RayBehaviorsController(
            SteeringSystemEditor editor,
            VisualElement root)
        {
            this.editor = editor;
            this.RootVisualElement = root.Q<VisualElement>("ray-groups");
            
            var addGroupButton = RootVisualElement.Q<Button>("add-ray-behavior-group-button");
            addGroupButton.clickable.clicked += onAddGroup;
            
            listView = RootVisualElement.Q<ListView>("ray-groups");
            createListView();   
        }

        private void createListView()
        {
            listView.showBoundCollectionSize = false;
            listView.bindingPath = nameof(SteeringSystemAsset.RaycastBehaviorGroupInitDatas);
            listView.Bind(editor.SerializedSteeringSystemAsset);
            
            listView.makeItem = () =>
            {
                var groupItem = editor.RayBehaviorGroupItemTemplate.Instantiate();
                
                var controller = new RayBehaviorGroupItemController(editor, this, groupItem);
                groupItem.userData = controller;
                
                return groupItem;
            };
            
            listView.bindItem = (behaviorGroupItem, behaviorGroupIndex) =>
            {
                ((RayBehaviorGroupItemController)behaviorGroupItem.userData).SetData(
                    listView.itemsSource[behaviorGroupIndex] as SerializedProperty,
                    behaviorGroupIndex);
            };
            
            arrayProperty = editor.SerializedSteeringSystemAsset.FindProperty(listView.bindingPath);
        }

        private void onAddGroup()
        {
            var defaultRaysRunner = new JobRunnerHolder<ICreateRaysJobWrapper>();
            var createRayRunners = editor.ReflectionModel.GetCreateRayJobRunners();
            if (createRayRunners.Count > 0)
            {
                var defaultRunner = (ICreateRaysJobWrapper)Activator.CreateInstance(Type.GetType(createRayRunners[0].AssemblyQualifiedName)!);
                defaultRaysRunner = new JobRunnerHolder<ICreateRaysJobWrapper>(defaultRunner);
            }

            var neighborhoodBehaviorModel = new UIRaycastBehaviorGroupModel
            {
                BehaviorJobs = new List<JobRunnerHolder<IRaycastBehaviorJobWrapper>>(),
                RayQueryJob = defaultRaysRunner,
                RaycastSettings = RaycastSettingsUI.Preset
            };
            
            editor.SteeringSystemAsset.RaycastBehaviorGroupInitDatas.Add(neighborhoodBehaviorModel);
            editor.SerializedSteeringSystemAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(editor.SteeringSystemAsset);
            listView.Rebuild();
        }

        public void DeleteBehaviorGroup(int behaviorGroupIndex)
        {
            arrayProperty.DeleteArrayElementAtIndex(behaviorGroupIndex);
            editor.SerializedSteeringSystemAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(editor.SteeringSystemAsset);
            editor.RefreshRequiredQueries();
        }

        public void RefreshTypeDropDowns()
        {
            listView.Rebuild();
        }
    }
    
}