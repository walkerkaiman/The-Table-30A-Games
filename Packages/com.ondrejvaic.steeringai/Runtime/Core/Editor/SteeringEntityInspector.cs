using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteeringAI.Core.Editor
{
    [CustomEditor(typeof(SteeringEntityAuthoring))]
    public class SteeringEntityInspector : UnityEditor.Editor
    {
        public VisualTreeAsset InspectorXML;
        public VisualTreeAsset QueryItemTemplate;
        
        private Button addTagButton;
        private PopupField<TypeDescription> componentsDropdown;
        private ListView allListView;
        
        private List<TypeDescription> allPossibleTags;

        public override VisualElement CreateInspectorGUI()
        {
            if (InspectorXML == null) return null;
            
            // Create a new VisualElement to be the root of our inspector UI
            VisualElement root = new VisualElement();
            InspectorXML.CloneTree(root);
            
            var radiusField = root.Q<PropertyField>("radius-field");
            radiusField.bindingPath = nameof(SteeringEntityAuthoring.Radius);
            
            var simulateField = root.Q<PropertyField>("simulate-field");
            simulateField.bindingPath = nameof(SteeringEntityAuthoring.Simulate);

            addTagButton = root.Q<Button>("add-query-component-button");
            addTagButton.clickable.clicked += onAddComponent;
            initTagTypes();
            
            allListView = root.Q<ListView>("queries-list-view");
            allListView.showBoundCollectionSize = false;
            allListView.bindingPath = nameof(SteeringEntityAuthoring.Tags);
            allListView.Bind(serializedObject);
            
            allListView.makeItem = () =>
            {
                var queryItem = QueryItemTemplate.Instantiate(); 
                
                var newListEntryLogic = new SourceQueryItemController(queryItem, this);
                queryItem.userData = newListEntryLogic;
            
                return queryItem;
            };
            
            allListView.bindItem = (queryItem, queryItemIndex) =>
            {
                ((SourceQueryItemController)queryItem.userData).SetData(
                    allListView.itemsSource[queryItemIndex] as SerializedProperty, queryItemIndex);
            };
            
            componentsDropdown = new PopupField<TypeDescription>
            {
                label = "Tags:",
                formatSelectedValueCallback = v => v.Name
            };

            root.Q<VisualElement>("drop-down-parent").Add(componentsDropdown);
            updateDropDown();

            return root;
        }

        private void onAddComponent()
        {
            serializedObject.Update();
            (target as SteeringEntityAuthoring)?.Tags.Add(componentsDropdown.value);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            updateDropDown();
        }

        private void initTagTypes()
        {
            allPossibleTags = (
                from assembly in AppDomain.CurrentDomain.GetAssemblies()
                    from type in assembly.GetTypes()
                        let targetAttribute = type.GetCustomAttribute<SteeringEntityTagAttribute>()
                        where targetAttribute != null && type.GetFields().Length == 0 
                            select new TypeDescription(type)
            ).AsParallel().ToList();
        }

        private List<TypeDescription> getPossibleTags()
        {
            var tags = (target as SteeringEntityAuthoring)?.Tags;
            if(tags == null)
                return allPossibleTags;
            
            var possibleTags = (
                from type in allPossibleTags
                    where !tags.Exists(x => x.AssemblyQualifiedName == type.AssemblyQualifiedName)
                        select type).AsParallel().ToList();
            
            return possibleTags;
        }

        private void updateDropDown()
        {
            componentsDropdown.choices = getPossibleTags();

            if (componentsDropdown.choices.Count > 0)
                componentsDropdown.index = 0;
            else
                componentsDropdown.value = TypeDescription.Empty;
            
            addTagButton.SetEnabled(componentsDropdown.choices.Count > 0);
        }
        
        public void OnRemoveSourceQueryItem(int queryIndex)
        {
            var arrayProperty = serializedObject.FindProperty(nameof(SteeringEntityAuthoring.Tags));
            arrayProperty.DeleteArrayElementAtIndex(queryIndex);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            updateDropDown();
        }
    }
}