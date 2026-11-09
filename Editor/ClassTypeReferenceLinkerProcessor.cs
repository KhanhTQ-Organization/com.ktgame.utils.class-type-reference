using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;
using UnityEngine;

namespace com.ktgame.utils.class_type_reference
{
    internal sealed class ClassTypeReferenceLinkerProcessor : IUnityLinkerProcessor
    {
        private static readonly string[] s_Extensions = { ".asset", ".prefab", ".unity" };
        private const long MaxFileSize = 32L * 1024 * 1024;

        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            var types = CollectReferencedTypes();

            var sb = new StringBuilder();
            sb.AppendLine("<linker>");
            foreach (var group in types.GroupBy(t => t.Assembly.GetName().Name).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"  <assembly fullname=\"{Escape(group.Key)}\" ignoreIfMissing=\"1\">");
                foreach (var type in group.OrderBy(t => t.FullName, StringComparer.Ordinal))
                {
                    sb.AppendLine($"    <type fullname=\"{Escape(type.FullName.Replace('+', '/'))}\" preserve=\"all\"/>");
                }

                sb.AppendLine("  </assembly>");
            }

            sb.AppendLine("</linker>");

            var path = Path.Combine(data.inputDirectory, "ClassTypeReference.link.xml");
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        internal static List<Type> CollectReferencedTypes()
        {
            var result = new HashSet<Type>();
            foreach (var root in GetSearchRoots())
            {
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (var file in files)
                {
                    if (!IsCandidate(file))
                    {
                        continue;
                    }

                    try
                    {
                        foreach (var line in File.ReadLines(file))
                        {
                            var trimmed = line.TrimStart();
                            if (!trimmed.StartsWith("classRef:", StringComparison.Ordinal) &&
                                !trimmed.StartsWith("_classRef:", StringComparison.Ordinal))
                            {
                                continue;
                            }

                            var value = trimmed.Substring(trimmed.IndexOf(':') + 1).Trim();
                            var type = ClassTypeReference.ResolveType(value);
                            if (type != null && !type.ContainsGenericParameters && !type.IsGenericType)
                            {
                                result.Add(type);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ClassTypeReference] Không đọc được '{file}': {e.Message}");
                    }
                }
            }

            return result.ToList();
        }

        private static IEnumerable<string> GetSearchRoots()
        {
            yield return "Assets";
            
            if (Directory.Exists("Packages"))
            {
                foreach (var dir in Directory.GetDirectories("Packages"))
                {
                    yield return dir;
                }
            }
        }

        private static bool IsCandidate(string file)
        {
            var ext = Path.GetExtension(file);
            if (Array.IndexOf(s_Extensions, ext) < 0)
            {
                return false;
            }

            try
            {
                return new FileInfo(file).Length <= MaxFileSize;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string Escape(string value)
        {
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}