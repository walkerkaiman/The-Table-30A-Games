using System;
using SteeringAI.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteeringAI.Editor
{
    public class NeighborBehaviorGroupItemController
    {
        private readonly ListView neighborBehaviorListView;
        private readonly PopupField<TypeDescription> behaviorsDropdown;
        private readonly PopupField<TypeDescription> neighborQueryDropdown;
        private readonly PropertyField neighborQueryJobField;

        private readonly TargetQueriesController allComponentsQueriesController;
        private readonly TargetQueriesController noneComponentsQueriesController;
        private readonly TargetQueriesController anyComponentsQueriesController;

        private readonly ListView allRequiredComponentsQueriesListView;
        private readonly Button addBehaviorButton;
        
        private readonly SteeringSystemEditor editor;
        private readonly VisualElement visualElement;
        
        private int behaviorGroupIndex;
        private SerializedProperty behaviorArrayProperty;

        public NeighborBehaviorGroupItemController(
            SteeringSystemEditor editor,
            NeighborBehaviorsController neighborBehaviorsController,
            VisualElement visualElement)
        {
            this.visualElement = visualElement;
            this.editor = editor;

            var settings = visualElement.Q<PropertyField>("settings-field");
            settings.bindingPath = nameof(UINeighborhoodBehaviorGroupModel.Settings);
            
            neighborBehaviorListView = visualElement.Q<ListView>("neighborhood-behaviors");
            neighborBehaviorListView.showBoundCollectionSize = false;
            neighborBehaviorListView.bindingPath = nameof(UINeighborhoodBehaviorGroupModel.BehaviorJobs);
            neighborBehaviorListView.selectionType = SelectionType.Single;

            behaviorsDropdown = SteeringSystemEditor.CreatePopupField<TypeDescription>(visualElement, "behaviors-drop-down", "Behaviors");
            behaviorsDropdown.formatSelectedValueCallback = v => v.Name;

            neighborQueryDropdown = SteeringSystemEditor.CreatePopupField<TypeDescription>(visualElement, "neighbor-query-drop-down", null);
            neighborQueryDropdown.formatSelectedValueCallback = v => v.Name;
            neighborQueryDropdown.formatListItemCallback = v => v.Name;
            neighborQueryDropdown.choices = editor.ReflectionModel.GetNeighborQueryJobRunners();
            neighborQueryDropdown.RegisterValueChangedCallback(onNeighborQueryChanged);
            
            neighborQueryJobField = visualElement.Q<PropertyField>("neighbor-query-job-field");
            neighborQueryJobField.bindingPath = nameof(UINeighborhoodBehaviorGroupModel.NeighborQueryJob) + "." + nameof(UINeighborhoodBehaviorGroupModel.NeighborQueryJob.JobRunner);
            
            allComponentsQueriesController = new TargetQueriesController(
                visualElement.Q<VisualElement>("all-parent"),
                editor,
                editor.QueryItemTemplate);

            allRequiredComponentsQueriesListView = visualElement.Q<ListView>("all-required-list-view");
            allRequiredComponentsQueriesListView.SetEnabled(false);
            
            var deleteGroupButton = visualElement.Q<Button>("delete-group-button");
            deleteGroupButton.clickable.clicked += () =>
            {
                neighborBehaviorsController.DeleteBehaviorGroup(behaviorGroupIndex);
            };
            
            addBehaviorButton = visualElement.Q<Button>("add-behavior-button");
            addBehaviorButton.clickable.clicked += () =>
            {
                addBehavior(behaviorsDropdown.value);
            };
        }

        private void onNeighborQueryChanged(ChangeEvent<TypeDescription> evt)
        {
            // first trigger of the event
            if(evt.previousValue.Name == null) return;
            
            var jobInstance = (INeighborQueryJobWrapper)Activator.CreateInstance(Type.GetType(evt.newValue.AssemblyQualifiedName)!);
            var newGroup = editor.SteeringSystemAsset.NeighborBehaviorGroupInitDatas[behaviorGroupIndex];
            newGroup.NeighborQueryJob = new JobRunnerHolder<INeighborQueryJobWrapper>(jobInstance);
            editor.SteeringSystemAsset.NeighborBehaviorGroupInitDatas[behaviorGroupIndex] = newGroup;
            editor.SerializedSteeringSystemAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(editor.SteeringSystemAsset);
            editor.RefreshRequiredQueries();
            refreshAllRequired();
        }

        private void updateNeighborQueryDropDown()
        {
            Type currentNeighborQueryType = editor.SteeringSystemAsset.NeighborBehaviorGroupInitDatas[behaviorGroupIndex].NeighborQueryJob.JobRunner?.GetType();
            
            if(currentNeighborQueryType == null)
            {
                neighborQueryDropdown.value = TypeDescription.Empty;
                return;
            }
            
            var currentNeighborQuery = new TypeDescription(currentNeighborQueryType);

            int index = neighborQueryDropdown.choices.IndexOf(currentNeighborQuery);
            
            if (neighborQueryDropdown.choices.Count > 0)
            {
                neighborQueryDropdown.index = index >= 0 ? index : 0;
            }
            else
            {
                neighborQueryDropdown.value = TypeDescription.Empty;   
            }
        }

        private void updateBehaviorsDropDown()
        {
            behaviorsDropdown.choices = editor.ReflectionModel.GetPossibleNeighborJobRunners(behaviorGroupIndex);
            if (behaviorsDropdown.choices.Count > 0)
                behaviorsDropdown.index = 0;
            else
                behaviorsDropdown.value = TypeDescription.Empty;
            
            addBehaviorButton.SetEnabled(behaviorsDropdown.choices.Count > 0);
        }

        public void RemoveBehavior(int behaviorIndex)
        {
            behaviorArrayProperty.DeleteArrayElementAtIndex(behaviorIndex);
            editor.SerializedSteeringSystemAsset.ApplyModifiedProperties();
            updateBehaviorsDropDown();
            editor.RefreshRequiredQueries();
            refreshAllRequired();
        }
        
        private void addBehavior(TypeDescription behaviorsDropdownValue)
        {
            UINeighborhoodBehaviorGroupModel neighborhoodBehaviorGroupModel = editor.SteeringSystemAsset.NeighborBehaviorGroupInitDatas[behaviorGroupIndex];
            var behavior = Activator.CreateInstance(Type.GetType(behaviorsDropdownValue.AssemblyQualifiedName)!);
            neighborhoodBehaviorGroupModel.BehaviorJobs.Add(new JobRunnerHolder<INeighborBehaviorJobWrapper>((INeighborBehaviorJobWrapper)behavior));
            editor.SerializedSteeringSystemAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(editor.SteeringSystemAsset);
            updateBehaviorsDropDown();
            neighborBehaviorListView.Rebuild();
            editor.RefreshRequiredQueries();
            refreshAllRequired();
        }

        public void SetData(UINeighborhoodBehaviorGroupModel neighborhoodBehaviorGroupModel, SerializedProperty serializedProperty, int behaviorGroupIndex)
        {
            this.behaviorGroupIndex = behaviorGroupIndex;
            
            var field = visualElement as IBindable;
            field.bindingPath = serializedProperty.propertyPath;
            
            visualElement.Bind(serializedProperty.serializedObject);

            neighborQueryJobField.BindProperty(serializedProperty.FindPropertyRelative(nameof(UINeighborhoodBehaviorGroupModel.NeighborQueryJob) + "." + nameof(UINeighborhoodBehaviorGroupModel.NeighborQueryJob.JobRunner)));

            neighborBehaviorListView.Bind(serializedProperty.serializedObject);
            neighborBehaviorListView.makeItem = () =>
            {
                var groupItem = editor.NeighborBehaviorItemTemplate.Instantiate();
                
                var controller = new NeighborBehaviorItemController(groupItem, this);
                groupItem.userData = controller;
                
                return groupItem;
            };
            
            neighborBehaviorListView.bindItem = (behaviorItem, behaviorIndex) =>
            {
                if(editor.SteeringSystemAsset.NeighborBehaviorGroupInitDatas[behaviorGroupIndex].BehaviorJobs.Count <= behaviorIndex) return;
                
                ((NeighborBehaviorItemController)behaviorItem.userData).SetData(
                    neighborBehaviorListView.itemsSource[behaviorIndex] as SerializedProperty,
                    behaviorIndex);
            };
            
            behaviorArrayProperty = editor.SerializedSteeringSystemAsset.FindProperty(nameof(SteeringSystemAsset.NeighborBehaviorGroupInitDatas) + $".Array.data[{this.behaviorGroupIndex}].{nameof(UINeighborhoodBehaviorGroupModel.BehaviorJobs)}");
            
            updateBehaviorsDropDown();
            updateNeighborQueryDropDown();

            allComponentsQueriesController.SetData(behaviorGroupIndex);
            
            allRequiredComponentsQueriesListView.makeItem = () =>
            {
                var queryItem = editor.QueryItemTemplate.Instantiate();
                queryItem.Q<Button>("remove-button").RemoveFromHierarchy();
                queryItem.userData = queryItem.Q<Label>("type-name-label");
                return queryItem;
            };
            
            allRequiredComponentsQueriesListView.bindItem = (queryItem, queryIndex) =>
            {
                var typeDescription = (TypeDescription)allRequiredComponentsQueriesListView.itemsSource[queryIndex];
                ((Label)queryItem.userData).text = typeDescription.Name;
            };
            
            allRequiredComponentsQueriesListView.itemsSource = neighborhoodBehaviorGroupModel.GetRequiredTargetComponentQueryTypes();
        }

        private void refreshAllRequired()
        {
            allRequiredComponentsQueriesListView.itemsSource = editor.SteeringSystemAsset.NeighborBehaviorGroupInitDatas[behaviorGroupIndex].GetRequiredTargetComponentQueryTypes();
            allRequiredComponentsQueriesListView.Rebuild();
        }
    }
}