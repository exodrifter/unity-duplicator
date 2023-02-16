using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditorInternal;
using UnityEngine;

namespace Exodrifter.Duplicator
{
	public class BuildEditor : EditorWindow
	{
		private static List<BuildConfig> configs = new List<BuildConfig>();
		private ReorderableList list;

		private Vector2 leftPos;
		private Vector2 rightPos;
		private bool options;

		private void Awake()
		{
			configs = BuildUtil.LoadSettings();
		}

		private void OnEnable()
		{
			configs = BuildUtil.LoadSettings();
		}

		private void OnFocus()
		{
			configs = BuildUtil.LoadSettings();
		}

		[DidReloadScripts]
		private static void OnReload()
		{
			configs = BuildUtil.LoadSettings();
		}

		private void OnGUI()
		{
			EditorGUI.BeginChangeCheck();

			EditorGUILayout.BeginVertical();
			EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
			leftPos = EditorGUILayout.BeginScrollView(leftPos, GUILayout.Width(250));

			if (list == null)
			{
				list = new ReorderableList(configs, typeof(BuildConfig));
				list.drawHeaderCallback += (rect) =>
				{
					GUI.Label(rect, "Build Configs");
				};
				list.drawElementCallback += (rect, index, active, focused) =>
				{
					GUI.Label(rect, ((BuildConfig)list.list[index]).folder);
				};
				list.onAddCallback += (list) =>
				{
					var config = new BuildConfig();
					config.folder = "New Build Config";
					list.list.Add(config);
				};
			}
			list.DoLayoutList();

			if (GUILayout.Button("Build Defaults"))
			{
				BuildDefaults();
			}
			if (GUILayout.Button("Open Build Folder"))
			{
				OpenBuildFolder();
			}

			GUILayout.EndScrollView();
			rightPos = GUILayout.BeginScrollView(rightPos);

			if (list.index >= 0)
			{
				var config = (BuildConfig)list.list[list.index];
				DrawConfig(config);
			}
			else
			{
				GUI.enabled = false;
				EditorGUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				EditorGUILayout.BeginVertical();
				EditorGUILayout.Space(20);
				GUILayout.Label("No build selected");
				EditorGUILayout.EndVertical();
				GUILayout.FlexibleSpace();
				EditorGUILayout.EndHorizontal();
				GUI.enabled = true;
			}

			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndHorizontal();

			GUI.enabled = false;
			GUILayout.Label(BuildUtil.GetSettingsPath());
			GUI.enabled = true;

			EditorGUILayout.EndVertical();

			if (EditorGUI.EndChangeCheck())
			{
				configs = (List<BuildConfig>)list.list;
				BuildUtil.SaveSettings(configs);
			}
		}

		private void DrawConfig(BuildConfig config)
		{
			if (config == null)
			{
				throw new ArgumentNullException(nameof(config));
			}

			var targetSupported = BuildUtil.IsTargetSupported(config.target);
			if (!targetSupported && config.target != 0)
			{
				var message = string.Format(
					"Build module for {0} is not installed!",
					config.target
				);
				EditorGUILayout.HelpBox(message, MessageType.Warning);
			}

			GUI.enabled = targetSupported;
			if (GUILayout.Button(new GUIContent("Build")))
			{
				EditorApplication.delayCall += () =>
					BuildUtil.Build((BuildConfig)list.list[list.index]);
			}
			GUI.enabled = true;

			config.folder = EditorGUILayout.TextField("Folder", config.folder);

			EditorGUILayout.Space();

			config.target = (BuildTarget)EditorGUILayout.EnumPopup("Target Platform", config.target);
			EditorGUILayout.BeginHorizontal();
			switch (config.target)
			{
				// Web builds do not have an exe name; Unity will always use the
				// name index.html
				case BuildTarget.WebGL:
					GUI.enabled = false;
					EditorGUILayout.TextField("Exe Name", "index");
					GUILayout.Label(".html", GUILayout.Width(50));
					GUI.enabled = true;
					break;

				default:
					config.exeName = EditorGUILayout.TextField("Exe Name", config.exeName);
					GUILayout.Label(
						BuildUtil.GetExeFiletype(config.target),
						GUILayout.Width(50)
					);
					break;
			}
			EditorGUILayout.EndHorizontal();
			config.defaultBuild = EditorGUILayout.Toggle("Default Build", config.defaultBuild);

			options = EditorGUILayout.Foldout(options, "Build Options");
			if (options)
			{
				DrawBuildOptions(config);
			}
		}

		private void DrawBuildOptions(BuildConfig config)
		{
			foreach (BuildOptions value in Enum.GetValues(typeof(BuildOptions)))
			{
				if (value != 0)
				{
					DrawBuildOption(config, value);
				}
			}
		}

		private void DrawBuildOption(BuildConfig config, BuildOptions option)
		{
			var label = Enum.GetName(typeof(BuildOptions), option);
			var value = (config.options & option) > 0;
			value = EditorGUILayout.ToggleLeft(label, value);

			if (value)
			{
				config.options |= option;
			}
			else
			{
				config.options &= ~option;
			}
		}

		private void DrawBuildInfo(string info)
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.Space(20, false);
			GUI.enabled = false;
			EditorGUILayout.LabelField(info);
			GUI.enabled = true;
			EditorGUILayout.EndHorizontal();
		}

		#region Menu Items

		[MenuItem("Tools/Build/Build Defaults", false, 0)]
		public static void BuildDefaults()
		{
			var configs = BuildUtil.LoadSettings();

			foreach (var config in configs)
			{
				if (config.defaultBuild)
				{
					BuildUtil.Build(config);
				}
			}
		}

		[MenuItem("Tools/Build/Open Build Folder", false, 1)]
		public static void OpenBuildFolder()
		{
			Process.Start("file://" + BuildUtil.GetBuildPath());
		}

		[MenuItem("Tools/Build/Settings", false, 2)]
		public static void OpenSettingsWindow()
		{
			var window = GetWindow<BuildEditor>();
			window.titleContent = new GUIContent("Build Settings");
			window.Show();
		}

		#endregion
	}
}
