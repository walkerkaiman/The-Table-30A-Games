using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace SteeringAI.Core
{
    [Serializable]
    public struct TypeDescription
    {
        public string Name;
        public string AssemblyQualifiedName;

        public static TypeDescription Empty => new("<none>", "<none>");

        public TypeDescription(Type type)
        {
            Name = type.Name;
            AssemblyQualifiedName = type.AssemblyQualifiedName;
        }
        
        public TypeDescription(string name, string assemblyQualifiedName)
        {
            this.Name = name;
            this.AssemblyQualifiedName = assemblyQualifiedName;
        }

        public override string ToString()
        {
            return $"{Name}";
        }

        public override bool Equals(object obj) =>
            obj is TypeDescription other
            && Equals(other);

        private bool Equals(TypeDescription other) => 
            Name == other.Name && AssemblyQualifiedName == other.AssemblyQualifiedName;
		
        public override int GetHashCode() => 
            HashCode.Combine(Name, AssemblyQualifiedName);
    }
    
    [Serializable]
    public class NeighborhoodSettingsUI
    {
        public float MaxNeighborDistance;
        public float MaxFOV;
        public int MaxNumNeighbors;
        
        public static NeighborhoodSettingsUI Preset => new()
        {
            MaxNeighborDistance = 10,
            MaxNumNeighbors = 7,
            MaxFOV = 360,
        };

        public NeighborhoodSettings GetSettings()
        {
            return new NeighborhoodSettings
            {
                MaxNeighborDistance = MaxNeighborDistance,
                MaxNumNeighbors = MaxNumNeighbors,
                MaxFOV = MaxFOV,
            };
        }
    }
    
    [Serializable]
    public struct UINeighborhoodBehaviorGroupModel
    {
        public List<JobRunnerHolder<INeighborBehaviorJobWrapper>> BehaviorJobs;
        
        // this used to work, after updating to 2022.3.35, it just doesn't, I had to make it SerializeReference and ref type...
        // public NeighborhoodSettings Settings;
        [SerializeReference] public NeighborhoodSettingsUI Settings; 
        public List<TypeDescription> AdditionalTargetTypeDescs;
        public JobRunnerHolder<INeighborQueryJobWrapper> NeighborQueryJob;

        public List<TypeDescription> GetRequiredTargetComponentQueryTypes()
        {
            var requiredTypes = new HashSet<Type>();
            var requiredTypeDescs = new HashSet<TypeDescription>();

            foreach (var behavior in BehaviorJobs)
            {
                requiredTypes.UnionWith(SteeringSystemAsset.GetTargetTypes(behavior.JobRunner?.GetType()));   
            }

            requiredTypes.UnionWith(SteeringSystemAsset.GetTargetTypes(NeighborQueryJob.JobRunner?.GetType()));
            
            foreach (var type in requiredTypes)
            {
                requiredTypeDescs.Add(new TypeDescription(type));
            }

            return requiredTypeDescs.ToList();
        }
    }

    public class RaycastSettingsUI
    {
        public float MaxDistance;
        public int NumRays;
        public LayerMask LayerMask;

        public static RaycastSettingsUI Preset => new()
        {
            MaxDistance = 7,
            NumRays = 10,
            LayerMask = int.MaxValue
        };

        public RaycastSettings GetSettings()
        {
            return new RaycastSettings
            {
                MaxDistance = MaxDistance,
                NumRays = NumRays,
                LayerMask = LayerMask
            };
        }
    }
    
    [Serializable]
    public struct UIRaycastBehaviorGroupModel
    {
        public List<JobRunnerHolder<IRaycastBehaviorJobWrapper>> BehaviorJobs;
        public JobRunnerHolder<ICreateRaysJobWrapper> RayQueryJob;
        [SerializeReference] public RaycastSettingsUI RaycastSettings;
    }

    [Serializable]
    public struct JobRunnerHolder<T>
    {
        public JobRunnerHolder(T jobRunner)
        {
            JobRunner = jobRunner;
            Name = jobRunner.GetType().Name;
        }
        
        public string Name;
        [SerializeReference] public T JobRunner;        
    }

    [CreateAssetMenu(fileName = "SteeringSystemAsset", menuName = "SteeringSystem")]
    public class SteeringSystemAsset : ScriptableObject
    {
        [SerializeField] public JobRunnerHolder<IMergeJobWrapper> MergeJobWrapper;

        [SerializeField] public List<UINeighborhoodBehaviorGroupModel> NeighborBehaviorGroupInitDatas = new();
        
        [SerializeField] public List<UIRaycastBehaviorGroupModel> RaycastBehaviorGroupInitDatas = new();

        [SerializeField] public List<JobRunnerHolder<ISimpleBehaviorJobWrapper>> SimpleBehaviorsList = new();

        [SerializeField] public TypeDescription MainTagDescription;

        [SerializeField] public List<TypeDescription> RequiredSourceTypesList = new();
        
        [SerializeField] public SteeringEntityAuthoring Prefab;

        public ISimpleBehaviorJobWrapper[] SimpleBehaviors { get; private set; }
        
        public NeighborBehaviorGroup[] NeighborBehaviorGroups { get; private set; }

        public RaycastBehaviorGroup[] RaycastBehaviorGroups { get; private set; }

        public ComponentType[] RequiredSourceTypes { get; private set; }
        
        public static List<Type> GetSourceTypes(Type jobRunnerType)
        {
            if(jobRunnerType == null) return new List<Type>();
            
            var wrapperAttribute = jobRunnerType.GetCustomAttribute<JobWrapperAttribute>();
            return new List<Type>(wrapperAttribute.SourceTypes ?? Type.EmptyTypes);
        }
        
        public static List<Type> GetTargetTypes(Type jobRunnerType)
        {
            if(jobRunnerType == null) return new List<Type>();
            
            var wrapperAttribute = jobRunnerType.GetCustomAttribute<JobWrapperAttribute>();
            return new List<Type>(wrapperAttribute.TargetTypes ?? Type.EmptyTypes);
        }

        public void UpdateRequiredTypesList()
        {
            RequiredSourceTypesList.Clear();
            
            HashSet<Type> requiredTypes = new HashSet<Type>();

            if (MergeJobWrapper.JobRunner != null)
            {
                requiredTypes.UnionWith(GetSourceTypes(MergeJobWrapper.JobRunner.GetType()));   
            }

            foreach (var behavior in SimpleBehaviorsList)
            {
                if (behavior.JobRunner != null)
                {
                    requiredTypes.UnionWith(GetSourceTypes(behavior.JobRunner.GetType()));   
                }
            }
            
            foreach (var behavior in NeighborBehaviorGroupInitDatas)
            {
                if (behavior.NeighborQueryJob.JobRunner != null)
                {
                    requiredTypes.UnionWith(GetSourceTypes(behavior.NeighborQueryJob.JobRunner.GetType()));
                }
            }
            
            foreach (var behavior in NeighborBehaviorGroupInitDatas.SelectMany(behaviorGroup => behaviorGroup.BehaviorJobs))
            {
                if (behavior.JobRunner != null)
                {
                    requiredTypes.UnionWith(GetSourceTypes(behavior.JobRunner.GetType()));
                }
            }
            
            foreach (var behavior in RaycastBehaviorGroupInitDatas.SelectMany(behaviorGroup => behaviorGroup.BehaviorJobs))
            {
                if (behavior.JobRunner != null)
                {
                    requiredTypes.UnionWith(GetSourceTypes(behavior.JobRunner.GetType()));
                }
            }
            
            var convertedTypes = (
                from requiredType in requiredTypes
                    let type = requiredType
                    where type != null
                        select new TypeDescription(type));
            
            if(MainTagDescription.Name != null)
                RequiredSourceTypesList.Add(MainTagDescription);
                
            RequiredSourceTypesList.AddRange(convertedTypes);
        }

        public bool Load()
        {
            if (!isValid())
            {
                return false;
            }
            
            UpdateRequiredTypesList();

            RequiredSourceTypes = new ComponentType[RequiredSourceTypesList.Count];
            for (int i = 0; i < RequiredSourceTypesList.Count; i++)
            {
                var typeName = RequiredSourceTypesList[i].AssemblyQualifiedName;
                RequiredSourceTypes[i] = new ComponentType(Type.GetType(typeName), ComponentType.AccessMode.ReadOnly);
            }

            RaycastBehaviorGroups = new RaycastBehaviorGroup[RaycastBehaviorGroupInitDatas.Count];
            for (int i = 0; i < RaycastBehaviorGroupInitDatas.Count; i++)
            {
                var behaviorGroupInitData = RaycastBehaviorGroupInitDatas[i];
                var behaviorGroup = new RaycastBehaviorGroup
                {
                    BehaviorJobRunners = behaviorGroupInitData.BehaviorJobs.Where(t => t.JobRunner != null).Select(t => t.JobRunner).ToArray(),
                    RaySettings = behaviorGroupInitData.RaycastSettings.GetSettings(),
                    CreateRaysJobWrapper = behaviorGroupInitData.RayQueryJob.JobRunner
                };
                
                RaycastBehaviorGroups[i] = behaviorGroup;
            }
            
            NeighborBehaviorGroups = new NeighborBehaviorGroup[NeighborBehaviorGroupInitDatas.Count];
            for (int i = 0; i < NeighborBehaviorGroupInitDatas.Count; i++)
            {
                var behaviorGroupInitData = NeighborBehaviorGroupInitDatas[i];
                var behaviorGroup = new NeighborBehaviorGroup
                {
                    BehaviorJobRunners = behaviorGroupInitData.BehaviorJobs.Where(t => t.JobRunner != null).Select(t => t.JobRunner).ToArray(),
                    NeighborhoodSettings = behaviorGroupInitData.Settings.GetSettings(),
                    NeighborQueryJobWrapper = behaviorGroupInitData.NeighborQueryJob.JobRunner,
                    TargetQueryDesc = new EntityQueryDesc
                    {
                        All = new []
                        {
                            ComponentType.ReadOnly<LocalTransform>(),
                            ComponentType.ReadOnly<SteeringEntityTagComponent>()
                        },
                        Any = new ComponentType[behaviorGroupInitData.AdditionalTargetTypeDescs.Count]
                    }
                };

                for (int typeIndex = 0; typeIndex < behaviorGroupInitData.AdditionalTargetTypeDescs.Count; typeIndex++)
                {
                    Type type = Type.GetType(behaviorGroupInitData.AdditionalTargetTypeDescs[typeIndex].AssemblyQualifiedName);
                    if (type == null)
                    {
                        Debug.LogError("Couldn't find type " + behaviorGroupInitData.AdditionalTargetTypeDescs[typeIndex].AssemblyQualifiedName);
                    }
                    behaviorGroup.TargetQueryDesc.Any[typeIndex] = new ComponentType(type, ComponentType.AccessMode.ReadOnly);
                }
                
                NeighborBehaviorGroups[i] = behaviorGroup;
            }

            SimpleBehaviors = SimpleBehaviorsList.Where(t => t.JobRunner != null).Select(t => t.JobRunner).ToArray();
            return true;
        }

        private bool isValid()
        {
            bool isValid = true;
            
            foreach (var behaviorGroupInitData in RaycastBehaviorGroupInitDatas)
            {
                isValid = isValid && behaviorGroupInitData.BehaviorJobs.All(t => t.JobRunner != null);
                isValid = isValid && behaviorGroupInitData.RayQueryJob.JobRunner != null;
            }
            
            foreach (var behaviorGroupInitData in NeighborBehaviorGroupInitDatas)
            {
                isValid = isValid && behaviorGroupInitData.BehaviorJobs.All(t => t.JobRunner != null);
                isValid = isValid && behaviorGroupInitData.NeighborQueryJob.JobRunner != null;
            } 
 
            isValid = isValid && SimpleBehaviorsList.All(t => t.JobRunner != null);            
            isValid = isValid && MergeJobWrapper.JobRunner != null;
            
            return isValid;
        }
        
        public void OnValidate()
        {
            Init();
            
            for (int i = 0; i < RaycastBehaviorGroupInitDatas.Count; i++)
            {
                var behaviorGroupInitData = RaycastBehaviorGroupInitDatas[i];
                var invalids = behaviorGroupInitData.BehaviorJobs.Where(t => t.JobRunner == null).Select(t => t).ToArray();
                foreach (var invalid in invalids)
                {
                    Debug.LogError("Invalid raycast behavior job runner " + invalid.Name + " in asset " + name + " in ray behavior group " + i + ".\n" +
                        "This job runner was either removed or renamed, please remove it or replace it with a valid one");
                }

                if (behaviorGroupInitData.RayQueryJob.JobRunner != null) continue;
                
                if (string.IsNullOrEmpty(behaviorGroupInitData.RayQueryJob.Name))
                {
                    Debug.LogError("No ray query job runner in asset " + name + " in ray behavior group " + i + ".");
                }
                else
                {
                    Debug.LogError("Invalid job runner " + behaviorGroupInitData.RayQueryJob.Name + " in asset " + name + " in ray behavior group " + i + ".\n" +
                                   "This job runner was either removed or renamed, please remove it or replace it with a valid one");
                }
            }
            
            for (int i = 0; i < NeighborBehaviorGroupInitDatas.Count; i++)
            {
                var behaviorGroupInitData = NeighborBehaviorGroupInitDatas[i];
                var invalids = behaviorGroupInitData.BehaviorJobs.Where(t => t.JobRunner == null).Select(t => t).ToArray();
                foreach (var invalid in invalids) 
                {
                    Debug.LogError("Invalid neighbor behavior job runner " + invalid.Name + " in asset " + name + " in neighbor behavior group " + i + ".\n" + 
                        "This job runner was either removed or renamed, please remove it or replace it with a valid one");
                }

                if (behaviorGroupInitData.NeighborQueryJob.JobRunner != null) continue;
                
                if (string.IsNullOrEmpty(behaviorGroupInitData.NeighborQueryJob.Name))
                {
                    Debug.LogError("No neighbor query job runner in asset " + name + " in neighbor behavior group " + i + ".");   
                }
                else
                {
                    Debug.LogError("Invalid neighbor query job runner " + behaviorGroupInitData.NeighborQueryJob.Name + " in asset " + name + " in neighbor behavior group " + i + ".\n" +
                                   "This job runner was either removed or renamed, please remove it or replace it with a valid one");   
                }
            } 
 
            var perBehaviorInvalids = SimpleBehaviorsList.Where(t => t.JobRunner == null).Select(t => t).ToArray();
            foreach (var invalid in perBehaviorInvalids)
            {
                Debug.LogError("Invalid per component behavior job runner " + invalid.Name + " in asset " + name + " in per component behaviors " + ".\n" + 
                    "This job runner was either removed or renamed, please remove it or replace it with a valid one");
            }
            
            if (MergeJobWrapper.JobRunner == null)
            {
                if(string.IsNullOrEmpty(MergeJobWrapper.Name))
                {
                    Debug.LogError("No combine job runner in asset " + name + "in combine jobs.");
                }
                else
                {
                    Debug.LogError("Invalid combine job runner " + MergeJobWrapper.Name + " in asset " + name + " in combine jobs " + ".\n" + 
                                   "This job runner was either removed or renamed, please remove it or replace it with a valid one");   
                }
            }
        }
 
        public void Init()
        {
            for (int i = 0; i < NeighborBehaviorGroupInitDatas.Count; i++)
            {
                var groupData = NeighborBehaviorGroupInitDatas[i];
                if (groupData.NeighborQueryJob.JobRunner != null || !string.IsNullOrEmpty(groupData.NeighborQueryJob.Name)) continue;
                
                var jobRunners = (
                    from assembly in AppDomain.CurrentDomain.GetAssemblies() 
                        from type in assembly.GetTypes()
                            where typeof(INeighborQueryJobWrapper).IsAssignableFrom(type) && !type.IsAbstract 
                                select type
                ).AsParallel().ToList();
                
                if (jobRunners.Count == 0) continue;
                 
                groupData.NeighborQueryJob = new JobRunnerHolder<INeighborQueryJobWrapper>(Activator.CreateInstance(jobRunners[0]) as INeighborQueryJobWrapper);
                NeighborBehaviorGroupInitDatas[i] = groupData;
            }
            
            for (int i = 0; i < RaycastBehaviorGroupInitDatas.Count; i++)
            {
                var groupData = RaycastBehaviorGroupInitDatas[i];
                if (groupData.RayQueryJob.JobRunner != null || !string.IsNullOrEmpty(groupData.RayQueryJob.Name)) continue;
                
                var jobRunners = (
                    from assembly in AppDomain.CurrentDomain.GetAssemblies() 
                        from type in assembly.GetTypes()
                            where typeof(ICreateRaysJobWrapper).IsAssignableFrom(type) && !type.IsAbstract 
                                select type
                ).AsParallel().ToList();
                
                if (jobRunners.Count == 0) continue;
                 
                groupData.RayQueryJob = new JobRunnerHolder<ICreateRaysJobWrapper>(Activator.CreateInstance(jobRunners[0]) as ICreateRaysJobWrapper);
                RaycastBehaviorGroupInitDatas[i] = groupData;
            }

            if (MergeJobWrapper.JobRunner == null && string.IsNullOrEmpty(MergeJobWrapper.Name))
            {
                var jobRunners = (
                    from assembly in AppDomain.CurrentDomain.GetAssemblies() 
                        from type in assembly.GetTypes()
                            where typeof(IMergeJobWrapper).IsAssignableFrom(type) && !type.IsAbstract 
                                select type
                ).AsParallel().ToList();

                if (jobRunners.Count != 0)
                {
                    MergeJobWrapper = new JobRunnerHolder<IMergeJobWrapper>(Activator.CreateInstance(jobRunners[0]) as IMergeJobWrapper);
                }
            }

            if (MainTagDescription.Name == "")
            {
                var components = (
                    from assembly in AppDomain.CurrentDomain.GetAssemblies() 
                        from type in assembly.GetTypes()
                            let targetAttribute = type.GetCustomAttribute<SteeringEntityTagAttribute>()
                            where targetAttribute != null && type.GetFields().Length == 0 
                                select type
                ).AsParallel().ToList();

                if (components.Count == 0)
                {
                    MainTagDescription = TypeDescription.Empty;
                    return;
                }

                MainTagDescription = new TypeDescription(components[0].Name, components[0].AssemblyQualifiedName);
            }
        }
    }
}