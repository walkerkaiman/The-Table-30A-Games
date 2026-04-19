using SteeringAI.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteeringAI.Editor
{
    public class NeighborBehaviorItemController
    {
        private readonly VisualElement visualElement;
        private int behaviorIndex = -1;
        
        public NeighborBehaviorItemController(
            VisualElement visualElement,
            NeighborBehaviorGroupItemController groupController)
        {
            this.visualElement = visualElement;
            var behaviorLabel = visualElement.Q<Label>("behavior-label");
            
            PropertyField propertyField = visualElement.Q<PropertyField>("property-field");
            propertyField.bindingPath = nameof(JobRunnerHolder<INeighborBehaviorJobWrapper>.JobRunner);
            behaviorLabel.bindingPath = nameof(JobRunnerHolder<INeighborBehaviorJobWrapper>.Name);
            
            Button removeButton = visualElement.Q<Button>("remove-button");
            removeButton.clickable.clicked += () =>
            {
                groupController.RemoveBehavior(behaviorIndex);
            };
        }

        public void SetData(SerializedProperty serializedProperty, int index)
        {
            var field = visualElement as IBindable;
            field.bindingPath = serializedProperty.propertyPath;
            
            visualElement.Bind(serializedProperty.serializedObject);
            behaviorIndex = index;
        }
    }
}