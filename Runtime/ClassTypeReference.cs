// Copyright (c) Rotorz Limited. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace com.ktgame.utils.class_type_reference
{
    [Serializable]
    public sealed class ClassTypeReference : ISerializationCallbackReceiver
    {
        public static string GetClassRef(Type type)
        {
            return type != null
                ? type.FullName + ", " + type.Assembly.GetName().Name
                : "";
        }

        #region Type resolution (cached, thread-safe)
        
        private static readonly object s_CacheLock = new object();
        private static readonly Dictionary<string, Type> s_TypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static readonly HashSet<string> s_WarnedMissing = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Resolves a class reference string (format of <see cref="GetClassRef"/>) to a <see cref="Type"/>.
        /// Falls back to searching every loaded assembly when the class was moved to another assembly
        /// (asmdef). Returns <c>null</c> if the type cannot be found. Results are cached.
        /// </summary>
        public static Type ResolveType(string classRef)
        {
            if (string.IsNullOrEmpty(classRef))
            {
                return null;
            }

            lock (s_CacheLock)
            {
                if (s_TypeCache.TryGetValue(classRef, out var cached))
                {
                    return cached;
                }
            }

            Type type = null;
            try
            {
                type = Type.GetType(classRef, false);
            }
            catch (Exception)
            {
                
            }

            if (type == null)
            {
                var typeName = GetTypeName(classRef);
                if (!string.IsNullOrEmpty(typeName))
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        Type t = null;
                        try
                        {
                            t = assembly.GetType(typeName, false);
                        }
                        catch (Exception)
                        {
                         
                        }

                        if (t != null)
                        {
                            type = t;
                            break;
                        }
                    }
                }
            }

            lock (s_CacheLock)
            {
                s_TypeCache[classRef] = type;
            }

            return type;
        }

        /// <summary>
        /// Returns the type name part of a class reference string (without the trailing assembly name).
        /// Commas inside generic argument brackets are ignored, so generic types are handled correctly.
        /// </summary>
        public static string GetTypeName(string classRef)
        {
            if (string.IsNullOrEmpty(classRef))
            {
                return "";
            }

            int depth = 0;
            for (int i = 0; i < classRef.Length; ++i)
            {
                char c = classRef[i];
                if (c == '[')
                {
                    ++depth;
                }
                else if (c == ']')
                {
                    --depth;
                }
                else if (c == ',' && depth == 0)
                {
                    return classRef.Substring(0, i).Trim();
                }
            }

            return classRef.Trim();
        }

        private static void WarnMissingOnce(string classRef)
        {
            lock (s_CacheLock)
            {
                if (!s_WarnedMissing.Add(classRef))
                {
                    return;
                }
            }

            Debug.LogWarning(string.Format("'{0}' was referenced but class type was not found.", classRef));
        }

        #endregion


        [SerializeField, FormerlySerializedAs("_classRef")]
        private string classRef;


        private Type type;


        /// <summary>
        /// Initializes a new instance of the <see cref="ClassTypeReference"/> class.
        /// </summary>
        public ClassTypeReference() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClassTypeReference"/> class.
        /// </summary>
        /// <param name="assemblyQualifiedClassName">Assembly qualified class name.</param>
        /// <remarks>
        /// If the class cannot be resolved, the original string is kept (instead of being cleared)
        /// so that the reference is reported as missing rather than silently becoming "(None)".
        /// </remarks>
        public ClassTypeReference(string assemblyQualifiedClassName)
        {
            var resolved = ResolveType(assemblyQualifiedClassName);
            if (resolved != null)
            {
                this.Type = resolved;
            }
            else
            {
                this.type = null;
                this.classRef = assemblyQualifiedClassName ?? "";
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClassTypeReference"/> class.
        /// </summary>
        /// <param name="type">Class type.</param>
        /// <exception cref="System.ArgumentException">
        /// If <paramref name="type"/> is not a class type.
        /// </exception>
        public ClassTypeReference(Type type)
        {
            this.Type = type;
        }


        /// <summary>
        /// Gets or sets type of class reference.
        /// </summary>
        /// <exception cref="System.ArgumentException">
        /// If <paramref name="value"/> is not a class type.
        /// </exception>
        public Type Type
        {
            get { return this.type; }
            set
            {
                if (value != null && !value.IsClass)
                {
                    throw new ArgumentException(string.Format("'{0}' is not a class type.", value.FullName), "value");
                }

                this.type = value;
                this.classRef = GetClassRef(value);
            }
        }

        /// <summary>
        /// Raw serialized class reference string ("Namespace.Class, Assembly"). Never <c>null</c>.
        /// </summary>
        public string ClassRef
        {
            get { return this.classRef ?? ""; }
        }

        /// <summary>
        /// <c>true</c> if this reference points to a class that currently exists.
        /// Use this (or <see cref="IsValid(ClassTypeReference)"/>) instead of comparing the reference with <c>null</c>.
        /// </summary>
        public bool HasType
        {
            get { return this.type != null; }
        }

        /// <summary>
        /// <c>true</c> if a class was assigned but cannot be found anymore (deleted or renamed).
        /// </summary>
        public bool IsMissing
        {
            get { return this.type == null && !string.IsNullOrEmpty(this.classRef); }
        }

        /// <summary>
        /// Null-safe check: <c>true</c> when <paramref name="reference"/> is not <c>null</c> and points to an existing class.
        /// </summary>
        public static bool IsValid(ClassTypeReference reference)
        {
            return reference != null && reference.type != null;
        }


        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (!string.IsNullOrEmpty(this.classRef))
            {
                this.type = ResolveType(this.classRef);

                if (this.type != null)
                {
                    var current = GetClassRef(this.type);
                    if (!string.Equals(current, this.classRef, StringComparison.Ordinal) &&
                        Type.GetType(this.classRef, false) == null)
                    {
                        this.classRef = current;
                    }
                }
                else
                {
                    WarnMissingOnce(this.classRef);
                }
            }
            else
            {
                this.type = null;
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        /// <summary>Returns the class reference string, or <c>null</c> if <paramref name="typeReference"/> is <c>null</c>.</summary>
        public static implicit operator string(ClassTypeReference typeReference)
        {
            return typeReference != null ? typeReference.classRef : null;
        }

        /// <summary>Returns the referenced type, or <c>null</c> if <paramref name="typeReference"/> is <c>null</c>.</summary>
        public static implicit operator Type(ClassTypeReference typeReference)
        {
            return typeReference != null ? typeReference.Type : null;
        }

        public static implicit operator ClassTypeReference(Type type)
        {
            return new ClassTypeReference(type);
        }

        public override string ToString()
        {
            if (this.Type != null)
            {
                return this.Type.FullName;
            }

            return string.IsNullOrEmpty(this.classRef) ? "(None)" : GetTypeName(this.classRef) + " {Missing}";
        }
    }
}