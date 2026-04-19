using System;
using SteeringAI.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteeringAI.Editor
{
    public class SimpleBehaviorsController
    {
        public readonly VisualElement RootVisualElement;
        private readonly PopupField<TypeDescription> behaviorsDropdown;
        private readonly Button addBehaviorButton;
        private readonly SteeringSystemEditor editor;
        private readonly ListView listView;
        private readonly SerializedProperty arrayProperty;

        public SimpleBehaviorsController(
            SteeringSystemEditor editor,
            VisualElement root)
        {
            this.editor = editor;
            this.RootVisualElement = root.Q<VisualElement>("per-component-groups") ;

            behaviorsDropdown = SteeringSystemEditor.CreatePopupField<TypeDescription>(RootVisualElement, "per-component-behavior-dropdown-parent", "Behaviors:");
            behaviorsDropdown.formatSelectedValueCallback = v => v.ToString();
            behaviorsDropdown.formatListItemCallback = v => v.ToString();
            
            addBehaviorButton = RootVisualElement.Q<Button>("add-per-component-behavior-button");
            addBehaviorButton.clickable.clicked += onAddBehavior;

            listView = RootVisualElement.Q<ListView>("per-component-behavior-list-view");

            listView.showBoundCollectionSize = false;
            listView.bindingPath = nameof(SteeringSystemAsset.SimpleBehaviorsList);
            listView.Bind(editor.SerializedSteeringSystemAsset);
            
            listView.makeItem = () =>
            {
                var behaviorItem = editor.SimpleBehaviorItemTemplate.Instantiate();

                var controller = new SimpleBehaviorItemController(behaviorItem, this);
                behaviorItem.userData = controller;
                
                return behaviorItem;
            };
            
            listView.bindItem = (behaviorItem, behaviorIndex) =>
            {
                ((SimpleBehaviorItemController)behaviorItem.userData).SetData(
                    listView.itemsSource[behaviorIndex] as SerializedProperty,
                    behaviorIndex);
            };
            
            arrayProperty = editor.SerializedSteeringSystemAsset.FindProperty(listView.bindingPath);
            
            updateDropDown();
        }
        
        private void onAddBehavior()
        {
            var newBehavior = Activator.CreateInstance(Type.GetType(behaviorsDropdown.value.AssemblyQualifiedName)!);
            
            editor.SteeringSystemAsset.SimpleBehaviorsList.Add(
                new JobRunnerHolder<ISimpleBehaviorJobWrapper>((ISimpleBehaviorJobWrapper)newBehavior));
            
            editor.SerializedSteeringSystemAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(editor.SteeringSystemAsset);
            listView.Rebuild();
            updateDropDown();
            editor.RefreshRequiredQueries();
        }

        private void updateDropDown()
        {
            behaviorsDropdown.choices = editor.ReflectionModel.GetPossibleSimpleTypes();
            
            if (behaviorsDropdown.choices.Count > 0)
                behaviorsDropdown.index = 0;
            else
                behaviorsDropdown.value = TypeDescription.Empty;
            
            addBehaviorButton.SetEnabled(behaviorsDropdown.choices.Count > 0);
        }
        
        public void RemoveBehavior(int behaviorIndex)
        {
            arrayProperty.DeleteArrayElementAtIndex(behaviorIndex);
            editor.SerializedSteeringSystemAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(editor.SteeringSystemAsset);
            updateDropDown();
            editor.RefreshRequiredQueries();
        }

        public void RefreshTypeDropDowns()
        {
            listView.Rebuild();
        }
    }
}