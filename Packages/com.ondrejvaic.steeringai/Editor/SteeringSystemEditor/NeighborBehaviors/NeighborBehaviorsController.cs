using System;
using System.Collections.Generic;
using SteeringAI.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteeringAI.Editor
{
    public class NeighborBehaviorsController
    {
        public readonly VisualElement RootVisualElement;
        private readonly SteeringSystemEditor editor;
        private readonly ListView listView;
        
        private SerializedProperty arrayProperty;

        public NeighborBehaviorsController(
            SteeringSystemEditor editor,
            VisualElement root)
        {
            this.editor = editor;
            this.RootVisualElement = root.Q<VisualElement>("neighbor-groups") ;

            var addGroupButton = RootVisualElement.Q<Button>("add-neighbor-behavior-group-button");
            addGroupButton.clickable.clicked += onAddGroup;

            listView = RootVisualElement.Q<ListView>("neighborhood-groups");
            createListView();
        }
        
        private void onAddGroup()
        {
            var defaultNeighborQueryRunner = new JobRunnerHolder<INeighborQueryJobWrapper>();
            var neighborQueryRunners = editor.ReflectionModel.GetNeighborQueryJobRunners();
            if (neighborQueryRunners.Count > 0)
            {
                var defaultRunner = (INeighborQueryJobWrapper)Activator.CreateInstance(Type.GetType(neighborQueryRunners[0].AssemblyQualifiedName)!);
                defaultNeighborQueryRunner = new JobRunnerHolder<INeighborQueryJobWrapper>(defaultRunner);
            }

            var neighborhoodBehaviorModel = new UINeighborhoodBehaviorGroupModel
            {
                Settings = NeighborhoodSettingsUI.Preset,
                BehaviorJobs = new List<JobRunnerHolder<INeighborBehaviorJobWrapper>>(),
                NeighborQueryJob = defaultNeighborQueryRunner,
                AdditionalTargetTypeDescs = new List<TypeDescription>()
            };
            
            editor.SteeringSystemAsset.NeighborBehaviorGroupInitDatas.Add(neighborhoodBehaviorModel);
            editor.SerializedSteeringSystemAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(editor.SteeringSystemAsset);
            listView.Rebuild();
        }

        void createListView()
        {
            listView.showBoundCollectionSize = false;
            listView.bindingPath = nameof(SteeringSystemAsset.NeighborBehaviorGroupInitDatas);
            listView.Bind(editor.SerializedSteeringSystemAsset);
            
            listView.makeItem = () =>
            {
                var groupItem = editor.NeighborBehaviorGroupItemTemplate.Instantiate();
                
                var controller = new NeighborBehaviorGroupItemController(
                    editor,
                    this,
                    groupItem);
                
                groupItem.userData = controller;
                
                return groupItem;
            };
            
            listView.bindItem = (behaviorGroupItem, behaviorGroupIndex) =>
            {
                ((NeighborBehaviorGroupItemController)behaviorGroupItem.userData).SetData(
                    editor.SteeringSystemAsset.NeighborBehaviorGroupInitDatas[behaviorGroupIndex],
                    listView.itemsSource[behaviorGroupIndex] as SerializedProperty,
                    behaviorGroupIndex);
            };
            
            arrayProperty = editor.SerializedSteeringSystemAsset.FindProperty(listView.bindingPath);
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