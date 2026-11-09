// Copyright (c) Rotorz Limited. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace com.ktgame.utils.class_type_reference
{
    [CustomPropertyDrawer(typeof(ClassTypeReference))]
    [CustomPropertyDrawer(typeof(ClassTypeConstraintAttribute), true)]
    public sealed class ClassTypeReferencePropertyDrawer : PropertyDrawer
    {
        #region Type Filtering

        /// <summary>
        /// Gets or sets a function that returns a collection of types that are
        /// to be excluded from drop-down. A value of <c>null</c> specifies that
        /// no types are to be excluded.
        /// </summary>
        /// <remarks>
        /// <para>This property must be set immediately before presenting a class
        /// type reference property field using <see cref="EditorGUI.PropertyField"/>
        /// or <see cref="EditorGUILayout.PropertyField"/> since the value of this
        /// property is reset to <c>null</c> each time the control is drawn.</para>
        /// <para>Since filtering makes extensive use of <see cref="ICollection{Type}.Contains"/>
        /// it is recommended to use a collection that is optimized for fast
        /// lookups such as <see cref="HashSet{Type}"/> for better performance.</para>
        /// </remarks>
        /// <example>
        /// <para>Exclude a specific type from being selected:</para>
        /// <code language="csharp"><![CDATA[
        /// private SerializedProperty someClassTypeReferenceProperty;
        ///
        /// public override void OnInspectorGUI() {
        ///     serializedObject.Update();
        ///
        ///     ClassTypeReferencePropertyDrawer.ExcludedTypeCollectionGetter = GetExcludedTypeCollection;
        ///     EditorGUILayout.PropertyField(this.someClassTypeReferenceProperty);
        ///
        ///     serializedObject.ApplyModifiedProperties();
        /// }
        ///
        /// private ICollection<Type> GetExcludedTypeCollection() {
        ///     var set = new HashSet<Type>();
        ///     set.Add(typeof(SpecialClassToHideInDropdown));
        ///     return set;
        /// }
        /// ]]></code>
        /// </example>
        public static Func<ICollection<Type>> ExcludedTypeCollectionGetter { get; set; }

        private const ClassGrouping DefaultGrouping = ClassGrouping.ByNamespaceFlat;

        private static List<Type> GetFilteredTypes(ClassTypeConstraintAttribute filter)
        {
            var types = new List<Type>();

            var excludedTypes = (ExcludedTypeCollectionGetter != null ? ExcludedTypeCollectionGetter() : null);
            
            Type baseOrInterface = null;
            var extendsFilter = filter as ClassExtendsAttribute;
            var implementsFilter = filter as ClassImplementsAttribute;
            if (extendsFilter != null && filter.GetType() == typeof(ClassExtendsAttribute))
            {
                baseOrInterface = extendsFilter.BaseType;
            }
            else if (implementsFilter != null && filter.GetType() == typeof(ClassImplementsAttribute))
            {
                baseOrInterface = implementsFilter.InterfaceType;
            }

            if (baseOrInterface != null)
            {
                foreach (var type in TypeCache.GetTypesDerivedFrom(baseOrInterface))
                {
                    AddIfSelectable(type, filter, excludedTypes, types);
                }
            }
            else
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    FilterTypes(assembly, filter, excludedTypes, types);
                }
            }

            types.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

            return types;
        }

        private static void FilterTypes(Assembly assembly, ClassTypeConstraintAttribute filter, ICollection<Type> excludedTypes, List<Type> output)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types;
            }
            catch (Exception)
            {
                return;
            }

            foreach (var type in types)
            {
                AddIfSelectable(type, filter, excludedTypes, output);
            }
        }

        private static void AddIfSelectable(Type type, ClassTypeConstraintAttribute filter, ICollection<Type> excludedTypes, List<Type> output)
        {
            if (type == null || type.FullName == null)
            {
                return;
            }
            
            if (!type.IsVisible || !type.IsClass)
            {
                return;
            }

            if (filter != null && !filter.IsConstraintSatisfied(type))
            {
                return;
            }

            if (excludedTypes != null && excludedTypes.Contains(type))
            {
                return;
            }

            output.Add(type);
        }

        #endregion


        #region Control Drawing / Event Handling

        private static readonly int s_ControlHint = typeof(ClassTypeReferencePropertyDrawer).GetHashCode();
        private static GUIContent s_TempContent = new GUIContent();

        private static string DrawTypeSelectionControl(Rect position, GUIContent label, string classRef, ClassTypeConstraintAttribute filter)
        {
            classRef = classRef ?? "";

            if (label != null && label != GUIContent.none)
            {
                position = EditorGUI.PrefixLabel(position, label);
            }

            int controlID = GUIUtility.GetControlID(s_ControlHint, FocusType.Keyboard, position);

            bool triggerDropDown = false;

            switch (Event.current.GetTypeForControl(controlID))
            {
                case EventType.ExecuteCommand:
                    if (Event.current.commandName == "TypeReferenceUpdated")
                    {
                        if (s_SelectionControlID == controlID)
                        {
                            if (classRef != s_SelectedClassRef || EditorGUI.showMixedValue)
                            {
                                classRef = s_SelectedClassRef ?? "";
                                GUI.changed = true;
                            }

                            s_SelectionControlID = 0;
                            s_SelectedClassRef = null;
                            s_TargetWindow = null;
                        }
                    }

                    break;

                case EventType.MouseDown:
                    if (GUI.enabled && position.Contains(Event.current.mousePosition))
                    {
                        GUIUtility.keyboardControl = controlID;
                        triggerDropDown = true;
                        Event.current.Use();
                    }

                    break;

                case EventType.KeyDown:
                    if (GUI.enabled && GUIUtility.keyboardControl == controlID)
                    {
                        if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.Space)
                        {
                            triggerDropDown = true;
                            Event.current.Use();
                        }
                    }

                    break;

                case EventType.Repaint:
                    if (EditorGUI.showMixedValue)
                    {
                        s_TempContent.text = "—";
                    }
                    else
                    {
                        s_TempContent.text = ClassTypeReference.GetTypeName(classRef);
                        if (s_TempContent.text == "")
                        {
                            s_TempContent.text = "(None)";
                        }
                        else if (ClassTypeReference.ResolveType(classRef) == null)
                        {
                            s_TempContent.text += " {Missing}";
                        }
                    }

                    EditorStyles.popup.Draw(position, s_TempContent, controlID);
                    break;
            }

            if (triggerDropDown)
            {
                s_SelectionControlID = controlID;
                s_SelectedClassRef = classRef;
                s_TargetWindow = EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow : EditorWindow.mouseOverWindow;

                var filteredTypes = GetFilteredTypes(filter);
                var selectedType = EditorGUI.showMixedValue ? null : ClassTypeReference.ResolveType(classRef);
                DisplayDropDown(position, filteredTypes, selectedType, filter != null ? filter.Grouping : DefaultGrouping, !EditorGUI.showMixedValue && classRef == "");
            }

            return classRef;
        }

        private static void DrawTypeSelectionControl(Rect position, SerializedProperty property, GUIContent label, ClassTypeConstraintAttribute filter)
        {
            bool restoreShowMixedValue = EditorGUI.showMixedValue;
            try
            {
                EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

                EditorGUI.BeginChangeCheck();
                var selected = DrawTypeSelectionControl(position, label, property.stringValue, filter);
                
                if (EditorGUI.EndChangeCheck())
                {
                    property.stringValue = selected;
                }
            }
            finally
            {
                EditorGUI.showMixedValue = restoreShowMixedValue;
                ExcludedTypeCollectionGetter = null;
            }
        }

        private static void DisplayDropDown(Rect position, List<Type> types, Type selectedType, ClassGrouping grouping, bool noneSelected)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("(None)"), noneSelected, s_OnSelectedTypeName, null);
            menu.AddSeparator("");

            for (int i = 0; i < types.Count; ++i)
            {
                var type = types[i];

                string menuLabel = FormatGroupedTypeName(type, grouping);
                if (string.IsNullOrEmpty(menuLabel))
                {
                    continue;
                }

                var content = new GUIContent(menuLabel);
                menu.AddItem(content, type == selectedType, s_OnSelectedTypeName, type);
            }

            menu.DropDown(position);
        }

        private static string FormatGroupedTypeName(Type type, ClassGrouping grouping)
        {
            string name = type.FullName;

            switch (grouping)
            {
                default:
                case ClassGrouping.None:
                    return name;

                case ClassGrouping.ByNamespace:
                    return name.Replace('.', '/');

                case ClassGrouping.ByNamespaceFlat:
                    int lastPeriodIndex = name.LastIndexOf('.');
                    if (lastPeriodIndex != -1)
                    {
                        name = name.Substring(0, lastPeriodIndex) + "/" + name.Substring(lastPeriodIndex + 1);
                    }

                    return name;

                case ClassGrouping.ByAddComponentMenu:
                    var addComponentMenuAttributes = type.GetCustomAttributes(typeof(AddComponentMenu), false);
                    if (addComponentMenuAttributes.Length == 1)
                    {
                        return ((AddComponentMenu)addComponentMenuAttributes[0]).componentMenu;
                    }

                    return "Scripts/" + type.FullName.Replace('.', '/');
            }
        }

        private static int s_SelectionControlID;
        private static string s_SelectedClassRef;
        private static EditorWindow s_TargetWindow;

        private static readonly GenericMenu.MenuFunction2 s_OnSelectedTypeName = OnSelectedTypeName;

        private static void OnSelectedTypeName(object userData)
        {
            var selectedType = userData as Type;

            s_SelectedClassRef = ClassTypeReference.GetClassRef(selectedType);

            var window = s_TargetWindow != null ? s_TargetWindow : EditorWindow.focusedWindow;
            if (window == null)
            {
                Debug.LogWarning("[ClassTypeReference] Không tìm thấy cửa sổ Inspector để áp dụng lựa chọn. Hãy chọn lại.");
                s_SelectionControlID = 0;
                s_SelectedClassRef = null;
                return;
            }

            var typeReferenceUpdatedEvent = EditorGUIUtility.CommandEvent("TypeReferenceUpdated");
            window.SendEvent(typeReferenceUpdatedEvent);
            window.Repaint();
        }

        #endregion


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorStyles.popup.CalcHeight(GUIContent.none, 0);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var classRefProperty = property.propertyType == SerializedPropertyType.String
                ? property
                : property.FindPropertyRelative("classRef");

            if (classRefProperty == null || classRefProperty.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label, new GUIContent("Use ClassTypeReference or string"));
                return;
            }

            label = EditorGUI.BeginProperty(position, label, property);
            try
            {
                DrawTypeSelectionControl(position, classRefProperty, label, attribute as ClassTypeConstraintAttribute);
            }
            finally
            {
                EditorGUI.EndProperty();
            }
        }
    }
}