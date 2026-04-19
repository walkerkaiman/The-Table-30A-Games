using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteeringAI.Core.Editor
{
    public class SourceQueryItemController
    {
        private readonly VisualElement visualElement;
        private int queryIndex = -1;
        
        public SourceQueryItemController(VisualElement visualElement, SteeringEntityInspector controller) 
        {
            this.visualElement = visualElement;
            var typeNameLabel = visualElement.Q<Label>("type-name-label");
            typeNameLabel.bindingPath = "Name"; 
             
            Button removeButton = visualElement.Q<Button>("remove-button");
            removeButton.clickable.clicked += () => controller.OnRemoveSourceQueryItem(queryIndex); 
        }
        
        public void SetData(SerializedProperty serializedProperty, int queryIndex)
        {
            var field = visualElement as IBindable;
            field.bindingPath = serializedProperty.propertyPath;
    
            visualElement.Bind(serializedProperty.serializedObject);
            this.queryIndex = queryIndex;
        }
    }
}