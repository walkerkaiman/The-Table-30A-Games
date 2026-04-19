using SteeringAI.Core;
using UnityEngine.UIElements;

namespace SteeringAI.Editor
{
    public class TargetQueryItemController
    {
        private readonly Label typeNameLabel;

        private int queryIndex = -1;
        
        public TargetQueryItemController(VisualElement visualElement, TargetQueriesController queriesController) 
        {
            typeNameLabel = visualElement.Q<Label>("type-name-label");
            
            Button removeButton = visualElement.Q<Button>("remove-button");
            removeButton.clickable.clicked += () => queriesController.OnRemoveComponent(queryIndex);
        }
        
        public void SetData(TypeDescription componentType, int queryIndex)
        {
            typeNameLabel.text = componentType.ToString();
            this.queryIndex = queryIndex;
        }
    }
}
