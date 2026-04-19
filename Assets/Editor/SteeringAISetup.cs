#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace TableGames.EditorTools
{
    /// <summary>
    /// One-shot bootstrapper for the com.ondrejvaic.steeringai package. Handles the post-install
    /// chores described in https://steeringai.ondrejvaic.com/docs/getting-started/installation
    /// so you don't have to manually hunt down each asset.
    ///
    /// Runs on editor load (idempotent) and also exposes a "Tools → Steering AI → Setup" menu.
    ///
    /// Responsibilities:
    ///   • Ensures STEERING_DEBUG is defined in Player scripting symbols (dev builds only — strip
    ///     before release for a small perf win, per the docs).
    ///   • Imports the Package Manager "SteeringAI Samples" bundle so the sample
    ///     SteeringSystemAsset files land under Assets/Samples/… and become searchable.
    ///   • Scans the entire project for any *SteeringSystemAsset* (package or Assets/) and marks
    ///     every one of them as Addressable (required by BaseSteeringSystem.OnCreate which
    ///     loads them via Addressables.LoadAssetAsync).
    ///   • Flips every UniversalRendererData in the project to Forward+ rendering path — the
    ///     samples rely on com.unity.entities.graphics which only draws correctly under
    ///     Forward+ in URP.
    /// </summary>
    [InitializeOnLoad]
    public static class SteeringAISetup
    {
        private const string PackageId = "com.ondrejvaic.steeringai";
        private const string SamplesDisplayName = "SteeringAI Samples";
        private const string SetupRanKey = "TableGames.SteeringAISetup.LastRunVersion";
        private const string CurrentRunVersion = "1";

        private const string SteeringDebugDefine = "STEERING_DEBUG";

        private const string AddressableGroupName = "SteeringAI";

        static SteeringAISetup()
        {
            if (EditorPrefs.GetString(SetupRanKey, "") == CurrentRunVersion) return;
            EditorApplication.delayCall += RunAutoSetup;
        }

        // ── Menu entry points ────────────────────────────────────────────────

        [MenuItem("Tools/Steering AI/Run Full Setup")]
        public static void MenuRunFullSetup()
        {
            EditorPrefs.DeleteKey(SetupRanKey);
            RunAutoSetup();
        }

        [MenuItem("Tools/Steering AI/Import Samples")]
        public static void MenuImportSamples()
        {
            ImportSamples();
        }

        [MenuItem("Tools/Steering AI/Mark All SteeringSystemAssets Addressable")]
        public static void MenuMarkAddressable()
        {
            int count = MarkSteeringAssetsAddressable();
            Debug.Log($"[SteeringAISetup] Marked {count} SteeringSystemAsset(s) addressable.");
        }

        [MenuItem("Tools/Steering AI/Set All URP Renderers to Forward+")]
        public static void MenuSetForwardPlus()
        {
            int count = SetAllRenderersForwardPlus();
            Debug.Log($"[SteeringAISetup] Forced {count} UniversalRendererData asset(s) to Forward+.");
        }

        // ── Main setup ───────────────────────────────────────────────────────

        private static void RunAutoSetup()
        {
            EnsureSteeringDebugDefine();
            int addressableCount = MarkSteeringAssetsAddressable();
            int forwardPlusCount = SetAllRenderersForwardPlus();

            Debug.Log(
                "[SteeringAISetup] Install bootstrap complete. " +
                $"Addressables: {addressableCount}, Forward+ renderers: {forwardPlusCount}. " +
                "If no SteeringSystemAssets were found, run Tools → Steering AI → Import Samples.");

            EditorPrefs.SetString(SetupRanKey, CurrentRunVersion);
        }

        // ── Scripting defines ────────────────────────────────────────────────

        private static void EnsureSteeringDebugDefine()
        {
            // NamedBuildTarget is Unity 2022.2+'s replacement for the BuildTargetGroup-based define API.
            // Standalone / Android / iOS / WebGL are the platforms we actually build to; adjust if we add
            // more targets in the future.
            var buildTargets = new[]
            {
                NamedBuildTarget.Standalone,
                NamedBuildTarget.Android,
                NamedBuildTarget.iOS,
                NamedBuildTarget.WebGL,
            };

            foreach (var target in buildTargets)
            {
                string defines = PlayerSettings.GetScriptingDefineSymbols(target);
                var list = defines.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                if (list.Contains(SteeringDebugDefine)) continue;

                list.Add(SteeringDebugDefine);
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
                Debug.Log($"[SteeringAISetup] Added {SteeringDebugDefine} to {target.TargetName} scripting define symbols.");
            }
        }

        // ── Samples import ───────────────────────────────────────────────────

        /// <summary>
        /// Imports the SteeringAI Samples via the Package Manager UPM API. This is equivalent to
        /// clicking the "Import" button in the Package Manager window. The samples contain all
        /// the ready-to-use SteeringSystemAssets (Sheep, Bird, Flocking variants, etc.).
        /// </summary>
        private static void ImportSamples()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                "Packages/" + PackageId + "/package.json");
            if (info == null)
            {
                Debug.LogWarning("[SteeringAISetup] com.ondrejvaic.steeringai package not found.");
                return;
            }

            var samples = UnityEditor.PackageManager.UI.Sample.FindByPackage(PackageId, info.version);
            bool imported = false;
            foreach (var sample in samples)
            {
                if (sample.displayName != SamplesDisplayName) continue;
                if (sample.isImported)
                {
                    Debug.Log($"[SteeringAISetup] {SamplesDisplayName} already imported at: {sample.importPath}");
                    imported = true;
                    continue;
                }
                if (sample.Import(UnityEditor.PackageManager.UI.Sample.ImportOptions.OverridePreviousImports))
                {
                    Debug.Log($"[SteeringAISetup] Imported {SamplesDisplayName} → {sample.importPath}");
                    imported = true;
                }
            }

            if (!imported)
            {
                Debug.LogWarning(
                    "[SteeringAISetup] Could not import SteeringAI samples automatically. " +
                    "Open Window → Package Manager → Steering AI → Samples and click Import.");
            }

            AssetDatabase.Refresh();
        }

        // ── Addressables ─────────────────────────────────────────────────────

        /// <summary>
        /// Finds every SteeringSystemAsset in the project (Assets/ and Packages/) and ensures it
        /// lives in the Addressable asset catalog, in a dedicated "SteeringAI" group. The asset
        /// address is set to the relative path the runtime expects (see BaseSteeringSystem and
        /// SteeringAI.Samples.Samples.SamplesRootPath).
        /// </summary>
        private static int MarkSteeringAssetsAddressable()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(create: true);
            if (settings == null)
            {
                Debug.LogWarning("[SteeringAISetup] Could not create AddressableAssetSettings.");
                return 0;
            }

            var group = settings.FindGroup(AddressableGroupName)
                        ?? settings.CreateGroup(
                            AddressableGroupName,
                            setAsDefaultGroup: false,
                            readOnly: false,
                            postEvent: false,
                            schemasToCopy: new List<AddressableAssetGroupSchema>(settings.DefaultGroup.Schemas));

            string[] guids = AssetDatabase.FindAssets("t:SteeringSystemAsset");
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                var entry = settings.FindAssetEntry(guid);
                if (entry == null)
                {
                    entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                }
                else if (entry.parentGroup != group)
                {
                    settings.MoveEntry(entry, group, readOnly: false, postEvent: false);
                }

                string expectedAddress = DeriveExpectedAddress(path);
                if (!string.IsNullOrEmpty(expectedAddress) && entry.address != expectedAddress)
                {
                    entry.address = expectedAddress;
                }

                count++;
            }

            if (count > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, postEvent: true, settingsModified: true);
                AssetDatabase.SaveAssets();
            }

            return count;
        }

        /// <summary>
        /// BaseSteeringSystem subclasses load their asset via a path like
        ///   "Assets/Samples/SteeringAI/1.0.0/SteeringAI Samples/9. Full Example/SteeringSystems/SheepSteeringSystemAsset.asset"
        /// That path is constructed from SteeringAI.Samples.Samples.SamplesRootPath + sub-path.
        /// We keep the Addressable address identical to the asset path so those lookups resolve.
        /// </summary>
        private static string DeriveExpectedAddress(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            if (assetPath.StartsWith("Packages/", System.StringComparison.Ordinal))
            {
                return Path.GetFileNameWithoutExtension(assetPath);
            }
            return assetPath;
        }

        // ── Forward+ URP renderer ────────────────────────────────────────────

        private static int SetAllRenderersForwardPlus()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData");
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                var obj = AssetDatabase.LoadMainAssetAtPath(path);
                if (obj == null) continue;

                var so = new SerializedObject(obj);
                var renderingModeProp = so.FindProperty("m_RenderingMode");
                if (renderingModeProp == null) continue;

                // URP RenderingMode enum: 0 = Forward, 1 = Deferred, 2 = ForwardPlus.
                if (renderingModeProp.intValue != 2)
                {
                    renderingModeProp.intValue = 2;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(obj);
                    Debug.Log($"[SteeringAISetup] Forced Forward+ on {path}");
                }

                count++;
            }

            if (count > 0) AssetDatabase.SaveAssets();
            return count;
        }
    }
}
#endif
