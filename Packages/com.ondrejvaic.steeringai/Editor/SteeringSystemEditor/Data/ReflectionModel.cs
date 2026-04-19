using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SteeringAI.Core;
using UnityEngine;

namespace SteeringAI.Editor
{
    public class ReflectionModel
    {
        private List<Assembly> _Assemblies
        {
            get
            {
                assemblies ??= AppDomain.CurrentDomain.GetAssemblies().ToList();
                return assemblies;
            }
        }

        private List<TypeDescription> _NeighborJobRunners
        {
            get
            {
                neighborJobRunners ??= getNeighborJobRunners();
                return neighborJobRunners;
            }
        }

        private List<Type> _TagsComponents
        {
            get
            {
                tagsComponents ??= getTagsComponents();
                return tagsComponents;
            }
        }

        private List<TypeDescription> _RayJobRunners
        {
            get
            {
                rayJobRunners ??= getRayJobRunners();
                return rayJobRunners;
            }
        }
        
        private List<TypeDescription> _CreateRayJobRunners
        {
            get
            {
                createRayJobRunners ??= getCreateRayJobRunners();
                return createRayJobRunners;
            }
        }
        
        private List<TypeDescription> _NeighborQueryJobRunners
        {
            get
            {
                neighborQueryJobRunners ??= getNeighborQueryJobRunners();
                return neighborQueryJobRunners;
            }
        }

        private List<TypeDescription> _SimpleJobRunners
        {
            get
            {
                simpleJobRunners ??= getSimpleJobRunners();
                return simpleJobRunners;
            }
        }
        
        private List<TypeDescription> _CombineJobRunners 
        {
            get
            {
                _combineJobRunners ??= getCombineJobRunners();
                return _combineJobRunners;
            }
        }

        private List<Type> sourceQueryComponents;
        private List<Type> tagsComponents;
        private List<Assembly> assemblies;
        private Type mainSettingsType;
        private List<string> mainSettingsFields;
        private List<TypeDescription> neighborJobRunners;
        private List<TypeDescription> rayJobRunners;
        private List<TypeDescription> createRayJobRunners;
        private List<TypeDescription> neighborQueryJobRunners;
        private List<TypeDescription> simpleJobRunners;
        private List<TypeDescription> _combineJobRunners;
        private Dictionary<string, TypeDescription> raySettingsFieldToType;
        private Dictionary<Type, Type> componentsToAuthorings;

        private readonly SteeringSystemAsset steeringSystemAsset;

        public ReflectionModel(SteeringSystemAsset steeringSystemAsset)
        {
            this.steeringSystemAsset = steeringSystemAsset;
        }

        private List<TypeDescription> getNeighborJobRunners() => getJobRunners(typeof(INeighborBehaviorJobWrapper));
        
        private List<TypeDescription> getRayJobRunners() => getJobRunners(typeof(IRaycastBehaviorJobWrapper));

        private List<TypeDescription> getCreateRayJobRunners() => getJobRunners(typeof(ICreateRaysJobWrapper));
        
        private List<TypeDescription> getNeighborQueryJobRunners() => getJobRunners(typeof(INeighborQueryJobWrapper));  

        private List<TypeDescription> getSimpleJobRunners() => getJobRunners(typeof(ISimpleBehaviorJobWrapper));

        private List<TypeDescription> getCombineJobRunners() => getJobRunners(typeof(IMergeJobWrapper));
        
        private List<TypeDescription> getJobRunners(Type jobRunnerType)
        {
            var jobRunners = (
                from assembly in _Assemblies 
                    from type in assembly.GetTypes()
                        let attribute = type.GetCustomAttribute<JobWrapperAttribute>()
                        where attribute != null && jobRunnerType.IsAssignableFrom(type) && !type.IsAbstract
                                select new TypeDescription(type)
                ).AsParallel().ToList();
            
            return jobRunners;    
        }
        
        private List<Type> getTagsComponents()
        {
            var components = (
                from assembly in _Assemblies
                from type in assembly.GetTypes()
                let targetAttribute = type.GetCustomAttribute<SteeringEntityTagAttribute>()
                where targetAttribute != null && type.GetFields().Length == 0 
                select type
            ).AsParallel().ToList();
            
            return components;
        }
        
        public Dictionary<Type, Type> GetComponentsToAuthorings()
        {
            if (componentsToAuthorings != null)
                return componentsToAuthorings;
            
            componentsToAuthorings = new Dictionary<Type, Type>();
            
            foreach (var assembly in _Assemblies)
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    var attribute = type.GetCustomAttribute<ComponentAuthoringAttribute>();
                    if (attribute == null)
                        continue;
                    
                    var componentType = attribute.TargetComponentType;
                    if (componentsToAuthorings.TryGetValue(componentType, out var authoring))
                    {
                        Debug.LogWarning("Duplicate component authoring for " + componentType + " in " + type.FullName + " and " + authoring.FullName + "");
                        continue;
                    }
                        
                    componentsToAuthorings.Add(componentType, type);
                }
            }

            return componentsToAuthorings;
        }

        public List<TypeDescription> GetPossibleNeighborJobRunners(int behaviorGroupIndex)
        {
            var behaviorJobs = steeringSystemAsset.NeighborBehaviorGroupInitDatas[behaviorGroupIndex].BehaviorJobs ?? new List<JobRunnerHolder<INeighborBehaviorJobWrapper>>();
            var outAttribute = steeringSystemAsset.MergeJobWrapper.JobRunner?.GetType().GetCustomAttribute<OutDataAttribute>();
            
            if (outAttribute == null)
            {
                if (steeringSystemAsset.MergeJobWrapper.JobRunner == null)
                {
                    Debug.LogWarning("No combine job runner found");
                }
                else
                {
                    Debug.LogWarning("OutDataAttribute not found on " + steeringSystemAsset.MergeJobWrapper.JobRunner.GetType().FullName);
                }
                return new List<TypeDescription>();
            }
            var outTypes = outAttribute.OutTypes;

            var jobRunners = new List<TypeDescription>();
            foreach (var jobRunner in _NeighborJobRunners)
            {
                var jobType = Type.GetType(jobRunner.AssemblyQualifiedName);
                if (jobType == null)
                {
                    Debug.LogError(jobRunner.AssemblyQualifiedName + " not found");
                    continue;
                }
                
                var currentOutAttribute = jobType.GetCustomAttribute<OutDataAttribute>();
                if (currentOutAttribute == null)
                {
                    Debug.LogError("OutDataAttribute not found on " + jobRunner.AssemblyQualifiedName);
                    continue;
                }

                if (currentOutAttribute.OutTypes.Length != 1)
                {
                    Debug.LogError("OutDataAttribute on " + jobRunner.AssemblyQualifiedName + " must have only one type");
                    continue;
                }

                bool outMatch = outTypes.Any(t => t == currentOutAttribute.OutTypes[0]);
                
                bool contains = behaviorJobs.Exists(behaviorJob =>
                {
                    if (behaviorJob.JobRunner == null) return false;

                    return behaviorJob.JobRunner.GetType().AssemblyQualifiedName!.Equals(jobRunner.AssemblyQualifiedName);
                });
                
                if (!contains && outMatch)
                {
                    jobRunners.Add(jobRunner);
                }
            }

            return jobRunners;
        }

        public List<TypeDescription> GetPossibleTargetQueryTypes(List<TypeDescription> queriesList)
        {
            var components = (
                from type in _TagsComponents
                    where !queriesList.Exists(x => x.AssemblyQualifiedName == type.AssemblyQualifiedName)
                        select new TypeDescription(type)).AsParallel().ToList();

            return components;
        }

        public List<TypeDescription> GetAllTags()
        {
            var tags = (
                from type in _TagsComponents
                    select new TypeDescription(type)).AsParallel().ToList();
                    
            return tags;
        }

        public List<TypeDescription> GetPossibleRayJobRunners(int behaviorGroupIndex)
        {
            var behaviorJobs = steeringSystemAsset.RaycastBehaviorGroupInitDatas[behaviorGroupIndex].BehaviorJobs ?? new List<JobRunnerHolder<IRaycastBehaviorJobWrapper>>();
            var outAttribute = steeringSystemAsset.MergeJobWrapper.JobRunner?.GetType().GetCustomAttribute<OutDataAttribute>();
            
            if (outAttribute == null)
            {
                if (steeringSystemAsset.MergeJobWrapper.JobRunner == null)
                {
                    Debug.LogWarning("No combine job runner found");
                }
                else
                {
                    Debug.LogWarning("OutDataAttribute not found on " + steeringSystemAsset.MergeJobWrapper.JobRunner.GetType().FullName);
                }
                return new List<TypeDescription>();
            }
            var outTypes = outAttribute.OutTypes;

            var jobRunners = new List<TypeDescription>();
            foreach (var jobRunner in _RayJobRunners)
            {
                var jobType = Type.GetType(jobRunner.AssemblyQualifiedName);
                if (jobType == null)
                {
                    Debug.LogError(jobRunner.AssemblyQualifiedName + " not found");
                    continue;
                }
                
                var currentOutAttribute = jobType.GetCustomAttribute<OutDataAttribute>();
                if (currentOutAttribute == null)
                {
                    Debug.LogError("OutDataAttribute not found on " + jobRunner.AssemblyQualifiedName);
                    continue;
                }

                if (currentOutAttribute.OutTypes.Length != 1)
                {
                    Debug.LogError("OutDataAttribute on " + jobRunner.AssemblyQualifiedName + " must have only one type");
                    continue;
                }

                bool outMatch = outTypes.Any(t => t == currentOutAttribute.OutTypes[0]);

                bool contains = behaviorJobs.Exists(behaviorJob =>
                {
                    if (behaviorJob.JobRunner == null) return false;

                    return behaviorJob.JobRunner.GetType().AssemblyQualifiedName!.Equals(jobRunner.AssemblyQualifiedName);
                });
                
                if (!contains && outMatch)
                {
                    jobRunners.Add(jobRunner);
                }
            }

            return jobRunners;
        }

        public List<TypeDescription> GetPossibleSimpleTypes()
        {
            var behaviorJobs = steeringSystemAsset.SimpleBehaviorsList ?? new List<JobRunnerHolder<ISimpleBehaviorJobWrapper>>();
            var outAttribute = steeringSystemAsset.MergeJobWrapper.JobRunner?.GetType().GetCustomAttribute<OutDataAttribute>();
            
            if (outAttribute == null)
            {
                if (steeringSystemAsset.MergeJobWrapper.JobRunner == null)
                {
                    Debug.LogWarning("No combine job runner found");
                }
                else
                {
                    Debug.LogWarning("OutDataAttribute not found on " + steeringSystemAsset.MergeJobWrapper.JobRunner.GetType().FullName);
                }
                return new List<TypeDescription>();
            }
            var outTypes = outAttribute.OutTypes;

            var jobRunners = new List<TypeDescription>();
            foreach (var jobRunner in _SimpleJobRunners)
            {
                var jobType = Type.GetType(jobRunner.AssemblyQualifiedName);
                if (jobType == null)
                {
                    Debug.LogError(jobRunner.AssemblyQualifiedName + " not found");
                    continue;
                }
                
                var currentOutAttribute = jobType.GetCustomAttribute<OutDataAttribute>();
                if (currentOutAttribute == null)
                {
                    Debug.LogError("OutDataAttribute not found on " + jobRunner.AssemblyQualifiedName);
                    continue;
                }

                if (currentOutAttribute.OutTypes.Length != 1)
                {
                    Debug.LogError("OutDataAttribute on " + jobRunner.AssemblyQualifiedName + " must have only one type");
                    continue;
                }

                bool outMatch = outTypes.Any(t => t == currentOutAttribute.OutTypes[0]);
                
                bool contains = behaviorJobs.Exists(behaviorJob =>
                {
                    if (behaviorJob.JobRunner == null) return false;

                    return behaviorJob.JobRunner.GetType().AssemblyQualifiedName!.Equals(jobRunner.AssemblyQualifiedName);
                });
                
                if (!contains && outMatch)
                {
                    jobRunners.Add(jobRunner);
                }
            }

            return jobRunners;
        }

        public List<TypeDescription> GetCreateRayJobRunners()
        {
            return _CreateRayJobRunners;
        }
        
        public List<TypeDescription> GetNeighborQueryJobRunners()
        {
            return _NeighborQueryJobRunners;
        }

        public List<TypeDescription> GetCombineJobRunners()
        {
            return _CombineJobRunners;
        }
    }
}