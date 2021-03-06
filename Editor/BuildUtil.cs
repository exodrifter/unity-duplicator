﻿using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Exodrifter.Duplicator
{
	public static class BuildUtil
	{
		/// <summary>
		/// Returns the path to the build folder.
		/// </summary>
		/// <returns>The path to the build folder.</returns>
		public static string GetBuildPath()
		{
			var file = "../Builds/";
			return Path.GetFullPath(Path.Combine(Application.dataPath, file));
		}

		/// <summary>
		/// Returns the path to the build settings file.
		/// </summary>
		/// <returns>The path to the build settings file.</returns>
		public static string GetSettingsPath()
		{
			var file = "../ProjectSettings/Anchor/BuildSettings.json";
			return Path.GetFullPath(Path.Combine(Application.dataPath, file));
		}

		/// <summary>
		/// Returns true if the build module is installed for the specified
		/// <see cref="BuildTarget"/>.
		/// </summary>
		/// <param name="target">The build target to check.</param>
		/// <returns>True if the build module is installed.</returns>
		public static bool IsTargetSupported(BuildTarget target)
		{
			var manager = Type.GetType(
				"UnityEditor.Modules.ModuleManager, UnityEditor.dll");

			var flags = BindingFlags.Static | BindingFlags.NonPublic;
			var fn = manager.GetMethod("GetTargetStringFromBuildTarget", flags);
			var targetStr = fn.Invoke(null, new object[] { target });

			fn = manager.GetMethod("IsPlatformSupportLoaded", flags);
			return (bool)fn.Invoke(null, new object[] { targetStr });
		}

		#region Build

		/// <summary>
		/// Builds the specified config.
		/// </summary>
		/// <param name="config">The build config to use.</param>
		public static void Build(BuildConfig config)
		{
			if (!IsTargetSupported(config.target))
			{
				Debug.LogError(string.Format(
					"Failed to build {0}; {0} build module is not installed",
					config.target
				));

				return;
			}

			// Do the build
			var path = Path.Combine(GetBuildPath(), config.folder);
			var options = new BuildPlayerOptions();
			options.target = config.target;
			options.scenes = EditorBuildSettings.scenes.Select(x => x.path).ToArray();

			switch (config.target)
			{
				// The WebGL build treats the locationPathName as the folder the
				// build will be generated in instead of the name of the
				// executable, which will always be index.html
				case BuildTarget.WebGL:
					options.locationPathName = path;
					break;

				default:
					options.locationPathName = Path.Combine(path,
						GetExeFilename(config.exeName, config.target));
				break;
			}

			try
			{
				if (Directory.Exists(path))
				{
					Directory.Delete(path, true);
				}
				BuildPipeline.BuildPlayer(options);
			}
			catch (Exception e)
			{
				Debug.LogError(string.Format(
					"Failed to build {0}; {1}",
					config.folder, e));
				return;
			}

			// Compress the build
			try
			{
				switch (config.target)
				{
					// Instead of zipping the entire folder, zip each item in
					// the folder instead for the Web build. This is because
					// itch.io expects the index.html file to be in the root
					// directory of the zipped build.
					case BuildTarget.WebGL:
						var zipFilename = path + ".zip";
						if (File.Exists(zipFilename))
						{
							File.Delete(zipFilename);
						}

						using (var zip = ZipStorer.Create(zipFilename))
						{
							var directories = Directory.GetDirectories(path);
							foreach (var directory in directories)
							{
								zip.AddDirectory(
									ZipStorer.Compression.Deflate,
									directory, ""
								);
							}

							var files = Directory.GetFiles(path);
							foreach (var file in files)
							{
								zip.AddFile(
									ZipStorer.Compression.Deflate,
									file,
									Path.GetFileName(file)
								);
							}
						}
						break;

					case BuildTarget.StandaloneWindows:
					case BuildTarget.StandaloneWindows64:
						var zf = path + ".zip";
						if (File.Exists(zf))
						{
							File.Delete(zf);
						}

						using (var zip = ZipStorer.Create(zf))
						{
							zip.AddDirectory(
								ZipStorer.Compression.Deflate,
								path, ""
							);
						}
						break;

					default:
						var archivePath = path + ".tar.gz";
						if (File.Exists(archivePath))
						{
							File.Delete(archivePath);
						}

						using (var stream = new GZipOutputStream(File.Create(archivePath)))
						{
							using (var archive = TarArchive.CreateOutputTarArchive(stream, TarBuffer.DefaultBlockFactor))
							{
								TarCompress(new DirectoryInfo(path), archive);
							}
						}
						break;
				}
			}
			catch (Exception e)
			{
				Debug.LogError(string.Format(
					"Failed to zip {0}; {1}",
					config.folder, e));
				return;
			}
		}

		private static void TarCompress(DirectoryInfo directory, TarArchive archive)
		{
			foreach (FileInfo fileToBeTarred in directory.GetFiles())
			{
				var entry = TarEntry.CreateEntryFromFile(fileToBeTarred.FullName);
				entry.TarHeader.Name = entry.TarHeader.Name.Substring("Builds/".Length);
				archive.WriteEntry(entry, true);
			}

			foreach (var d in directory.GetDirectories())
			{
				TarCompress(d, archive);
			}
		}

		private static string GetExeFilename(string exeName, BuildTarget target)
		{
			return Path.ChangeExtension(exeName, GetExeFiletype(target));
		}

		public static string GetExeFiletype(BuildTarget target)
		{
			switch (target)
			{
				case BuildTarget.Android:
					return ".apk";
				case BuildTarget.StandaloneWindows:
				case BuildTarget.StandaloneWindows64:
					return ".exe";
				case BuildTarget.StandaloneOSX:
					return ".app";
				case BuildTarget.StandaloneLinux64:
					return ".x86_64";
				case BuildTarget.WebGL:
					return ".html";
				default:
					throw new ArgumentException("Unknown target " + target);
			}
		}

		#endregion

		#region Settings

		/// <summary>
		/// This intermediate class exists because Unity does not know how to
		/// serialize a top-level JSON array.
		/// </summary>
		private struct Configs
		{
			public List<BuildConfig> configs;

			public Configs(List<BuildConfig> configs)
			{
				this.configs = configs;
			}
		}

		public static List<BuildConfig> LoadSettings()
		{
			var path = GetSettingsPath();
			if (!File.Exists(path))
			{
				return new List<BuildConfig>();
			}

			try
			{
				var json = File.ReadAllText(path);
				return JsonUtility.FromJson<Configs>(json).configs;
			}
			catch (Exception e)
			{
				Debug.LogError(string.Format(
					"Failed to load settings from \"{0}\"; {1}",
					path, e
				));

				return new List<BuildConfig>();
			}
		}

		public static void SaveSettings(List<BuildConfig> configs)
		{
			var path = GetSettingsPath();

			try
			{
				var json = JsonUtility.ToJson(new Configs(configs), true);
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path, json);
			}
			catch (Exception e)
			{
				Debug.LogError(string.Format(
					"Failed to write settings to \"{0}\"; {1}",
					path, e
				));
			}
		}

#endregion
	}
}
