using System;
using System.Collections.Generic;
using SteeringAI.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SteeringAI.Editor
{
    public class SteeringSystemEditor : EditorWindow
    {
        public VisualTreeAsset RootTemplate;
        public VisualTreeAsset NeighborBehaviorGroupItemTemplate;
        public VisualTreeAsset RayBehaviorGroupItemTemplate;
        public VisualTreeAsset NeighborBehaviorItemTemplate;
        public VisualTreeAsset QueryItemTemplate;
        public VisualTreeAsset RequiredQueryItemTemplate;
        public VisualTreeAsset SimpleBehaviorItemTemplate;
        
        public SteeringSystemAsset SteeringSystemAsset => steeringSystemAsset;
        public SteeringSystemAsset steeringSystemAsset;
        
        public ReflectionModel ReflectionModel => reflectionModel;
        private ReflectionModel reflectionModel;
        
        public SerializedObject SerializedSteeringSystemAsset => serializedSteeringSystemAsset;
        
        private Label mainEntityTagLabel;
        private Label mainSettingsLabel;

        private Dictionary<ToolbarToggle, VisualElement> behaviorRootElements;
        
        private NeighborBehaviorsController neighborBehaviorsController;
        private SimpleBehaviorsController simpleBehaviorsController;
        private RayBehaviorsController rayBehaviorsController;
        private SourceQueryController sourceQueryController;
        
        private VisualElement combineRoot;
        private PopupField<TypeDescription> combineDropdown;

        private PopupField<TypeDescription> mainTagDropdown;
        private Label mainLabel;

        private SerializedObject serializedSteeringSystemAsset;
        
        [SerializeField] public string currentSteeringSystemPath;

        public static PopupField<T> CreatePopupField<T>(VisualElement root, string parent, string labelText)
        {
            var parentVisualElement = root.Q<VisualElement>(parent);
            var newPopupField = new PopupField<T>
            {
                label = labelText
            };

            parentVisualElement.Add(newPopupField);
            
            return newPopupField;
        }

        public void Init()
        {
            steeringSystemAsset = AssetDatabase.LoadAssetAtPath<SteeringSystemAsset>(currentSteeringSystemPath);
            if (steeringSystemAsset == null)
            {
                Debug.LogWarning("Could not find " + currentSteeringSystemPath);
                currentSteeringSystemPath = null;
                return;
            }
            
            if (mainLabel != null) // is opened already
            {
                rootVisualElement.RemoveAt(0); // destroys 
            }

            steeringSystemAsset.OnValidate();
            steeringSystemAsset.Init();
            reflectionModel = new ReflectionModel(SteeringSystemAsset);

            VisualElement tree = RootTemplate.Instantiate();
            rootVisualElement.Add(tree);

            serializedSteeringSystemAsset = new SerializedObject(SteeringSystemAsset);
            
            sourceQueryController = new SourceQueryController(this, rootVisualElement);
            rayBehaviorsController = new RayBehaviorsController(this, rootVisualElement);
            neighborBehaviorsController = new NeighborBehaviorsController(this, rootVisualElement);
            simpleBehaviorsController = new SimpleBehaviorsController(this, rootVisualElement);
            
            createCombineField();
            createBehaviorToolBar();
            createMainTagPicker();
            
            var prefabField = rootVisualElement.Q<ObjectField>("prefab-field");
            prefabField.objectType = typeof(SteeringEntityAuthoring);
            prefabField.allowSceneObjects = false;
            prefabField.value = steeringSystemAsset.Prefab;
            prefabField.RegisterValueChangedCallback(prefabFieldChanged);
            string prefabName = (steeringSystemAsset.Prefab != null ? " (" + steeringSystemAsset.Prefab.name + ")" : " <none>");
            
            mainLabel = rootVisualElement.Q<Label>("title");
            mainLabel.text = $"{steeringSystemAsset.name} : {prefabName}";
        }
        
        private void CreateGUI()
        {
            if(currentSteeringSystemPath == null) return;
            
            Init();
        }

        private void prefabFieldChanged(ChangeEvent<Object> evt)
        {
            string prefabName = (evt.newValue != null ? " (" + evt.newValue.name + ")" : " <none>");
            mainLabel.text = $"{steeringSystemAsset.name} : " + prefabName;
            
            serializedSteeringSystemAsset.Update();
            steeringSystemAsset.Prefab = (SteeringEntityAuthoring)evt.newValue;
            sourceQueryController.RefreshRequiredQueries();
            serializedSteeringSystemAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(steeringSystemAsset);
            RefreshRequiredQueries();
        }

        private void createMainTagPicker()
        {
            mainTagDropdown = CreatePopupField<TypeDescription>(rootVisualElement, "main-tag-dropdown", "Main Tag:");
            
            mainTagDropdown.formatSelectedValueCallback = v => v.ToString();
            mainTagDropdown.formatListItemCallback = v => v.ToString();
            mainTagDropdown.choices = reflectionModel.GetAllTags();
            mainTagDropdown.RegisterValueChangedCallback(onMainTagChanged);
            
            if(SteeringSystemAsset.MainTagDescription.Name == null) return;
            
            var mainTag = SteeringSystemAsset.MainTagDescription;
            int index = mainTagDropdown.choices.IndexOf(mainTag);
        
            if (mainTagDropdown.choices.Count > 0)
            {
                mainTagDropdown.index = index >= 0 ? index : 0;
            }
            else
            {
                mainTagDropdown.value = TypeDescription.Empty;
            }
        }

        private void onMainTagChanged(ChangeEvent<TypeDescription> evt)
        {
            serializedSteeringSystemAsset.Update();
            steeringSystemAsset.MainTagDescription = evt.newValue;
            serializedSteeringSystemAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(steeringSystemAsset);
            RefreshRequiredQueries();
        }
        
        private void createCombineField()
        {
            combineRoot = rootVisualElement.Q<VisualElement>("combine-parent");
      
            var combineField = rootVisualElement.Q<PropertyField>("combine-job-field");
            combineField.BindProperty(SerializedSteeringSystemAsset.FindProperty(nameof(SteeringSystemAsset.MergeJobWrapper) + "." + nameof(SteeringSystemAsset.MergeJobWrapper.JobRunner)));
            
            combineDropdown = CreatePopupField<TypeDescription>(rootVisualElement, "combine-dropdown", null);
            combineDropdown.formatSelectedValueCallback = v => v.ToString();
            combineDropdown.formatListItemCallback = v => v.ToString();
            combineDropdown.choices = reflectionModel.GetCombineJobRunners();
            combineDropdown.RegisterValueChangedCallback(onCombineChanged);
            
            if(SteeringSystemAsset.MergeJobWrapper.JobRunner == null)
            {
                combineDropdown.value = TypeDescription.Empty;
                return;
            }
            
            Type moveType = SteeringSystemAsset.MergeJobWrapper.JobRunner.GetType();
            
            var currentType = new TypeDescription(moveType);
            int index = combineDropdown.choices.IndexOf(currentType);
            
            if (combineDropdown.choices.Count > 0)
            {
                combineDropdown.index = index >= 0 ? index : 0;
            }
            else
            {
                combineDropdown.value = TypeDescription.Empty;
            }
        }

        private void onCombineChanged(ChangeEvent<TypeDescription> evt)
        {
            // first trigger of the event
            if(evt.previousValue.Name == null) return;
            
            var jobInstance = (IMergeJobWrapper)Activator.CreateInstance(Type.GetType(evt.newValue.AssemblyQualifiedName)!);
            
            SteeringSystemAsset.MergeJobWrapper = new JobRunnerHolder<IMergeJobWrapper>(jobInstance);
            EditorUtility.SetDirty(SteeringSystemAsset);
            refreshTypeDropDowns();
            RefreshRequiredQueries();
        }

        private void refreshTypeDropDowns()
        {
            neighborBehaviorsController.RefreshTypeDropDowns();
            rayBehaviorsController.RefreshTypeDropDowns();
            simpleBehaviorsController.RefreshTypeDropDowns();
        }

        private void createBehaviorToolBar()
        {
            behaviorRootElements = new Dictionary<ToolbarToggle, VisualElement>
            {
                { rootVisualElement.Q<ToolbarToggle>("show-neighbor-behaviors"), neighborBehaviorsController.RootVisualElement },
                { rootVisualElement.Q<ToolbarToggle>("show-ray-behaviors"), rayBehaviorsController.RootVisualElement },
                { rootVisualElement.Q<ToolbarToggle>("show-per-component-behaviors"), simpleBehaviorsController.RootVisualElement },
                { rootVisualElement.Q<ToolbarToggle>("show-combine"), combineRoot },
            };

            foreach (var behaviorPair in behaviorRootElements)
            {
                behaviorPair.Key.RegisterValueChangedCallback((changeEvent) =>
                {
                    if (changeEvent.newValue == changeEvent.previousValue) return;
                    if (!changeEvent.newValue) return;
                    
                    foreach (var behaviorPairInner in behaviorRootElements)
                    {
                        if (behaviorPairInner.Key == behaviorPair.Key)
                        {
                            behaviorPairInner.Value.style.display = DisplayStyle.Flex;
                        }
                        else
                        {
                            behaviorPairInner.Key.value = false;
                            behaviorPairInner.Value.style.display = DisplayStyle.None;
                        }
                    }
                });
            }
        }

        public void RefreshRequiredQueries() => sourceQueryController.RefreshRequiredQueries();
    }
}
