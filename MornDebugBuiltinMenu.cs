using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MornEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Audio;
#endif

namespace MornDebug
{
    [CreateAssetMenu(fileName = nameof(MornDebugBuiltinMenu), menuName = "Morn/" + nameof(MornDebugBuiltinMenu))]
    public sealed class MornDebugBuiltinMenu : MornDebugMenuBase
    {
#if UNITY_EDITOR
        private class SceneTree : MornEditorTreeBase<EditorBuildSettingsScene>
        {
            public SceneTree(string prefix) : base(prefix)
            {
            }

            protected override string NodeToPath(EditorBuildSettingsScene node)
            {
                return node.path;
            }

            protected override void NodeClicked(EditorBuildSettingsScene node)
            {
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<SceneAsset>(node.path));
            }
        }

        [SerializeField] private string _scenePathPrefix;
        [SerializeField] private AudioMixer _debugMixer;
        [SerializeField] private string _mixerVolumeKey;
        private SceneTree _tree;
        private const string MixerVolumeKey = nameof(MornDebugBuiltinMenu) + "_MixerVolume";
#endif
        

        public override IEnumerable<(string key, Action action)> GetMenuItems()
        {
            yield return ("セーブマネージャ", () =>
            {
                var cachedEnabled = GUI.enabled;
                GUI.enabled = !Application.isPlaying;
                if (GUILayout.Button("PlayerPrefsをリセット"))
                {
                    PlayerPrefs.DeleteAll();
                }

                GUI.enabled = cachedEnabled;
            });
#if UNITY_EDITOR
            yield return ("サウンド", () =>
            {
                var volume = PlayerPrefs.GetFloat(MixerVolumeKey, 0);
                GUILayout.Label($"音量 : {volume} dB");
                var newVolume = GUILayout.HorizontalSlider(volume, -100, 0, GUILayout.Height(10));
                if (!Mathf.Approximately(volume, newVolume))
                {
                    PlayerPrefs.SetFloat(MixerVolumeKey, newVolume);
                    PlayerPrefs.Save();
                }
            });
            yield return ("リロード", () =>
            {
                var cachedEnabled = GUI.enabled;
                GUI.enabled = !Application.isPlaying;
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reload Domain"))
                    {
                        ReloadDomain();
                    }

                    if (GUILayout.Button("Reload Scene"))
                    {
                        ReloadScene();
                    }
                }

                GUI.enabled = cachedEnabled;
            });
            _tree = new SceneTree(_scenePathPrefix);
            foreach (var scene in EditorBuildSettings.scenes)
            {
                _tree.Add(scene);
            }

            yield return ("シーン一覧", () =>
            {
                var cachedEnabled = GUI.enabled;
                GUI.enabled = !Application.isPlaying;
                _tree.OnGUI();
                GUI.enabled = cachedEnabled;
            });
            yield return ("git/便利系", () =>
            {
                var cachedEnabled = GUI.enabled;
                GUI.enabled = !Application.isPlaying;
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Submodule更新"))
                    {
                        UpdateSubmoduleAsync().Forget();
                    }

                    if (GUILayout.Button("差分全消し"))
                    {
                        DeleteDiffAsync().Forget();
                    }
                }

                GUI.enabled = cachedEnabled;
            });
#endif
        }

        public override void OnUpdate()
        {
#if UNITY_EDITOR
            base.OnUpdate();
            if (Application.isPlaying)
            {
                if (_debugMixer)
                {
                    var volume = PlayerPrefs.GetFloat(MixerVolumeKey, 0);
                    _debugMixer.SetFloat(_mixerVolumeKey, volume);
                }
            }
#endif
        }

        private async static UniTask UpdateSubmoduleAsync(CancellationToken ct = default)
        {
            var process = MornProcess.MornProcess.CreateAtAssets("git");
            var stashName = $"{nameof(MornDebug)}による退避 {DateTime.Now:yyyy/MM/dd HH:mm:ss}";
            await process.ExecuteAsync($"stash push -m \"{stashName}\"", ct);
            await process.ExecuteAsync($"submodule foreach --recursive git stash push -m \"{stashName}\"", ct);
            await process.ExecuteAsync("submodule deinit -f --all", ct);
            await process.ExecuteAsync("submodule update --init --recursive", ct);
            process.Dispose();
            MornDebugGlobal.Log("submodule更新完了");
        }

        private async static UniTask DeleteDiffAsync(CancellationToken ct = default)
        {
            var process = MornProcess.MornProcess.CreateAtAssets("git");
            var stashName = $"{nameof(MornDebug)}による退避 {DateTime.Now:yyyy/MM/dd HH:mm:ss}";
            await process.ExecuteAsync($"stash push -m \"{stashName}\"", ct);
            await process.ExecuteAsync($"submodule foreach --recursive git stash push -m \"{stashName}\"", ct);
            await process.ExecuteAsync("reset --hard HEAD", ct);
            await process.ExecuteAsync("clean -fd", ct);
            process.Dispose();
            MornDebugGlobal.Log("差分全消し完了");
        }

#if UNITY_EDITOR
        [MenuItem("Tools/Submoduleなおすボタン")]
        private static void ReloadSubmodule()
        {
            UpdateSubmoduleAsync().Forget();
        }

        [MenuItem("Tools/差分全消しボタン")]
        private static void DeleteDiff()
        {
            DeleteDiffAsync().Forget();
        }

        [MenuItem("Tools/Reload Domain")]
        private static void ReloadDomain()
        {
            EditorUtility.RequestScriptReload();
        }

        [MenuItem("Tools/Reload Scene")]
        private static void ReloadScene()
        {
            var scene = SceneManager.GetActiveScene();
            var opts = new LoadSceneParameters();
            EditorSceneManager.LoadSceneInPlayMode(scene.path, opts);
        }
#endif
    }
}