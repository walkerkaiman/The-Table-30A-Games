using System;
using SteeringAI.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteeringAI.Editor
{
    public class RayBehaviorGroupItemController
    {
        private readonly Button addBehaviorButton;
        private readonly Label settings;
        private readonly ListView listView;
        private readonly PopupField<TypeDescription> behaviorsDropdown;
        private readonly PopupField<TypeDescription> createRayDropdown;
        private readonly VisualElement visualElement;
        private readonly PropertyField rayCreationJobField;
        
        private readonly SteeringSystemEditor editor;

        private int behaviorGroupIndex;
        private SerializedProperty behaviorArrayProperty;

        public RayBehaviorGroupItemController(
            SteeringSystemEditor editor,
            RayBehaviorsController rayBehaviorsController,
            VisualElement visualElement)
        {
            this.editor = editor;
            this.visualElement = visualElement;
            
            listView = visualElement.Q<ListView>("ray-behaviors");
            listView.showBoundCollectionSize = false;
            listView.bindingPath = nameof(UIRaycastBehaviorGroupModel.BehaviorJobs);
            listView.selectionType = SelectionType.Single;
            
            behaviorsDropdown = SteeringSystemEditor.CreatePopupField<TypeDescription>(visualElement, "behaviors-drop-down", "Behaviors");
            behaviorsDropdown.formatSelectedValueCallback = v => v.Name;
            behaviorsDropdown.formatListItemCallback = v => v.ToString();
            
            createRayDropdown = SteeringSystemEditor.CreatePopupField<TypeDescription>(visualElement, "create-ray-drop-down", null);
            createRayDropdown.formatSelectedValueCallback = v => v.Name;
            createRayDropdown.formatListItemCallback = v => v.Name;
            createRayDropdown.choices = editor.ReflectionModel.GetCreateRayJobRunners();
            createRayDropdown.RegisterValueChangedCallback(onCreateRayChanged);
            
            rayCreationJobField = visualElement.Q<PropertyField>("ray-creation-job-field");
            rayCreationJobField.bindingPath = nameof(UIRaycastBehaviorGroupModel.RayQueryJob) + "." + nameof(UIRaycastBehaviorGroupModel.RayQueryJob.JobRunner);

            var baseRaySettingsField = visualElement.Q<PropertyField>("base-ray-settings-field");
            baseRaySettingsField.bindingPath = nameof(UIRaycastBehaviorGroupModel.RaycastSettings);
                
            var deleteGroupButton = visualElement.Q<Button>("delete-group-button");
            deleteGroupButton.clickable.clicked += () =>
            {
                rayBehaviorsController.DeleteBehaviorGroup(behaviorGroupIndex);
            };
            
            addBehaviorButton = visualElement.Q<Button>("add-behavior-button");
            addBehaviorButton.clickable.clicked += () =>
            {
                addBehavior(behaviorsDropdown.value);
            };
        }

        private void onCreateRayChanged(ChangeEvent<TypeDescription> evt)
        {
            // first trigger of the event
            if(evt.previousValue.Name == null) return;
            
            var jobInstance = (ICreateRaysJobWrapper)Activator.CreateInstance(Type.GetType(evt.newValue.AssemblyQualifiedName)!);
            var newGroup = editor.SteeringSystemAsset.RaycastBehaviorGroupInitDatas[behaviorGroupIndex];
            newGroup.RayQueryJob = new JobRunnerHolder<ICreateRaysJobWrapper>(jobInstance);
            editor.SteeringSystemAsset.RaycastBehaviorGroupInitDatas[behaviorGroupIndex] = newGroup;
            editor.SerializedSteeringSystemAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(editor.SteeringSystemAsset);
        }

        private void addBehavior(TypeDescription behaviorsDropdownValue)
        {
            UIRaycastBehaviorGroupModel rayBehaviorGroupModel = editor.SteeringSystemAsset.RaycastBehaviorGroupInitDatas[behaviorGroupIndex];
            var behavior = Activator.CreateInstance(Type.GetType(behaviorsDropdownValue.AssemblyQualifiedName)!);
            rayBehaviorGroupModel.BehaviorJobs.Add(new JobRunnerHolder<IRaycastBehaviorJobWrapper>((IRaycastBehaviorJobWrapper)behavior));
            editor.SerializedSteeringSystemAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(editor.SteeringSystemAsset);
            updateBehaviorsDropDown();
            listView.Rebuild();
            editor.RefreshRequiredQueries();
        }

        private void updateCreatedRaysDropDown()
        {
            Type currentCreateRayType = editor.SteeringSystemAsset.RaycastBehaviorGroupInitDatas[behaviorGroupIndex].RayQueryJob.JobRunner?.GetType();
            
            if(currentCreateRayType == null)
            {
                createRayDropdown.value = TypeDescription.Empty;
                return;
            }
            
            var currentCreateRay = new TypeDescription(currentCreateRayType);
            
            int index = createRayDropdown.choices.IndexOf(currentCreateRay);
            if (createRayDropdown.choices.Count > 0)
            {
                createRayDropdown.index = index >= 0 ? index : 0;
            }
            else
            {
                createRayDropdown.value = TypeDescription.Empty;   
            }
        }

        private void updateBehaviorsDropDown()
        {
            behaviorsDropdown.choices = editor.ReflectionModel.GetPossibleRayJobRunners(behaviorGroupIndex);
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
            EditorUtility.SetDirty(editor.SteeringSystemAsset);
            updateBehaviorsDropDown();
            editor.RefreshRequiredQueries();
        }

        public void SetData(SerializedProperty serializedProperty, int behaviorGroupIndex)
        {
            this.behaviorGroupIndex = behaviorGroupIndex;

            var field = visualElement as IBindable;
            field.bindingPath = serializedProperty.propertyPath;
            
            visualElement.Bind(serializedProperty.serializedObject);
            
            rayCreationJobField.BindProperty(serializedProperty.FindPropertyRelative(nameof(UIRaycastBehaviorGroupModel.RayQueryJob) + "." + nameof(UIRaycastBehaviorGroupModel.RayQueryJob.JobRunner)));
            
            listView.Bind(serializedProperty.serializedObject);
            listView.makeItem = () =>
            {
                var groupItem = editor.NeighborBehaviorItemTemplate.Instantiate();
                
                var controller = new RayBehaviorItemController(groupItem, this);
                
                groupItem.userData = controller;
                
                return groupItem;
            };
            
            listView.bindItem = (behaviorItem, behaviorIndex) =>
            {
                if(editor.SteeringSystemAsset.RaycastBehaviorGroupInitDatas[behaviorGroupIndex].BehaviorJobs.Count <= behaviorIndex) return;
                
                ((RayBehaviorItemController)behaviorItem.userData).SetData(
                    listView.itemsSource[behaviorIndex] as SerializedProperty,
                    behaviorIndex);
            };
            
            behaviorArrayProperty = editor.SerializedSteeringSystemAsset.FindProperty(nameof(SteeringSystemAsset.RaycastBehaviorGroupInitDatas) + $".Array.data[{this.behaviorGroupIndex}].{nameof(UIRaycastBehaviorGroupModel.BehaviorJobs)}");
            
            updateBehaviorsDropDown();
            updateCreatedRaysDropDown();
        }
    }
}