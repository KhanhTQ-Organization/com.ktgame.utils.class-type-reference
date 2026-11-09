// Copyright (c) Rotorz Limited. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root.

using System;

namespace com.ktgame.utils.class_type_reference
{
    /// <summary>
    /// Constraint that allows selection of classes that implement a specific interface
    /// when selecting a <see cref="ClassTypeReference"/> with the Unity inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ClassImplementsAttribute : ClassTypeConstraintAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClassImplementsAttribute"/> class.
        /// </summary>
        public ClassImplementsAttribute() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClassImplementsAttribute"/> class.
        /// </summary>
        /// <param name="interfaceType">Type of interface that selectable classes must implement.</param>
        public ClassImplementsAttribute(Type interfaceType)
        {
            this.InterfaceType = interfaceType;
        }
        
        public Type InterfaceType { get; private set; }


        /// <inheritdoc/>
        public override bool IsConstraintSatisfied(Type type)
        {
            if (!base.IsConstraintSatisfied(type))
            {
                return false;
            }
            
            if (this.InterfaceType == null)
            {
                return true;
            }

            bool isGenericDefinition = this.InterfaceType.IsGenericTypeDefinition;
            foreach (var interfaceType in type.GetInterfaces())
            {
                if (interfaceType == this.InterfaceType)
                {
                    return true;
                }
                
                if (isGenericDefinition && interfaceType.IsGenericType &&
                    interfaceType.GetGenericTypeDefinition() == this.InterfaceType)
                {
                    return true;
                }
            }

            return false;
        }
    }
}