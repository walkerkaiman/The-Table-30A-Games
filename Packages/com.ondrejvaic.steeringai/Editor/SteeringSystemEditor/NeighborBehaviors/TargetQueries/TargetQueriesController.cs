using System.Collections.Generic;
using SteeringAI.Core;
using UnityEditor;
using UnityEngine.UIElements;

namespace SteeringAI.Editor
{
    public class TargetQueriesController
    {
        private readonly VisualElement parent;

        private readonly SteeringSystemEditor editor;
        private readonly VisualTreeAsset queryItemTemplate;
        
        private readonly ListView listView;
        private readonly Button addComponentButton;
        private readonly PopupField<TypeDescription> componentsDropdown;
        private int behaviorGroupIndex;
        
        public TargetQueriesController(
            VisualElement parent,
            SteeringSystemEditor editor,
            VisualTreeAsset queryItemTemplate)
        {
            this.editor = editor;
            this.queryItemTemplate = queryItemTemplate;
            
            this.listView = parent.Q<ListView>("queries-list-view");
            
            addComponentButton = parent.Q<Button>("add-query-component-button");
            addComponentButton.clickable.clicked += onAddComponent;
            
            this.componentsDropdown = SteeringSystemEditor.CreatePopupField<TypeDescription>(parent, "drop-down-parent", "");
            componentsDropdown.formatSelectedValueCallback = v => v.Name;

        }
        
        private void onAddComponent()
        {
            var componentType = componentsDropdown.value;
            var queryList = getQueryList();
            queryList.Add(componentType);
            updateDropDown();
            listView.Rebuild();
            editor.SerializedSteeringSystemAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(editor.SteeringSystemAsset);
        }

        public void SetData(int behaviorGroupIndex)
        {
            this.behaviorGroupIndex = behaviorGroupIndex;
            
            updateDropDown();
            
            listView.makeItem = () =>
            {
                var queryItem = queryItemTemplate.Instantiate();

                var controller = new TargetQueryItemController(queryItem, this);
                queryItem.userData = controller;

                return queryItem;
            };

            listView.bindItem = (queryItem, queryItemIndex) =>
            {
                ((TargetQueryItemController)queryItem.userData).SetData(getQueryList()[queryItemIndex], queryItemIndex);
            };
            
            listView.itemsSource = getQueryList();
        }

        private List<TypeDescription> getQueryList()
        {
            return editor.SteeringSystemAsset.NeighborBehaviorGroupInitDatas[behaviorGroupIndex].AdditionalTargetTypeDescs;
        }
        
        private void updateDropDown()
        {
            componentsDropdown.choices = editor.ReflectionModel.GetPossibleTargetQueryTypes(getQueryList());

            if (componentsDropdown.choices.Count > 0)
                componentsDropdown.index = 0;
            else
                componentsDropdown.value = TypeDescription.Empty;
            
            addComponentButton.SetEnabled(componentsDropdown.choices.Count > 0);
        }

        public void OnRemoveComponent(int queryIndex)
        {
            var queryList = getQueryList();
            queryList.RemoveAt(queryIndex);
            updateDropDown();
            listView.Rebuild();
        }
    }
}